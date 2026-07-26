using EDV.Framework.Persistence;
using EDV.Framework.Shared.Identity;
using EDV.Framework.Shared.Multitenancy;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EDV.Modules.Identity.Data;

internal sealed class IdentityDbInitializer(
    ILogger<IdentityDbInitializer> logger,
    IdentityDbContext context,
    RoleManager<AppRole> roleManager,
    UserManager<AppUser> userManager,
    TimeProvider timeProvider,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    ITenantInitialPasswordBuffer passwordBuffer,
    IConfiguration configuration) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("[{Tenant}] применены миграции базы данных для модуля identity", context.TenantInfo?.Identifier);
            }
        }
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedRolesAsync(cancellationToken);
        await SeedSystemGroupsAsync(cancellationToken);
        await SeedAdminUserAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken = default)
    {
        foreach (string roleName in RoleConstants.DefaultRoles)
        {
            if (await roleManager.Roles.SingleOrDefaultAsync(r => r.Name == roleName, cancellationToken)
                is not AppRole role)
            {
                // создаём роль
                role = new AppRole(roleName, $"{roleName} Role for {multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id} Tenant");
                await roleManager.CreateAsync(role);
            }

            // Назначаем разрешения
            if (roleName == RoleConstants.Basic)
            {
                await AssignPermissionsToRoleAsync(context, PermissionConstants.Basic, role, cancellationToken);
            }
            else if (roleName == RoleConstants.Admin)
            {
                await AssignPermissionsToRoleAsync(context, PermissionConstants.Admin, role, cancellationToken);

                if (multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id == MultitenancyConstants.Root.Id)
                {
                    await AssignPermissionsToRoleAsync(context, PermissionConstants.Root, role, cancellationToken);
                }
            }
        }
    }

    private async Task AssignPermissionsToRoleAsync(IdentityDbContext dbContext, IReadOnlyList<AppPermission> permissions, AppRole role, CancellationToken cancellationToken = default)
    {
        var currentClaims = await roleManager.GetClaimsAsync(role);
        var newClaims = permissions
            .Where(permission => !currentClaims.Any(c => c.Type == ClaimConstants.Permission && c.Value == permission.Name))
            .Select(permission => new AppRoleClaim
            {
                RoleId = role.Id,
                ClaimType = ClaimConstants.Permission,
                ClaimValue = permission.Name,
                CreatedBy = "application",
                CreatedOn = timeProvider.GetUtcNow()
            })
            .ToList();

        foreach (var claim in newClaims)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Заполнение разрешения '{Permission}' роли {Role} для арендатора '{TenantId}'.", claim.ClaimValue, role.Name, multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
            }
            await dbContext.RoleClaims.AddAsync(claim, cancellationToken);
        }

        // Сохраняем изменения в контексте базы данных
        if (newClaims.Count != 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

    }

    private async Task SeedSystemGroupsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        // Заполняем группу по умолчанию "All Users" - все новые пользователи автоматически добавляются в эту группу
        const string allUsersGroupName = "All Users";
        var allUsersGroup = await context.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Name == allUsersGroupName && g.IsSystemGroup, cancellationToken);

        if (allUsersGroup is null)
        {
            allUsersGroup = Group.Create(
                name: allUsersGroupName,
                description: "Default group for all users. New users are automatically added to this group.",
                isDefault: true,
                isSystemGroup: true,
                createdBy: "System");

            await context.Groups.AddAsync(allUsersGroup, cancellationToken);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Заполнение системной группы '{GroupName}' для арендатора '{TenantId}'.", allUsersGroupName, tenantId);
            }
        }

        // Заполняем группу "Administrators" с ролью Admin
        const string administratorsGroupName = "Administrators";
        var administratorsGroup = await context.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Name == administratorsGroupName && g.IsSystemGroup, cancellationToken);

        if (administratorsGroup is null)
        {
            administratorsGroup = Group.Create(
                name: administratorsGroupName,
                description: "System group for administrators with full administrative privileges.",
                isDefault: false,
                isSystemGroup: true,
                createdBy: "System");

            await context.Groups.AddAsync(administratorsGroup, cancellationToken);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Заполнение системной группы '{GroupName}' для арендатора '{TenantId}'.", administratorsGroupName, tenantId);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Назначаем роль Admin группе Administrators
        var adminRole = await roleManager.FindByNameAsync(RoleConstants.Admin);
        if (adminRole is not null)
        {
            var existingGroupRole = await context.GroupRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(gr => gr.GroupId == administratorsGroup.Id && gr.RoleId == adminRole.Id, cancellationToken);

            if (existingGroupRole is null)
            {
                context.GroupRoles.Add(GroupRole.Create(administratorsGroup.Id, adminRole.Id));

                await context.SaveChangesAsync(cancellationToken);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Роль Admin назначена группе '{GroupName}' для арендатора '{TenantId}'.", administratorsGroupName, tenantId);
                }
            }
        }
    }

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id) || string.IsNullOrWhiteSpace(multiTenantContextAccessor.MultiTenantContext.TenantInfo?.AdminEmail))
        {
            return;
        }

        if (await userManager.Users.FirstOrDefaultAsync(u => u.Email == multiTenantContextAccessor.MultiTenantContext.TenantInfo!.AdminEmail, cancellationToken)
            is not AppUser adminUser)
        {
            string adminUserName = $"{multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id.Trim()}.{RoleConstants.Admin}".ToUpperInvariant();
            adminUser = new AppUser
            {
                FirstName = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id.Trim().ToUpperInvariant(),
                LastName = RoleConstants.Admin,
                Email = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.AdminEmail,
                UserName = adminUserName,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.AdminEmail!.ToUpperInvariant(),
                NormalizedUserName = adminUserName.ToUpperInvariant(),
                // Нет аватара по умолчанию: ассет никогда не поставлялся, а формирование абсолютного
                // URL {OriginUrl}/… во время заполнения привязало бы его к localhost-источнику сидера
                // (у мигратора нет OriginOptions). Оставляем null → SPA отрисовывает инициалы.
                ImageUrl = null,
                IsActive = true
            };

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Заполнение администратора по умолчанию для арендатора '{TenantId}'.", multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
            }
            var initialPassword = ResolveInitialAdminPassword(multiTenantContextAccessor.MultiTenantContext.TenantInfo!.Id!);
            var password = new PasswordHasher<AppUser>();
            adminUser.PasswordHash = password.HashPassword(adminUser, initialPassword);
            // ОБЯЗАТЕЛЬНО проверяем IdentityResult: тихий сбой (отказ по политике паролей, временная
            // ошибка БД) пометил бы провижининг как "Completed" без администратора; исключение делает
            // его повторяемым Failed.
            var createResult = await userManager.CreateAsync(adminUser);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Не удалось заполнить администратора для арендатора '{multiTenantContextAccessor.MultiTenantContext.TenantInfo!.Id}': "
                    + string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }
        }

        // Назначаем роль пользователю
        if (!await userManager.IsInRoleAsync(adminUser, RoleConstants.Admin))
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Назначение роли Admin администратору для арендатора '{TenantId}'.", multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
            }
            await userManager.AddToRoleAsync(adminUser, RoleConstants.Admin);
        }
    }

    /// <summary>
    /// Определяет начальный пароль для администратора, заполняемого в арендатора.
    /// Порядок поиска:
    ///   1. <see cref="ITenantInitialPasswordBuffer"/> — устанавливается <c>CreateTenantCommandHandler</c>
    ///      для арендаторов, создаваемых во время выполнения (атомарное потребление, исчезает после вызова).
    ///   2. <c>Seed:DefaultAdminPassword</c> из конфигурации — покрывает заполнение корневого
    ///      арендатора фреймворком при старте и любой bootstrap тестового хоста. Операторы
    ///      задают это через переменную окружения / user-secrets / менеджер секретов продакшена.
    /// Выбрасывает исключение, если ни один источник не предоставил пароль — отказ от заполнения
    /// безопаснее, чем создание администратора с предсказуемым секретом.
    /// </summary>
    private string ResolveInitialAdminPassword(string tenantId)
    {
        var buffered = passwordBuffer.TryConsume(tenantId);
        if (!string.IsNullOrWhiteSpace(buffered))
        {
            return buffered;
        }

        var fromConfig = configuration["Seed:DefaultAdminPassword"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            return fromConfig;
        }

        throw new InvalidOperationException(
            $"Нет доступного начального пароля администратора для арендатора '{tenantId}'. " +
            "Передайте AdminPassword в запросе CreateTenant, либо задайте " +
            "'Seed:DefaultAdminPassword' в конфигурации для заполнения root/startup.");
    }
}