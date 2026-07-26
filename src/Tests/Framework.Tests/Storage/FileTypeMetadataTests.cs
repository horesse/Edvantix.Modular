using EDV.Framework.Storage;

namespace Framework.Tests.Storage;

public sealed class FileTypeMetadataTests
{
    #region Основной сценарий

    [Fact]
    public void GetRules_Should_ReturnImageRules_When_ImageRequested()
    {
        // Действие
        var rules = FileTypeMetadata.GetRules(FileType.Image);

        // Проверка
        rules.MaxSizeInMB.ShouldBe(5);
        rules.AllowedExtensions.ShouldContain(".png");
        rules.AllowedExtensions.ShouldContain(".jpg");
        rules.AllowedExtensions.ShouldContain(".jpeg");
        rules.AllowedExtensions.ShouldContain(".ico");
    }

    [Fact]
    public void GetRules_Should_ReturnPdfRules_When_PdfRequested()
    {
        // Действие
        var rules = FileTypeMetadata.GetRules(FileType.Pdf);

        // Проверка
        rules.MaxSizeInMB.ShouldBe(10);
        rules.AllowedExtensions.ShouldHaveSingleItem();
        rules.AllowedExtensions.ShouldContain(".pdf");
    }

    #endregion

    #region Граничные случаи

    [Fact]
    public void GetRules_Should_Throw_When_TypeUnsupported()
    {
        // У FileType.Document нет сопоставления, поэтому он попадает в ветку по умолчанию.
        Should.Throw<NotSupportedException>(() => FileTypeMetadata.GetRules(FileType.Document));
    }

    [Fact]
    public void FileValidationRules_Should_DefaultToFiveMb_When_NotSet()
    {
        // Подготовка и действие
        var rules = new FileValidationRules();

        // Проверка
        rules.MaxSizeInMB.ShouldBe(5);
        rules.AllowedExtensions.ShouldBeEmpty();
    }

    #endregion
}
