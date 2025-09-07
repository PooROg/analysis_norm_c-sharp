// Models/Domain/ExtendedDomainModels.cs
using AnalysisNorm.Infrastructure.Mathematics;

namespace AnalysisNorm.Models.Domain;

/// <summary>
/// Результат анализа участка - копия Python AnalysisResult
/// </summary>
public record AnalysisResult
{
    public string SectionName { get; init; } = string.Empty;
    public string? SpecificNormId { get; init; }
    public bool SingleSectionOnly { get; init; }
    public int TotalRoutes { get; init; }
    public IReadOnlyList<AnalysisItem> AnalysisItems { get; init; } = Array.Empty<AnalysisItem>();
    public AnalysisStatistics Statistics { get; init; } = new();
    public TimeSpan ProcessingTime { get; init; }
    public DateTime AnalysisDate { get; init; }

    /// <summary>
    /// Создает пустой результат для случаев ошибок
    /// </summary>
    public static AnalysisResult Empty(string sectionName) => new()
    {
        SectionName = sectionName,
        AnalysisDate = DateTime.UtcNow
    };

    /// <summary>
    /// Проверяет, содержит ли результат данные
    /// </summary>
    public bool HasData => AnalysisItems.Any();

    /// <summary>
    /// Процент приемлемых результатов
    /// </summary>
    public decimal AcceptablePercentage => Statistics.StatusDistribution.AcceptablePercentage;
}

/// <summary>
/// Элемент анализа - отдельное сравнение участка маршрута с нормой
/// </summary>
public record AnalysisItem
{
    public string RouteNumber { get; init; } = string.Empty;
    public DateTime RouteDate { get; init; }
    public string SectionName { get; init; } = string.Empty;
    public string NormId { get; init; } = string.Empty;
    public decimal TkmBrutto { get; init; }
    public decimal Distance { get; init; }
    public decimal ActualConsumption { get; init; }
    public decimal NormConsumption { get; init; }
    public decimal DeviationPercent { get; init; }
    public DeviationStatus DeviationStatus { get; init; }

    /// <summary>
    /// Цвет для отображения статуса
    /// </summary>
    public string StatusColor => StatusClassifier.GetStatusColor(DeviationStatus);

    /// <summary>
    /// Описание статуса
    /// </summary>
    public string StatusDescription => StatusClassifier.GetStatusDescription(DeviationStatus);

    /// <summary>
    /// Абсолютное отклонение
    /// </summary>
    public decimal AbsoluteDeviation => Math.Abs(ActualConsumption - NormConsumption);

    /// <summary>
    /// Требует ли корректирующих действий
    /// </summary>
    public bool RequiresAttention => StatusClassifier.RequiresCorrectiveAction(DeviationStatus);
}

/// <summary>
/// Статистика анализа
/// </summary>
public record AnalysisStatistics
{
    public int TotalItems { get; init; }
    public decimal MeanDeviation { get; init; }
    public decimal StandardDeviation { get; init; }
    public decimal MinDeviation { get; init; }
    public decimal MaxDeviation { get; init; }
    public StatusDistribution StatusDistribution { get; init; } = new();

    /// <summary>
    /// Коэффициент вариации (относительное стандартное отклонение)
    /// </summary>
    public decimal CoefficientOfVariation => MeanDeviation != 0 
        ? Math.Abs(StandardDeviation / MeanDeviation) * 100 
        : 0;

    /// <summary>
    /// Качественная оценка стабильности
    /// </summary>
    public string StabilityAssessment => CoefficientOfVariation switch
    {
        < 10 => "Высокая стабильность",
        < 20 => "Умеренная стабильность", 
        < 30 => "Низкая стабильность",
        _ => "Крайне нестабильно"
    };
}

/// <summary>
/// Статистика обработки файлов
/// </summary>
public record ProcessingStatistics
{
    public int TotalFilesProcessed { get; init; }
    public int TotalRoutesLoaded { get; init; }
    public int TotalNormsLoaded { get; init; }
    public int TotalErrors { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public DateTime LastProcessingDate { get; init; }

    /// <summary>
    /// Скорость обработки (маршрутов в секунду)
    /// </summary>
    public decimal ProcessingRate => ProcessingTime.TotalSeconds > 0 
        ? (decimal)(TotalRoutesLoaded / ProcessingTime.TotalSeconds) 
        : 0;

    /// <summary>
    /// Процент успешности обработки
    /// </summary>
    public decimal SuccessRate => TotalFilesProcessed > 0 
        ? (decimal)((TotalFilesProcessed - TotalErrors) / TotalFilesProcessed) * 100 
        : 0;
}

/// <summary>
/// Информация о локомотиве - расширенная версия
/// </summary>
public record Locomotive(
    string Model,
    string Number,
    LocomotiveMetadata? Metadata)
{
    public Locomotive
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Model);
        ArgumentException.ThrowIfNullOrWhiteSpace(Number);
    }

    /// <summary>
    /// Полное обозначение локомотива
    /// </summary>
    public string FullDesignation => $"{Model}-{Number}";

    /// <summary>
    /// Тип локомотива по модели
    /// </summary>
    public LocomotiveType Type => Model switch
    {
        var m when m.StartsWith("ЭП20") => LocomotiveType.EP20,
        var m when m.StartsWith("2ЭС5К") => LocomotiveType.ES5K_Double,
        var m when m.StartsWith("ЭС5К") => LocomotiveType.ES5K,
        var m when m.StartsWith("ВЛ80С") => LocomotiveType.VL80S,
        _ => LocomotiveType.Unknown
    };
}

/// <summary>
/// Типы локомотивов
/// </summary>
public enum LocomotiveType
{
    Unknown,
    EP20,        // ЭП20
    ES5K,        // ЭС5К
    ES5K_Double, // 2ЭС5К
    VL80S        // ВЛ80С
}

/// <summary>
/// Метаданные локомотива
/// </summary>
public record LocomotiveMetadata(
    decimal? MaxPower = null,
    decimal? Weight = null,
    string? Depot = null,
    DateTime? CommissioningDate = null);

/// <summary>
/// Метаданные нормы
/// </summary>
public record NormMetadata(
    string? Description = null,
    DateTime? CreatedDate = null,
    string? Source = null,
    string? Version = null,
    Dictionary<string, object>? AdditionalData = null);

/// <summary>
/// Метаданные участка
/// </summary>
public record SectionMetadata(
    string? Region = null,
    decimal? Elevation = null,
    string? TrackType = null,
    string? Electrification = null,
    Dictionary<string, object>? AdditionalData = null);

/// <summary>
/// Результат обработки с типизированными ошибками
/// </summary>
public record ProcessingResult<T>
{
    public T? Data { get; init; }
    public bool IsSuccess { get; init; }
    public IReadOnlyList<ProcessingError> Errors { get; init; } = Array.Empty<ProcessingError>();
    public ProcessingStatistics? Statistics { get; init; }

    public static ProcessingResult<T> Success(T data, ProcessingStatistics? stats = null) => new()
    {
        Data = data,
        IsSuccess = true,
        Statistics = stats
    };

    public static ProcessingResult<T> Failure(params ProcessingError[] errors) => new()
    {
        IsSuccess = false,
        Errors = errors
    };

    public static ProcessingResult<T> Failure(IEnumerable<ProcessingError> errors) => new()
    {
        IsSuccess = false,
        Errors = errors.ToArray()
    };
}

/// <summary>
/// Ошибка обработки
/// </summary>
public record ProcessingError(
    ErrorSeverity Severity,
    string Message,
    string? Context = null,
    Exception? Exception = null)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Краткое описание для логов
    /// </summary>
    public string ShortDescription => Context != null ? $"{Context}: {Message}" : Message;
}

/// <summary>
/// Уровни серьезности ошибок
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Информация о хранилище норм
/// </summary>
public record NormStorageInfo(
    int TotalNorms,
    int CachedFunctions,
    long MemoryUsageBytes,
    DateTime LastUpdated);

/// <summary>
/// Конфигурация анализа
/// </summary>
public record AnalysisConfiguration
{
    public decimal DeviationTolerancePercent { get; init; } = 5.0m;
    public bool EnableParallelProcessing { get; init; } = true;
    public int MaxConcurrentOperations { get; init; } = Environment.ProcessorCount;
    public TimeSpan CacheExpirationTime { get; init; } = TimeSpan.FromHours(1);
    public bool EnableDetailedLogging { get; init; } = true;
}