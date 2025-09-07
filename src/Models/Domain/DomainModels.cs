using System.Windows.Controls;

namespace AnalysisNorm.Models.Domain;


/// <summary>
/// Основная сущность нормы расхода электроэнергии
/// Неизменяемый объект (record) для безопасности в многопоточной среде
/// </summary>
public record Norm(
    string Id,
    string Type,
    IReadOnlyList<DataPoint> Points,
    NormMetadata Metadata)
{
    /// <summary>
    /// Валидация нормы при создании
    /// </summary>
    public Norm
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Type);
        ArgumentNullException.ThrowIfNull(Points);
        ArgumentNullException.ThrowIfNull(Metadata);
        
        if (Points.Count == 0)
            throw new ArgumentException("Norm must have at least one data point", nameof(Points));
    }

    /// <summary>
    /// Диапазон значений X (нагрузка)
    /// </summary>
    public (decimal Min, decimal Max) LoadRange => Points.Count > 0
        ? (Points.Min(p => p.X), Points.Max(p => p.X))
        : (0, 0);

    /// <summary>
    /// Диапазон значений Y (расход)
    /// </summary>
    public (decimal Min, decimal Max) ConsumptionRange => Points.Count > 0
        ? (Points.Min(p => p.Y), Points.Max(p => p.Y))
        : (0, 0);
}

/// <summary>
/// Точка данных нормы (X - нагрузка, Y - расход)
/// Структура для оптимизации памяти
/// </summary>
public readonly record struct DataPoint(decimal X, decimal Y)
{
    /// <summary>
    /// Валидация точки данных
    /// </summary>
    public DataPoint
    {
        if (X <= 0)
            throw new ArgumentException("X coordinate must be positive", nameof(X));
        if (Y <= 0)
            throw new ArgumentException("Y coordinate must be positive", nameof(Y));
    }

    /// <summary>
    /// Преобразование в строку для отладки
    /// </summary>
    public override string ToString() => $"({X:F2}, {Y:F2})";
}

/// <summary>
/// Метаданные нормы
/// </summary>
public record NormMetadata(
    DateTime CreatedAt,
    string Source,
    string Version,
    string? Description = null,
    Dictionary<string, object>? AdditionalData = null);

/// <summary>
/// Маршрут с полной информацией
/// </summary>
public record Route(
    string Number,
    DateTime Date,
    string Depot,
    Locomotive Locomotive,
    IReadOnlyList<Section> Sections,
    RouteMetadata Metadata)
{
    public Route
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Number);
        ArgumentException.ThrowIfNullOrWhiteSpace(Depot);
        ArgumentNullException.ThrowIfNull(Locomotive);
        ArgumentNullException.ThrowIfNull(Sections);
        ArgumentNullException.ThrowIfNull(Metadata);
    }

    /// <summary>
    /// Общий расход по всем участкам
    /// </summary>
    public decimal TotalActualConsumption => Sections.Sum(s => s.ActualConsumption);

    /// <summary>
    /// Общий нормативный расход
    /// </summary>
    public decimal TotalNormConsumption => Sections.Sum(s => s.NormConsumption);

    /// <summary>
    /// Общее отклонение маршрута в процентах
    /// </summary>
    public decimal TotalDeviationPercent => TotalNormConsumption != 0
        ? ((TotalActualConsumption - TotalNormConsumption) / TotalNormConsumption) * 100
        : 0;
}

/// <summary>
/// Участок маршрута
/// </summary>
public record Section(
    string Name,
    decimal TkmBrutto,
    decimal Distance,
    decimal ActualConsumption,
    decimal NormConsumption,
    string? NormId = null,
    SectionMetadata? Metadata = null)
{
    public Section
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        
        if (TkmBrutto< 0) throw new ArgumentException("TkmBrutto cannot be negative", nameof(TkmBrutto));
        if (Distance< 0) throw new ArgumentException("Distance cannot be negative", nameof(Distance));
        if (ActualConsumption< 0) throw new ArgumentException("ActualConsumption cannot be negative", nameof(ActualConsumption));
        if (NormConsumption< 0) throw new ArgumentException("NormConsumption cannot be negative", nameof(NormConsumption));
}

}

/// <summary>
/// ОБНОВЛЕННЫЙ ProcessingResult для CHAT 2 - заменяет существующий простой ProcessingResult
/// Поддерживает как простые сообщения об ошибках, так и детализированные ошибки
/// </summary>
public record ProcessingResult<T>
{
    public T? Data { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<ProcessingError>? Errors { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;

    // Простые методы (совместимые с EnhancedHtmlParser)
    public static ProcessingResult<T> Success(T data) => new()
    {
        Data = data,
        IsSuccess = true
    };

    public static ProcessingResult<T> Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };

    // Расширенные методы (для будущих чатов)
    public static ProcessingResult<T> Failure(params ProcessingError[] errors) => new()
    {
        IsSuccess = false,
        Errors = errors?.ToArray() ?? Array.Empty<ProcessingError>(),
        ErrorMessage = errors?.FirstOrDefault()?.Message
    };
}

/// <summary>
/// ДОБАВИТЬ: Недостающие типы для совместимости
/// </summary>
public record ProcessingError(
    ErrorSeverity Severity,
    string Message,
    string? Context = null,
    Exception? Exception = null)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string ShortDescription => Context != null ? $"{Context}: {Message}" : Message;
}

public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Результат обработки для совместимости с EnhancedHtmlParser
/// </summary>
public record ProcessingStatistics
{
    public int TotalItems { get; init; }
    public int ProcessedItems { get; init; }
    public int SkippedItems { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public long MemoryUsageBytes { get; init; }
}