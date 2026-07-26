using EDV.Framework.Shared.Storage;
using EDV.Framework.Storage;
using FluentValidation;

namespace EDV.Modules.Identity.Features.v1.Users;

public sealed class UserImageValidator : AbstractValidator<FileUploadRequest>
{
    public UserImageValidator() : this(FileType.Image) { }
    public UserImageValidator(FileType fileType)
    {
        var rules = FileTypeMetadata.GetRules(fileType);

        RuleFor(x => x.FileName)
            .NotEmpty()
            .Must(file => rules.AllowedExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .WithMessage($"Разрешены только следующие расширения: {string.Join(", ", rules.AllowedExtensions)}");

        RuleFor(x => x.Data)
            .NotEmpty()
            .Must(data => data.Count <= rules.MaxSizeInMB * 1024 * 1024)
            .WithMessage($"Размер файла должен быть не более {rules.MaxSizeInMB} МБ.");
    }
}