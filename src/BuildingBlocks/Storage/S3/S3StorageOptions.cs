namespace EDV.Framework.Storage.S3;

public sealed class S3StorageOptions
{
    public string? Bucket { get; set; }
    public string? Region { get; set; }
    public string? Prefix { get; set; }
    public bool PublicRead { get; set; } = true;
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Пользовательский URL эндпоинта S3. Укажите его, чтобы обращаться к MinIO или любому другому
    /// S3-совместимому сервису (например, "http://localhost:9000"). Оставьте пустым для AWS S3.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Явный ключ доступа. Если <see cref="AccessKey"/> или <see cref="SecretKey"/> пусты,
    /// вместо них используется стандартная цепочка учётных данных AWS SDK.
    /// </summary>
    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>
    /// Требуется для MinIO и большинства не-AWS S3-совместимых сервисов (они не поддерживают
    /// виртуально-хостовый стиль поддоменов). Игнорируется, если <see cref="ServiceUrl"/> пуст.
    /// </summary>
    public bool ForcePathStyle { get; set; }
}
