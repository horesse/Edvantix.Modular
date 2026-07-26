namespace EDV.Framework.Shared.Storage;

/// <summary>
/// Представляет запрос на загрузку файла с именем файла, типом содержимого и данными.
/// </summary>
public sealed class FileUploadRequest
{
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public List<byte> Data { get; set; } = [];
}