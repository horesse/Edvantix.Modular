using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Identity;
using EDV.Modules.Identity.Contracts.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EDV.Modules.Identity.Authorization.Jwt;

public class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _options;
    private readonly IHostEnvironment _environment;

    public ConfigureJwtBearerOptions(IOptions<JwtOptions> options, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        _options = options.Value;
        _environment = environment;
    }

    public void Configure(JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Configure(string.Empty, options);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        byte[] key = Encoding.ASCII.GetBytes(_options.SigningKey);

        options.RequireHttpsMetadata = true;
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidIssuer = _options.Issuer,
            ValidateIssuer = true,
            ValidateLifetime = true,
            ValidAudience = _options.Audience,
            ValidateAudience = true,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        // Захватываем причину ошибки валидации, чтобы OnChallenge могла включить её (в Development).
        // Без этого мы получаем тело `{"error":"Unauthorized"}` без подсказки, почему JwtBearer отклонил запрос.
        const string FailureKey = "JwtAuthFailure";
        bool isDev = _environment.IsDevelopment();

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                // Сохраняем тип исключения + сообщение в HttpContext, чтобы OnChallenge могла вывести их.
                context.HttpContext.Items[FailureKey] =
                    $"{context.Exception.GetType().Name}: {context.Exception.Message}";

                // Серверное логирование, чтобы видеть причину отклонения в консоли API.
                var failedLogger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("EDV.Identity.JwtAuth");
                failedLogger.LogWarning(context.Exception,
                    "Аутентификация JwtBearer НЕ УДАЛАСЬ для {Method} {Path}: {Reason}",
                    SanitizeForLog(context.HttpContext.Request.Method),
                    SanitizeForLog(context.HttpContext.Request.Path.ToString()),
                    context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";

                    // Был ли вообще отправлен заголовок Authorization? Помогает отличить "JWT отклонён"
                    // от "токен отсутствует" — оба дают 401, но по совершенно разным причинам.
                    bool hadAuthHeader = !string.IsNullOrEmpty(context.HttpContext.Request.Headers.Authorization);

                    // RFC 9457 ProblemDetails — соответствует контракту, используемому остальной частью API
                    // для ответов об ошибках (через глобальный обработчик исключений).
                    var problem = new ProblemDetails
                    {
                        Type = "https://datatracker.ietf.org/doc/html/rfc7235#section-3.1",
                        Title = "Не авторизован",
                        Status = StatusCodes.Status401Unauthorized,
                        Detail = "Для доступа к этому ресурсу требуется аутентификация.",
                        Instance = context.HttpContext.Request.Path,
                    };

                    // В Development выводим реальную причину отклонения JwtBearer; в Production оставляем
                    // тело непрозрачным, чтобы избежать утечки внутренностей валидации.
                    if (isDev)
                    {
                        if (context.HttpContext.Items[FailureKey] is string reason)
                        {
                            problem.Extensions["reason"] = reason;
                        }
                        else if (!hadAuthHeader)
                        {
                            problem.Extensions["reason"] = "В запросе отсутствует заголовок Authorization.";
                        }
                        else
                        {
                            // Заголовок присутствует, но JwtBearer не сработал OnAuthenticationFailed —
                            // обычно это означает, что схема bearer не соответствует AuthorizationPolicy.
                            problem.Extensions["reason"] = "Bearer-токен присутствует, но JwtBearer не выполнил его валидацию (несоответствие схемы?).";
                        }

                        var challengeLogger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("EDV.Identity.JwtAuth");
                        challengeLogger.LogWarning(
                            "Вызов JwtBearer для {Method} {Path}: hadAuthHeader={HadHeader} reason={Reason}",
                            SanitizeForLog(context.HttpContext.Request.Method),
                            SanitizeForLog(context.HttpContext.Request.Path.ToString()),
                            hadAuthHeader,
                            problem.Extensions["reason"]);
                    }

                    var traceId = context.HttpContext.TraceIdentifier;
                    if (!string.IsNullOrEmpty(traceId))
                    {
                        problem.Extensions["traceId"] = traceId;
                    }

                    var result = System.Text.Json.JsonSerializer.Serialize(problem);
                    return context.Response.WriteAsync(result);
                }
                return Task.CompletedTask;
            },
            // Серверная защита за /impersonation/revoke: для токена имперсонализации отклоняем, если
            // его разрешение отозвано/завершено — иначе отзыв не остановил бы уже находящиеся в пути токены.
            OnTokenValidated = async context =>
            {
                var actSub = context.Principal?.FindFirstValue(ClaimConstants.ActorSubject);
                if (string.IsNullOrEmpty(actSub))
                {
                    // Не токен имперсонализации — нулевая стоимость для обычных сессий.
                    return;
                }

                var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                if (string.IsNullOrEmpty(jti))
                {
                    // jti всегда создаётся в Start; отклоняем защитно, чтобы некорректный/обрезанный токен
                    // не мог обойти проверку отзыва.
                    context.HttpContext.Items[FailureKey] = "В токене имперсонализации отсутствует claim jti";
                    context.Fail("в токене имперсонализации отсутствует claim jti");
                    return;
                }

                // Разрешаем в ДОЧЕРНЕЙ области: этот хук выполняется до того, как Finbuckle разрешит арендатора,
                // поэтому IdentityDbContext с областью запроса кэшировал бы контекст с null-арендатором
                // и позже вызывал NRE в фильтрах запросов по арендатору.
                await using var hookScope = context.HttpContext.RequestServices.CreateAsyncScope();
                var grants = hookScope.ServiceProvider
                    .GetRequiredService<IImpersonationGrantService>();
                var revoked = await grants
                    .IsRevokedOrEndedAsync(jti, context.HttpContext.RequestAborted)
                    .ConfigureAwait(false);

                if (revoked)
                {
                    context.HttpContext.Items[FailureKey] = "Разрешение на имперсонализацию отозвано или завершено";
                    context.Fail("разрешение на имперсонализацию отозвано или завершено");
                }
            },
            OnForbidden = _ => throw new ForbiddenException(),
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Task.CompletedTask;
                }

                var path = context.HttpContext.Request.Path;
                // Browser EventSource/SignalR не могут отправить заголовок Authorization, поэтому используют
                // ?access_token=. Узкий список разрешённых путей предотвращает утечку токенов из строки запроса.
                if (path.StartsWithSegments("/notifications", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWithSegments("/api/v1/realtime/hub", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    }

    // Удаляем управляющие символы, чтобы данные из запроса от злоумышленника не могли подделать строки логов
    // (CodeQL cs/log-injection); защита в глубину поверх валидации URI Kestrel.
    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            buffer.Append(char.IsControl(c) ? '_' : c);
        }
        return buffer.ToString();
    }
}