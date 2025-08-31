using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Статус отклонения расхода от нормы
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeviationStatus
{
    /// <summary>
    /// Неизвестно / не определено
    /// </summary>
    [Description("Не определено")]
    Unknown = 0,

    /// <summary>
    /// Сильная экономия (больше -10%)
    /// </summary>
    [Description("Сильная экономия")]
    StrongEconomy = 1,

    /// <summary>
    /// Средняя экономия (-5% до -10%)
    /// </summary>
    [Description("Средняя экономия")]
    MediumEconomy = 2,

    /// <summary>
    /// Слабая экономия (-2% до -5%)
    /// </summary>
    [Description("Слабая экономия")]
    WeakEconomy = 3,

    /// <summary>
    /// В пределах нормы (-2% до +2%)
    /// </summary>
    [Description("Норма")]
    Normal = 4,

    /// <summary>
    /// Слабый перерасход (2% до 5%)
    /// </summary>
    [Description("Слабый перерасход")]
    WeakOverrun = 5,

    /// <summary>
    /// Средний перерасход (5% до 10%)
    /// </summary>
    [Description("Средний перерасход")]
    MediumOverrun = 6,

    /// <summary>
    /// Сильный перерасход (больше 10%)
    /// </summary>
    [Description("Сильный перерасход")]
    StrongOverrun = 7
}

/// <summary>
/// Вспомогательный класс для работы с DeviationStatus
/// ИСПРАВЛЕНО: убраны некорректные операторы преобразования из статического класса
/// </summary>
public static class DeviationStatusHelper
{
    /// <summary>
    /// Определяет статус отклонения на основе процентного значения
    /// ИСПРАВЛЕНО: добавлен недостающий метод GetStatus
    /// </summary>
    /// <param name="deviationPercentage">Процентное отклонение</param>
    /// <returns>Статус отклонения</returns>
    public static DeviationStatus GetStatus(decimal deviationPercentage)
    {
        return deviationPercentage switch
        {
            <= -10m => DeviationStatus.StrongEconomy,
            <= -5m => DeviationStatus.MediumEconomy,
            <= -2m => DeviationStatus.WeakEconomy,
            <= 2m => DeviationStatus.Normal,
            <= 5m => DeviationStatus.WeakOverrun,
            <= 10m => DeviationStatus.MediumOverrun,
            _ => DeviationStatus.StrongOverrun
        };
    }

    /// <summary>
    /// Получает описание статуса
    /// </summary>
    public static string GetDescription(DeviationStatus status)
    {
        var field = status.GetType().GetField(status.ToString());
        var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .FirstOrDefault() as DescriptionAttribute;

        return attribute?.Description ?? status.ToString();
    }

    /// <summary>
    /// Получает цветовое представление статуса для UI
    /// </summary>
    public static string GetColor(DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.StrongEconomy => "#1B5E20", // Темно-зеленый
            DeviationStatus.MediumEconomy => "#2E7D32", // Зеленый
            DeviationStatus.WeakEconomy => "#388E3C", // Светло-зеленый
            DeviationStatus.Normal => "#1976D2", // Синий
            DeviationStatus.WeakOverrun => "#F57C00", // Оранжевый
            DeviationStatus.MediumOverrun => "#E64A19", // Красно-оранжевый
            DeviationStatus.StrongOverrun => "#D32F2F", // Красный
            _ => "#9E9E9E" // Серый для Unknown
        };
    }

    /// <summary>
    /// Получает символьное представление статуса
    /// </summary>
    public static string GetSymbol(DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.StrongEconomy => "⬇⬇⬇",
            DeviationStatus.MediumEconomy => "⬇⬇",
            DeviationStatus.WeakEconomy => "⬇",
            DeviationStatus.Normal => "=",
            DeviationStatus.WeakOverrun => "⬆",
            DeviationStatus.MediumOverrun => "⬆⬆",
            DeviationStatus.StrongOverrun => "⬆⬆⬆",
            _ => "?"
        };
    }

    /// <summary>
    /// Проверяет является ли статус экономией
    /// </summary>
    public static bool IsEconomy(DeviationStatus status)
    {
        return status is DeviationStatus.StrongEconomy
            or DeviationStatus.MediumEconomy
            or DeviationStatus.WeakEconomy;
    }

    /// <summary>
    /// Проверяет является ли статус перерасходом
    /// </summary>
    public static bool IsOverrun(DeviationStatus status)
    {
        return status is DeviationStatus.WeakOverrun
            or DeviationStatus.MediumOverrun
            or DeviationStatus.StrongOverrun;
    }

    /// <summary>
    /// Проверяет является ли статус нормой
    /// </summary>
    public static bool IsNormal(DeviationStatus status)
    {
        return status == DeviationStatus.Normal;
    }

    /// <summary>
    /// Преобразует статус в строку для экспорта
    /// ИСПРАВЛЕНО: добавлен метод для безопасного преобразования в строку
    /// </summary>
    public static string ToString(DeviationStatus status)
    {
        return GetDescription(status);
    }

    /// <summary>
    /// Парсит строку в статус отклонения
    /// </summary>
    public static DeviationStatus Parse(string? statusString)
    {
        if (string.IsNullOrWhiteSpace(statusString))
            return DeviationStatus.Unknown;

        // Пробуем парсить по названию enum
        if (Enum.TryParse<DeviationStatus>(statusString, true, out var status))
            return status;

        // Пробуем парсить по описанию
        foreach (DeviationStatus enumStatus in Enum.GetValues<DeviationStatus>())
        {
            if (GetDescription(enumStatus).Equals(statusString, StringComparison.OrdinalIgnoreCase))
                return enumStatus;
        }

        return DeviationStatus.Unknown;
    }

    /// <summary>
    /// Получает все доступные статусы с описаниями
    /// </summary>
    public static Dictionary<DeviationStatus, string> GetAllStatusesWithDescriptions()
    {
        return Enum.GetValues<DeviationStatus>()
            .ToDictionary(status => status, GetDescription);
    }
}

/// <summary>
/// Расширения для DeviationStatus
/// </summary>
public static class DeviationStatusExtensions
{
    /// <summary>
    /// Получает описание статуса
    /// </summary>
    public static string GetDescription(this DeviationStatus status)
    {
        return DeviationStatusHelper.GetDescription(status);
    }

    /// <summary>
    /// Получает цвет статуса
    /// </summary>
    public static string GetColor(this DeviationStatus status)
    {
        return DeviationStatusHelper.GetColor(status);
    }

    /// <summary>
    /// Получает символ статуса
    /// </summary>
    public static string GetSymbol(this DeviationStatus status)
    {
        return DeviationStatusHelper.GetSymbol(status);
    }

    /// <summary>
    /// Проверяет является ли статус экономией
    /// </summary>
    public static bool IsEconomy(this DeviationStatus status)
    {
        return DeviationStatusHelper.IsEconomy(status);
    }

    /// <summary>
    /// Проверяет является ли статус перерасходом
    /// </summary>
    public static bool IsOverrun(this DeviationStatus status)
    {
        return DeviationStatusHelper.IsOverrun(status);
    }

    /// <summary>
    /// Проверяет является ли статус нормой
    /// </summary>
    public static bool IsNormal(this DeviationStatus status)
    {
        return DeviationStatusHelper.IsNormal(status);
    }
}