using Asp.Versioning;
using EDV.Framework.Core.Context;
using EDV.Framework.Eventing;
using EDV.Framework.Persistence;
using EDV.Framework.Quota;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Storage;
using EDV.Framework.Web.Modules;
using EDV.Modules.Identity.Authorization;
using EDV.Modules.Identity.Authorization.Jwt;
using EDV.Modules.Identity.Contracts.Authorization;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using EDV.Modules.Identity.Features.v1.Groups.AddUsersToGroup;
using EDV.Modules.Identity.Features.v1.Groups.CreateGroup;
using EDV.Modules.Identity.Features.v1.Groups.DeleteGroup;
using EDV.Modules.Identity.Features.v1.Groups.GetGroupById;
using EDV.Modules.Identity.Features.v1.Groups.GetGroupMembers;
using EDV.Modules.Identity.Features.v1.Groups.GetGroups;
using EDV.Modules.Identity.Features.v1.Groups.RemoveUserFromGroup;
using EDV.Modules.Identity.Features.v1.Groups.UpdateGroup;
using EDV.Modules.Identity.Features.v1.Impersonation.EndImpersonation;
using EDV.Modules.Identity.Features.v1.Impersonation.GetImpersonationGrants;
using EDV.Modules.Identity.Features.v1.Impersonation.RevokeImpersonationGrant;
using EDV.Modules.Identity.Features.v1.Impersonation.StartImpersonation;
using EDV.Modules.Identity.Features.v1.Permissions.GetPermissionCatalog;
using EDV.Modules.Identity.Features.v1.Roles;
using EDV.Modules.Identity.Features.v1.Roles.DeleteRole;
using EDV.Modules.Identity.Features.v1.Roles.GetRoleById;
using EDV.Modules.Identity.Features.v1.Roles.GetRoles;
using EDV.Modules.Identity.Features.v1.Roles.GetRoleWithPermissions;
using EDV.Modules.Identity.Features.v1.Roles.UpdateRolePermissions;
using EDV.Modules.Identity.Features.v1.Roles.UpsertRole;
using EDV.Modules.Identity.Features.v1.Sessions.AdminRevokeAllSessions;
using EDV.Modules.Identity.Features.v1.Sessions.AdminRevokeSession;
using EDV.Modules.Identity.Features.v1.Sessions.GetMySessions;
using EDV.Modules.Identity.Features.v1.Sessions.GetTenantSessions;
using EDV.Modules.Identity.Features.v1.Sessions.GetUserSessions;
using EDV.Modules.Identity.Features.v1.Sessions.RevokeAllSessions;
using EDV.Modules.Identity.Features.v1.Sessions.RevokeSession;
using EDV.Modules.Identity.Features.v1.Tokens.RefreshToken;
using EDV.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using EDV.Modules.Identity.Features.v1.TwoFactor.Disable;
using EDV.Modules.Identity.Features.v1.TwoFactor.Enroll;
using EDV.Modules.Identity.Features.v1.TwoFactor.VerifyEnroll;
using EDV.Modules.Identity.Features.v1.Users.AdminConfirmEmail;
using EDV.Modules.Identity.Features.v1.Users.AssignUserRoles;
using EDV.Modules.Identity.Features.v1.Users.ChangePassword;
using EDV.Modules.Identity.Features.v1.Users.ConfirmEmail;
using EDV.Modules.Identity.Features.v1.Users.DeleteUser;
using EDV.Modules.Identity.Features.v1.Users.ForgotPassword;
using EDV.Modules.Identity.Features.v1.Users.GetUserById;
using EDV.Modules.Identity.Features.v1.Users.GetUserGroups;
using EDV.Modules.Identity.Features.v1.Users.GetUserPermissions;
using EDV.Modules.Identity.Features.v1.Users.GetUserProfile;
using EDV.Modules.Identity.Features.v1.Users.GetUserRoles;
using EDV.Modules.Identity.Features.v1.Users.GetUsers;
using EDV.Modules.Identity.Features.v1.Users.RegisterUser;
using EDV.Modules.Identity.Features.v1.Users.ResendConfirmationEmail;
using EDV.Modules.Identity.Features.v1.Users.ResetPassword;
using EDV.Modules.Identity.Features.v1.Users.SearchUsers;
using EDV.Modules.Identity.Features.v1.Users.SelfRegistration;
using EDV.Modules.Identity.Features.v1.Users.SetProfileImage;
using EDV.Modules.Identity.Features.v1.Users.ToggleUserStatus;
using EDV.Modules.Identity.Features.v1.Users.UpdateUser;
using EDV.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace EDV.Modules.Identity;

public class IdentityModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(
            IdentityPermissions.All);

        var services = builder.Services;
        services.AddScoped<RolePermissionSyncer>();
        services.AddHostedService<RolePermissionSyncHostedService>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, PathAwareAuthorizationHandler>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<ICurrentUserService>());
        services.AddScoped<ICurrentUserInitializer>(sp => sp.GetRequiredService<ICurrentUserService>());
        services.AddScoped<IRequestContextService, RequestContextService>();
        services.AddScoped<IRequestContext>(sp => sp.GetRequiredService<IRequestContextService>());
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IImpersonationGrantService, ImpersonationGrantService>();

        // Сервисы пользователей — сфокусированные сервисы с единственной ответственностью
        services.AddTransient<IUserRegistrationService, UserRegistrationService>();
        services.AddTransient<IUserProfileService, UserProfileService>();
        services.AddTransient<IUserStatusService, UserStatusService>();
        services.AddTransient<IUserRoleService, UserRoleService>();
        services.AddTransient<IUserPasswordService, UserPasswordService>();
        services.AddTransient<IUserPermissionService, UserPermissionService>();

        // Фасад для обратной совместимости
        services.AddTransient<IUserService, UserService>();

        services.AddTransient<IRoleService, RoleService>();
        services.AddStorage(builder.Configuration);
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddDbContext<IdentityDbContext>();
        services.AddEventingCore(builder.Configuration);
        services.AddEventingForDbContext<IdentityDbContext>();
        services.AddIntegrationEventHandlers(typeof(IdentityModule).Assembly);
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<IdentityDbContext>(
                name: "db:identity",
                failureStatus: HealthStatus.Unhealthy);
        services.AddScoped<IDbInitializer, IdentityDbInitializer>();

        // Настраиваем параметры политики паролей
        services.Configure<PasswordPolicyOptions>(builder.Configuration.GetSection("PasswordPolicy"));

        // Льготный период подписки арендатора (общая секция "Billing") — используется проверкой истечения при входе.
        services.Configure<TenantGraceOptions>(builder.Configuration.GetSection(TenantGraceOptions.SectionName));

        // Регистрируем сервис истории паролей
        services.AddScoped<IPasswordHistoryService, PasswordHistoryService>();

        // Регистрируем сервис истечения срока пароля
        services.AddScoped<IPasswordExpiryService, PasswordExpiryService>();

        // Регистрируем сервис сессий и фоновую очистку
        services.AddScoped<ISessionService, SessionService>();
        services.AddHostedService<SessionCleanupHostedService>();

        // Регистрируем сервис ролей групп для прав, унаследованных от групп
        services.AddScoped<IGroupRoleService, GroupRoleService>();

        // Датчик квоты: сообщает текущее число пользователей по арендатору для квоты Users.
        services.AddScoped<IQuotaGaugeProvider, UserCountQuotaGaugeProvider>();

        services.AddIdentity<AppUser, AppRole>(options =>
        {
            options.Password.RequiredLength = IdentityModuleConstants.PasswordLength;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.User.RequireUniqueEmail = true;

            // Блокировка аккаунта: 5 последовательных неудачных входов → блокировка на 15 минут (по умолчанию применяется к новым пользователям).
            // Поток входа IdentityService управляет AccessFailedAsync / IsLockedOutAsync.
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
           .AddEntityFrameworkStores<IdentityDbContext>()
           .AddDefaultTokenProviders();

        // метрики
        services.AddSingleton<IdentityMetrics>();

        services.ConfigureJwtAuth();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var apiVersionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/identity")
            .WithTags("Identity")
            .WithApiVersionSet(apiVersionSet);

        // токены
        group.MapGenerateTokenEndpoint().AllowAnonymous().RequireRateLimiting("auth");
        group.MapRefreshTokenEndpoint().AllowAnonymous().RequireRateLimiting("auth");

        // Outbox диспетчеризуется фреймворковым OutboxDispatcherHostedService (включён по умолчанию). Второй диспетчер
        // здесь конкурировал бы за те же строки (нет захвата на уровне строки) → дублирующиеся обработчики + коллизии PK_InboxMessages, поэтому этот модуль не регистрирует ни одного.

        // роли
        group.MapGetRolesEndpoint();
        group.MapGetRoleByIdEndpoint();
        group.MapDeleteRoleEndpoint();
        group.MapGetRolePermissionsEndpoint();
        group.MapUpdateRolePermissionsEndpoint();
        group.MapCreateOrUpdateRoleEndpoint();

        // каталог разрешений — каждое разрешение, зарегистрированное в хосте,
        // отфильтрованное по контексту арендатора вызывающего (набор root vs admin)
        group.MapGetPermissionCatalogEndpoint();

        // пользователи
        group.MapAssignUserRolesEndpoint();
        group.MapChangePasswordEndpoint();
        group.MapAdminConfirmEmailEndpoint();
        group.MapResendConfirmationEmailEndpoint().RequireRateLimiting("auth");
        group.MapConfirmEmailEndpoint().RequireRateLimiting("auth");
        group.MapDeleteUserEndpoint();
        group.MapGetUserByIdEndpoint();
        group.MapGetCurrentUserPermissionsEndpoint();
        group.MapGetMeEndpoint();
        group.MapGetUserRolesEndpoint();
        group.MapGetUsersListEndpoint();
        group.MapSearchUsersEndpoint();
        group.MapRegisterUserEndpoint();
        group.MapForgotPasswordEndpoint().RequireRateLimiting("auth");
        group.MapResetPasswordEndpoint().RequireRateLimiting("auth");
        group.MapSelfRegisterUserEndpoint().RequireRateLimiting("auth");
        group.MapToggleUserStatusEndpoint();
        group.MapUpdateUserEndpoint();
        group.MapSetProfileImageEndpoint();

        // сессии - пользовательские эндпоинты
        group.MapGetMySessionsEndpoint();
        group.MapRevokeSessionEndpoint();
        group.MapRevokeAllSessionsEndpoint();

        // сессии - административные эндпоинты
        group.MapGetTenantSessionsEndpoint();
        group.MapGetUserSessionsEndpoint();
        group.MapAdminRevokeSessionEndpoint();
        group.MapAdminRevokeAllSessionsEndpoint();

        // группы
        group.MapGetGroupsEndpoint();
        group.MapGetGroupByIdEndpoint();
        group.MapCreateGroupEndpoint();
        group.MapUpdateGroupEndpoint();
        group.MapDeleteGroupEndpoint();
        group.MapGetGroupMembersEndpoint();
        group.MapAddUsersToGroupEndpoint();
        group.MapRemoveUserFromGroupEndpoint();

        // группы пользователя
        group.MapGetUserGroupsEndpoint();

        // имперсонация
        group.MapStartImpersonationEndpoint();
        group.MapEndImpersonationEndpoint();
        group.MapGetImpersonationGrantsEndpoint();
        group.MapRevokeImpersonationGrantEndpoint();

        // двухфакторная аутентификация (TOTP)
        group.MapEnrollTwoFactorEndpoint();
        group.MapVerifyEnrollTwoFactorEndpoint();
        group.MapDisableTwoFactorEndpoint();
    }
}