using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Data;
using EDV.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EDV.Modules.Identity.Services;

internal sealed class PasswordHistoryService : IPasswordHistoryService
{
    private readonly IdentityDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly PasswordPolicyOptions _passwordPolicyOptions;

    public PasswordHistoryService(
        IdentityDbContext db,
        UserManager<AppUser> userManager,
        IOptions<PasswordPolicyOptions> passwordPolicyOptions)
    {
        _db = db;
        _userManager = userManager;
        _passwordPolicyOptions = passwordPolicyOptions.Value;
    }

    public async Task<bool> IsPasswordInHistoryAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(newPassword);

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        // Получаем последние N паролей из истории (где N = PasswordHistoryCount)
        var passwordHistoryCount = _passwordPolicyOptions.PasswordHistoryCount;
        if (passwordHistoryCount <= 0)
        {
            return false; // Проверка истории паролей отключена
        }

        var recentPasswordHashes = await _db.Set<PasswordHistory>()
            .Where(ph => ph.UserId == userId)
            .OrderByDescending(ph => ph.CreatedAt)
            .Take(passwordHistoryCount)
            .Select(ph => ph.PasswordHash)
            .ToListAsync(cancellationToken);

        // Проверяем, совпадает ли новый пароль с каким-либо из недавних
        foreach (var passwordHash in recentPasswordHashes)
        {
            var passwordHasher = _userManager.PasswordHasher;
            var result = passwordHasher.VerifyHashedPassword(user, passwordHash, newPassword);

            if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                return true; // Пароль есть в истории
            }
        }

        return false; // Пароля нет в истории
    }

    public async Task SavePasswordHistoryAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return;
        }

        var passwordHistoryEntry = PasswordHistory.Create(userId, user.PasswordHash);

        _db.Set<PasswordHistory>().Add(passwordHistoryEntry);
        await _db.SaveChangesAsync(cancellationToken);

        // Очищаем старые записи истории паролей
        await CleanupOldPasswordHistoryAsync(userId, cancellationToken);
    }

    public async Task CleanupOldPasswordHistoryAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var passwordHistoryCount = _passwordPolicyOptions.PasswordHistoryCount;
        if (passwordHistoryCount <= 0)
        {
            return; // История паролей отключена
        }

        // Получаем все записи истории паролей пользователя, отсортированные от новых к старым
        var allPasswordHistories = await _db.Set<PasswordHistory>()
            .Where(ph => ph.UserId == userId)
            .OrderByDescending(ph => ph.CreatedAt)
            .ToListAsync(cancellationToken);

        // Оставляем только настроенное количество паролей
        if (allPasswordHistories.Count > passwordHistoryCount)
        {
            var oldPasswordHistories = allPasswordHistories
                .Skip(passwordHistoryCount)
                .ToList();

            _db.Set<PasswordHistory>().RemoveRange(oldPasswordHistories);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}