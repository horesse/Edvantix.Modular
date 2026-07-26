using EDV.Framework.Core.Exceptions;
using EDV.Framework.Shared.Multitenancy;
using EDV.Framework.Shared.Storage;
using EDV.Framework.Storage;
using EDV.Framework.Storage.Services;
using EDV.Framework.Web.Origin;
using EDV.Modules.Identity.Contracts.DTOs;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EDV.Modules.Identity.Services;

internal sealed class UserProfileService(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IStorageService storageService,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    IOptions<OriginOptions> originOptions,
    IHttpContextAccessor httpContextAccessor) : IUserProfileService
{
    private readonly Uri? _originUrl = originOptions.Value.OriginUrl;

    public async Task<UserDto> GetAsync(string userId, CancellationToken cancellationToken)
    {
        // Полагается на фильтр арендатора Finbuckle — вызывающий код может прочитать только
        // собственную запись пользователя, которая находится в разрешённом для запроса арендаторе.
        var user = await userManager.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException("пользователь не найден");

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            ImageUrl = ResolveImageUrl(user.ImageUrl),
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            TwoFactorEnabled = user.TwoFactorEnabled,
        };
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken) =>
        userManager.Users.AsNoTracking().CountAsync(cancellationToken);

    public async Task<List<UserDto>> GetListAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            result.Add(new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ImageUrl = ResolveImageUrl(user.ImageUrl),
                IsActive = user.IsActive
            });
        }

        return result;
    }

    public async Task UpdateAsync(string userId, string firstName, string lastName, string phoneNumber, FileUploadRequest image, bool deleteCurrentImage, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("пользователь не найден");

        Uri imageUri = user.ImageUrl ?? null!;
        // изображение необязательно: при чисто текстовых правках передаётся null FileUploadRequest,
        // поэтому проверяем перед обращением к Data, иначе типовой путь обновления без изображения даст NRE.
        if (image?.Data != null)
        {
            var imageString = await storageService.UploadAsync<AppUser>(image, FileType.Image, cancellationToken);
            user.ImageUrl = new Uri(imageString, UriKind.RelativeOrAbsolute);
            if (deleteCurrentImage && imageUri != null)
            {
                await storageService.RemoveAsync(imageUri.ToString(), cancellationToken);
            }
        }
        else if (deleteCurrentImage && imageUri != null)
        {
            await storageService.RemoveAsync(imageUri.ToString(), cancellationToken);
            user.ImageUrl = null;
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        string? currentPhoneNumber = await userManager.GetPhoneNumberAsync(user);
        if (phoneNumber != currentPhoneNumber)
        {
            await userManager.SetPhoneNumberAsync(user, phoneNumber);
        }

        var result = await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);

        if (!result.Succeeded)
        {
            throw new CustomException("Не удалось обновить профиль");
        }
    }

    public async Task SetImageUrlAsync(string userId, string? imageUrl, CancellationToken cancellationToken)
    {
        EnsureValidTenant();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("пользователь не найден");

        user.ImageUrl = string.IsNullOrWhiteSpace(imageUrl)
            ? null
            : new Uri(imageUrl, UriKind.RelativeOrAbsolute);

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new CustomException("Не удалось обновить изображение профиля");
        }

        await signInManager.RefreshSignInAsync(user);
    }

    public async Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null, CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();
        return await userManager.FindByEmailAsync(email.Normalize()) is AppUser user && user.Id != exceptId;
    }

    public async Task<bool> ExistsWithNameAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();
        return await userManager.FindByNameAsync(name) is not null;
    }

    public async Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null, CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();
        return await userManager.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken) is AppUser user && user.Id != exceptId;
    }

    private void EnsureValidTenant()
    {
        if (string.IsNullOrWhiteSpace(multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id))
        {
            throw new UnauthorizedException("недействительный арендатор");
        }
    }

    private string? ResolveImageUrl(Uri? imageUrl)
    {
        if (imageUrl is null)
        {
            return null;
        }

        // Абсолютные URL (например, S3) проходят без изменений.
        if (imageUrl.IsAbsoluteUri)
        {
            return imageUrl.ToString();
        }

        // Для относительных путей из локального хранилища добавляем префикс origin API и wwwroot.
        if (_originUrl is null)
        {
            var request = httpContextAccessor.HttpContext?.Request;
            if (request is not null && !string.IsNullOrWhiteSpace(request.Scheme) && request.Host.HasValue)
            {
                var baseUri = $"{request.Scheme}://{request.Host.Value}{request.PathBase}";
                var relativePath = imageUrl.ToString().TrimStart('/');
                return $"{baseUri.TrimEnd('/')}/{relativePath}";
            }

            return imageUrl.ToString();
        }

        var originRelativePath = imageUrl.ToString().TrimStart('/');
        return $"{_originUrl.AbsoluteUri.TrimEnd('/')}/{originRelativePath}";
    }
}