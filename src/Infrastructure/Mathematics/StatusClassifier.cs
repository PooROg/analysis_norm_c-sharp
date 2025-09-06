// Infrastructure/Mathematics/StatusClassifier.cs
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Infrastructure.Mathematics;

/// <summary>
/// Классификатор статусов отклонений норм
/// Определяет критичность отклонения на основе процентного значения
/// </summary>
public static class StatusClassifier
{
    // Константы для границ отклонений (соответствуют Python версии)
    private const decimal ExcellentThreshold = 5m;
    private const decimal GoodThreshold = 10m;
    private const decimal AcceptableThreshold = 20m;
    private const decimal PoorThreshold = 30m;

    /// <summary>
    /// Классифицирует отклонение по процентному значению
    /// </summary>
    /// <param name="deviationPercent">Процент отклонения (может быть отрицательным)</param>
    /// <returns>Статус отклонения</returns>
    public static DeviationStatus ClassifyDeviation(decimal deviationPercent)
    {
        var absDeviation = Math.Abs(deviationPercent);
        
        return absDeviation switch
        {
            <= ExcellentThreshold => DeviationStatus.Excellent,
            <= GoodThreshold => DeviationStatus.Good,
            <= AcceptableThreshold => DeviationStatus.Acceptable,
            <= PoorThreshold => DeviationStatus.Poor,
            _ => DeviationStatus.Critical
        };
    }

    /// <summary>
    /// Получает цвет для статуса (для UI)
    /// </summary>
    /// <param name="status">Статус отклонения</param>
    /// <returns>Название цвета для отображения</returns>
    public static string GetStatusColor(DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.Excellent => "#2E7D32",      // Темно-зеленый
            DeviationStatus.Good => "#4CAF50",           // Зеленый
            DeviationStatus.Acceptable => "#FF9800",     // Оранжевый
            DeviationStatus.Poor => "#F44336",           // Красный
            DeviationStatus.Critical => "#B71C1C",       // Темно-красный
            _ => "#9E9E9E"                               // Серый
        };
    }

    /// <summary>
    /// Получает описание статуса на русском языке
    /// </summary>
    /// <param name="status">Статус отклонения</param>
    /// <returns>Текстовое описание</returns>
    public static string GetStatusDescription(DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.Excellent => "Отлично",
            DeviationStatus.Good => "Хорошо",
            DeviationStatus.Acceptable => "Приемлемо",
            DeviationStatus.Poor => "Плохо",
            DeviationStatus.Critical => "Критично",
            _ => "Неизвестно"
        };
    }

    /// <summary>
    /// Определяет, требует ли статус корректирующих действий
    /// </summary>
    /// <param name="status">Статус отклонения</param>
    /// <returns>True, если требуются действия</returns>
    public static bool RequiresCorrectiveAction(DeviationStatus status)
    {
        return status is DeviationStatus.Poor or DeviationStatus.Critical;
    }

    /// <summary>
    /// Получает иконку для статуса
    /// </summary>
    /// <param name="status">Статус отклонения</param>
    /// <returns>Unicode символ иконки</returns>
    public static string GetStatusIcon(DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.Excellent => "✅",
            DeviationStatus.Good => "✅",
            DeviationStatus.Acceptable => "⚠️",
            DeviationStatus.Poor => "❌",
            DeviationStatus.Critical => "🔴",
            _ => "❔"
        };
    }

    /// <summary>
    /// Классифицирует множество отклонений и возвращает сводную статистику
    /// </summary>
    /// <param name="deviations">Коллекция процентных отклонений</param>
    /// <returns>Сводная статистика классификации</returns>
    public static DeviationStatistics ClassifyMultiple(IEnumerable<decimal> deviations)
    {
        var statusCounts = new Dictionary<DeviationStatus, int>
        {
            { DeviationStatus.Excellent, 0 },
            { DeviationStatus.Good, 0 },
            { DeviationStatus.Acceptable, 0 },
            { DeviationStatus.Poor, 0 },
            { DeviationStatus.Critical, 0 }
        };

        var totalCount = 0;
        var worstStatus = DeviationStatus.Excellent;

        foreach (var deviation in deviations)
        {
            var status = ClassifyDeviation(deviation);
            statusCounts[status]++;
            totalCount++;

            if (status > worstStatus)
                worstStatus = status;
        }

        var criticalPercentage = totalCount > 0 
            ? (decimal)(statusCounts[DeviationStatus.Critical] + statusCounts[DeviationStatus.Poor]) / totalCount * 100 
            : 0;

        return new DeviationStatistics
        {
            StatusCounts = statusCounts,
            TotalCount = totalCount,
            WorstStatus = worstStatus,
            CriticalPercentage = criticalPercentage,
            RequiresAttention = criticalPercentage > 10 // Более 10% критичных отклонений
        };
    }
}

/// <summary>
/// Статистика классификации отклонений
/// </summary>
public record DeviationStatistics
{
    public Dictionary<DeviationStatus, int> StatusCounts { get; init; } = new();
    public int TotalCount { get; init; }
    public DeviationStatus WorstStatus { get; init; }
    public decimal CriticalPercentage { get; init; }
    public bool RequiresAttention { get; init; }
}