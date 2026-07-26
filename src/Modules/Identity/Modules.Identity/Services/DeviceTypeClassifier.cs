namespace EDV.Modules.Identity.Services;

/// <summary>
/// Классифицирует типы устройств на основе строк семейства устройств user agent.
/// Вынесено из SessionService для снижения цикломатической сложности.
/// </summary>
public static class DeviceTypeClassifier
{
    private const string Desktop = "Desktop";
    private const string Mobile = "Mobile";
    private const string Tablet = "Tablet";

    private static readonly string[] MobileKeywords = ["mobile", "phone", "iphone", "android"];
    private static readonly string[] TabletKeywords = ["tablet", "ipad"];

    /// <summary>
    /// Определяет тип устройства по строке семейства устройств user agent.
    /// </summary>
    /// <param name="deviceFamily">Строка семейства устройств из разбора user agent.</param>
    /// <returns>Тип устройства: "Desktop", "Mobile" или "Tablet".</returns>
    public static string Classify(string? deviceFamily)
    {
        if (string.IsNullOrWhiteSpace(deviceFamily) || deviceFamily == "Other")
        {
            return Desktop;
        }

        var normalized = deviceFamily.ToLowerInvariant();

        if (MobileKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
        {
            return Mobile;
        }

        if (TabletKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
        {
            return Tablet;
        }

        return Desktop;
    }
}