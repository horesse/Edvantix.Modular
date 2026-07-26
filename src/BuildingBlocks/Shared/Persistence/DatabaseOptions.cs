using System.ComponentModel.DataAnnotations;

namespace EDV.Framework.Shared.Persistence;

/// <summary>
/// Параметры конфигурации для выбора провайдера базы данных и информации о подключении.
/// </summary>
public sealed class DatabaseOptions : IValidatableObject
{
    /// <summary>
    /// Используемый провайдер базы данных. Допустимые значения: <see cref="DbProviders.PostgreSQL"/>.
    /// По умолчанию — PostgreSQL.
    /// </summary>
    public string Provider { get; set; } = DbProviders.PostgreSQL;

    /// <summary>
    /// Строка подключения, используемая DbContext-ами EF Core и связанными сервисами.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Сборка, содержащая миграции EF Core для выбранного провайдера.
    /// </summary>
    public string MigrationsAssembly { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            yield return new ValidationResult("Строка подключения не может быть пустой.", new[] { nameof(ConnectionString) });
        }
    }
}