// Infrastructure/Mathematics/StatusClassifier.cs
namespace AnalysisNorm.Infrastructure.Mathematics;

/// <summary>
/// Классификатор статусов отклонений - точная копия Python StatusClassifier
/// Static class для упрощения архитектуры и совместимости с существующим проектом
/// </summary>
public static class StatusClassifier
{
    /// <summary>
    /// Пороговые значения отклонений в процентах (из Python core/utils.py)
    /// </summary>
    public static class Thresholds
    {
        public const decimal ExcellentMax = 5.0m;      // До 5% - отлично
        public const decimal GoodMax = 10.0m;          // До 10% - хорошо
        public const decimal AcceptableMax = 15.0m;    // До 15% - приемлемо
        public const decimal PoorMax = 25.0m;          // До 25% - плохо
        // Свыше 25% - критично
    }

    /// <summary>
    /// Классифицирует статус отклонения по процентному значению
    /// Точная копия Python алгоритма из StatusClassifier.classify_deviation
    /// </summary>
    public static DeviationStatus ClassifyDeviation(decimal deviationPercent)
    {
        var absDeviation = Math.Abs(deviationPercent);

        return absDeviation switch
        {
            <= Thresholds.ExcellentMax => DeviationStatus.Excellent,
            <= Thresholds.GoodMax => DeviationStatus.Good,
            <= Thresholds.AcceptableMax => DeviationStatus.Acceptable,
            <= Thresholds.PoorMax => DeviationStatus.Poor,
            _ => DeviationStatus.Critical
        };
    }

    /// <summary>
    /// Получает цвет для статуса (для UI и Excel экспорта)
    /// Соответствует Python visualizations.py цветовой схеме
    /// </summary>
    public static string GetStatusColor(DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.Excellent => "#4CAF50",    // Зеленый
            DeviationStatus.Good => "#8BC34A",         // Светло-зеленый
            DeviationStatus.Acceptable => "#FFC107",   // Желтый
            DeviationStatus.Poor => "#FF9800",         // Оранжевый
            DeviationStatus.Critical => "#F44336",     // Красный
            _ => "#9E9E9E"                             // Серый (неопределено)
        };
    }

    /// <summary>
    /// Получает описание статуса на русском языке
    /// </summary>
    public static string GetStatusDescription(DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.Excellent => "Отлично",
            DeviationStatus.Good => "Хорошо",
            DeviationStatus.Acceptable => "Приемлемо",
            DeviationStatus.Poor => "Плохо",
            DeviationStatus.Critical => "Критично",
            _ => "Не определено"
        };
    }

    /// <summary>
    /// Проверяет, является ли статус приемлемым для эксплуатации
    /// </summary>
    public static bool IsAcceptableForOperation(DeviationStatus status)
    {
        return status <= DeviationStatus.Acceptable;
    }

    /// <summary>
    /// Проверяет, требуется ли корректирующее действие
    /// </summary>
    public static bool RequiresCorrectiveAction(DeviationStatus status)
    {
        return status >= DeviationStatus.Poor;
    }

    /// <summary>
    /// Вычисляет статистику распределения статусов
    /// </summary>
    public static StatusDistribution CalculateStatusDistribution(IEnumerable<decimal> deviations)
    {
        var statusCounts = new Dictionary<DeviationStatus, int>();
        var totalCount = 0;

        foreach (var deviation in deviations)
        {
            var status = ClassifyDeviation(deviation);
            statusCounts[status] = statusCounts.GetValueOrDefault(status) + 1;
            totalCount++;
        }

        return new StatusDistribution
        {
            TotalCount = totalCount,
            ExcellentCount = statusCounts.GetValueOrDefault(DeviationStatus.Excellent),
            GoodCount = statusCounts.GetValueOrDefault(DeviationStatus.Good),
            AcceptableCount = statusCounts.GetValueOrDefault(DeviationStatus.Acceptable),
            PoorCount = statusCounts.GetValueOrDefault(DeviationStatus.Poor),
            CriticalCount = statusCounts.GetValueOrDefault(DeviationStatus.Critical)
        };
    }
}

/// <summary>
/// Статусы отклонений от нормы
/// </summary>
public enum DeviationStatus
{
    Excellent = 0,   // До 5%
    Good = 1,        // До 10%
    Acceptable = 2,  // До 15%
    Poor = 3,        // До 25%
    Critical = 4     // Свыше 25%
}

/// <summary>
/// Статистика распределения статусов - совместима с существующими типами
/// </summary>
public record StatusDistribution
{
    public int TotalCount { get; init; }
    public int ExcellentCount { get; init; }
    public int GoodCount { get; init; }
    public int AcceptableCount { get; init; }
    public int PoorCount { get; init; }
    public int CriticalCount { get; init; }

    /// <summary>
    /// Процент приемлемых результатов (отлично + хорошо + приемлемо)
    /// </summary>
    public decimal AcceptablePercentage => TotalCount > 0
        ? (decimal)(ExcellentCount + GoodCount + AcceptableCount) / TotalCount * 100
        : 0;

    /// <summary>
    /// Процент проблемных результатов (плохо + критично)
    /// </summary>
    public decimal ProblematicPercentage => TotalCount > 0
        ? (decimal)(PoorCount + CriticalCount) / TotalCount * 100
        : 0;
}