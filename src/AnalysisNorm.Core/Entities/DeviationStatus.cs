// === AnalysisNorm.Core/Entities/DeviationStatus.cs ===
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices; // ДОБАВЛЕНО: для MethodImpl

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Единый enum для статусов отклонений расхода от нормы
/// Соответствует StatusClassifier из Python utils.py
/// Заменяет все дублированные static классы DeviationStatus в проекте
/// </summary>
public enum DeviationStatus : byte // byte для экономии памяти - всего 7 значений
{
    [Display(Name = "Экономия сильная")]
    EconomyStrong = 0,

    [Display(Name = "Экономия средняя")]
    EconomyMedium = 1,

    [Display(Name = "Экономия слабая")]
    EconomyWeak = 2,

    [Display(Name = "Норма")]
    Normal = 3,

    [Display(Name = "Перерасход слабый")]
    OverrunWeak = 4,

    [Display(Name = "Перерасход средний")]
    OverrunMedium = 5,

    [Display(Name = "Перерасход сильный")]
    OverrunStrong = 6
}

/// <summary>
/// Высокопроизводительные методы для работы со статусами отклонений
/// ИСПРАВЛЕНО: Добавлен статический метод GetStatus
/// Консолидирует функциональность из всех дублированных DeviationStatus static классов
/// Оптимизирован для минимального allocation и максимальной скорости
/// </summary>
public static class DeviationStatusHelper
{
    #region Константы порогов (из Python StatusClassifier)

    private const decimal StrongEconomyThreshold = -30m;
    private const decimal MediumEconomyThreshold = -20m;
    private const decimal WeakEconomyThreshold = -5m;
    private const decimal NormalUpperThreshold = 5m;
    private const decimal WeakOverrunThreshold = 10m;
    private const decimal MediumOverrunThreshold = 20m;

    #endregion

    #region Static Classification Methods - ИСПРАВЛЕНО

    /// <summary>
    /// Классифицирует отклонение по статусу
    /// ИСПРАВЛЕНО: Добавлен недостающий статический метод GetStatus
    /// Соответствует Python StatusClassifier.get_status
    /// </summary>
    /// <param name="deviationPercent">Отклонение в процентах</param>
    /// <returns>Статус отклонения</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DeviationStatus GetStatus(decimal deviationPercent)
    {
        return deviationPercent switch
        {
            <= StrongEconomyThreshold => DeviationStatus.EconomyStrong,
            <= MediumEconomyThreshold => DeviationStatus.EconomyMedium,
            <= WeakEconomyThreshold => DeviationStatus.EconomyWeak,
            <= NormalUpperThreshold => DeviationStatus.Normal,
            <= WeakOverrunThreshold => DeviationStatus.OverrunWeak,
            <= MediumOverrunThreshold => DeviationStatus.OverrunMedium,
            _ => DeviationStatus.OverrunStrong
        };
    }

    /// <summary>
    /// Получает статус как строку для отображения
    /// ИСПРАВЛЕНО: Для правильного преобразования DeviationStatus в string
    /// </summary>
    public static string GetStatusText(DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.EconomyStrong => "Экономия сильная",
            DeviationStatus.EconomyMedium => "Экономия средняя",
            DeviationStatus.EconomyWeak => "Экономия слабая",
            DeviationStatus.Normal => "Норма",
            DeviationStatus.OverrunWeak => "Перерасход слабый",
            DeviationStatus.OverrunMedium => "Перерасход средний",
            DeviationStatus.OverrunStrong => "Перерасход сильный",
            _ => "Неопределено"
        };
    }

    /// <summary>
    /// Неявное преобразование DeviationStatus в string для совместимости
    /// ИСПРАВЛЕНО: Решает проблемы CS1503 с преобразованием типов
    /// </summary>
    public static implicit operator string(DeviationStatus status)
    {
        return GetStatusText(status);
    }

    #endregion

    #region Color Mapping для UI

    /// <summary>
    /// Получает цвет для статуса отклонения
    /// Thread-safe массив предвычисленных цветов для производительности
    /// </summary>
    private static readonly string[] StatusColors =
    {
        "darkgreen",    // EconomyStrong
        "green",        // EconomyMedium  
        "lightgreen",   // EconomyWeak
        "blue",         // Normal
        "orange",       // OverrunWeak
        "darkorange",   // OverrunMedium
        "red"           // OverrunStrong
    };

    /// <summary>
    /// Возвращает цвет для отображения статуса
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetStatusColor(DeviationStatus status)
    {
        var index = (int)status;
        return index < StatusColors.Length ? StatusColors[index] : "gray";
    }

    #endregion

    #region Константы для внешнего использования

    /// <summary>
    /// Публичные константы порогов для использования в других частях системы
    /// </summary>
    public static class Thresholds
    {
        public const decimal StrongEconomy = StrongEconomyThreshold;
        public const decimal MediumEconomy = MediumEconomyThreshold;
        public const decimal WeakEconomy = WeakEconomyThreshold;
        public const decimal NormalUpper = NormalUpperThreshold;
        public const decimal WeakOverrun = WeakOverrunThreshold;
        public const decimal MediumOverrun = MediumOverrunThreshold;
    }

    #endregion

    #region Batch операции для производительности

    /// <summary>
    /// Обрабатывает массив отклонений за один проход
    /// Оптимизировано для bulk операций в аналитических отчетах
    /// </summary>
    public static DeviationStatus[] GetStatusesBatch(ReadOnlySpan<decimal> deviations)
    {
        var results = new DeviationStatus[deviations.Length];

        for (int i = 0; i < deviations.Length; i++)
        {
            results[i] = GetStatus(deviations[i]);
        }

        return results;
    }

    /// <summary>
    /// Подсчитывает статистику по статусам для dashboard
    /// Возвращает количество каждого типа отклонения
    /// </summary>
    public static Dictionary<DeviationStatus, int> GetStatusStatistics(IEnumerable<DeviationStatus> statuses)
    {
        var stats = new Dictionary<DeviationStatus, int>();

        // Инициализируем все статусы нулями
        foreach (DeviationStatus status in Enum.GetValues<DeviationStatus>())
        {
            stats[status] = 0;
        }

        // Подсчитываем
        foreach (var status in statuses)
        {
            stats[status]++;
        }

        return stats;
    }

    #endregion
}