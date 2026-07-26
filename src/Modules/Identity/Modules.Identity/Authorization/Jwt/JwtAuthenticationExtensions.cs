using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EDV.Modules.Identity.Authorization.Jwt;

internal static class JwtAuthenticationExtensions
{
    internal static IServiceCollection ConfigureJwtAuth(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(nameof(JwtOptions))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        services
            .AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, null!);

        services.AddAuthorizationBuilder().AddRequiredPermissionPolicy();
        services.AddAuthorization(options =>
        {
            // Проверка разрешений находится в политике RequiredPermission (она считывает
            // метаданные RequiredPermissionAttribute каждой конечной точки). Настраиваем КАК
            // политику по умолчанию, ТАК И резервную политику:
            //   - FallbackPolicy покрывает конечные точки без метаданных аутентификации.
            //   - DefaultPolicy покрывает конечные точки, которые явно подключаются через
            //     .RequireAuthorization() — включая группы маршрутов модулей (Catalog/Billing/Chat/Files/…).
            //     Без этого групповой .RequireAuthorization() применял встроенную политику
            //     по умолчанию "только аутентифицированные", которая ПОДАВЛЯЛА резервную политику,
            //     поэтому .RequirePermission(...) никогда не выполнялся, и любой аутентифицированный
            //     участник арендатора мог выполнять защищённые запись операции. Обе должны указывать
            //     на политику разрешений.
            options.DefaultPolicy = options.GetPolicy(RequiredPermissionDefaults.PolicyName)!;
            options.FallbackPolicy = options.GetPolicy(RequiredPermissionDefaults.PolicyName);
        });
        return services;
    }
}