// Services/Interfaces/IAdvancedHtmlParser.cs
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Расширенный интерфейс HTML парсера для CHAT 3
/// Наследует базовый IHtmlParser и добавляет продвинутые функции парсинга
/// </summary>
public interface IAdvancedHtmlParser : IHtmlParser
{
    /// <summary>
    /// Расширенный парсинг с извлечением ТУ3/Ю7 данных
    /// </summary>
    Task<ProcessingResult<AdvancedParsingResult>> ParseWithAdvancedDataAsync(
        string htmlContent, 
        string? fileName = null);

    /// <summary>
    /// Пакетная обработка множества HTML файлов
    /// </summary>
    Task<ProcessingResult<BatchParsingResult>> ParseMultipleFilesAsync(
        IEnumerable<string> filePaths,
        ParsingOptions? options = null);

    /// <summary>
    /// Валидация HTML файла перед парсингом
    /// </summary>
    Task<HtmlValidationResult> ValidateHtmlAsync(string htmlContent);

    /// <summary>
    /// Получение статистики парсинга
    /// </summary>
    ParsingStatistics GetParsingStatistics();

    /// <summary>
    /// Извлечение метаданных из HTML без полного парсинга
    /// </summary>
    Task<ProcessingResult<HtmlMetadata>> ExtractMetadataAsync(string htmlContent);

    /// <summary>
    /// Парсинг с настраиваемыми правилами извлечения
    /// </summary>
    Task<ProcessingResult<IEnumerable<Route>>> ParseWithCustomRulesAsync(
        string htmlContent,
        Dictionary<string, string> customRules);
}

/// <summary>
/// Результат расширенного парсинга с ТУ3/Ю7 данными
/// </summary>
public record AdvancedParsingResult
{
    public IEnumerable<Route> Routes { get; init; } = Enumerable.Empty<Route>();
    public Tu3Data? Tu3Data { get; init; }
    public List<Yu7Record> Yu7Records { get; init; } = new();
    public DuplicationAnalysis DuplicationAnalysis { get; init; } = new();
    public List<SectionMergeAnalysis> MergeAnalyses { get; init; } = new();
    public ParsingStatistics Statistics { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public HtmlMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Результат пакетной обработки файлов
/// </summary>
public record BatchParsingResult
{
    public int TotalFilesProcessed { get; init; }
    public int SuccessfullyProcessed { get; init; }
    public int FailedToProcess { get; init; }
    public List<Route> AllRoutes { get; init; } = new();
    public TimeSpan TotalProcessingTime { get; init; }
    public List<FileProcessingResult> FileResults { get; init; } = new();
    public BatchParsingStatistics Statistics { get; init; } = new();
}

/// <summary>
/// Параметры парсинга
/// </summary>
public record ParsingOptions
{
    public bool EnableAdvancedDeduplication { get; init; } = true;
    public bool EnableSectionMerging { get; init; } = true;
    public bool StrictValidation { get; init; } = false;
    public bool PreserveOriginalData { get; init; } = true;
    public int MaxConcurrentFiles { get; init; } = 4;
    public TimeSpan TimeoutPerFile { get; init; } = TimeSpan.FromMinutes(2);
    public bool EnableDetailedLogging { get; init; } = false;
    public Dictionary<string, object> CustomSettings { get; init; } = new();
}

/// <summary>
/// Результат валидации HTML
/// </summary>
public record HtmlValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public HtmlStructureInfo StructureInfo { get; init; } = new();
    public double QualityScore { get; init; } // 0-100
}

/// <summary>
/// Информация о структуре HTML
/// </summary>
public record HtmlStructureInfo
{
    public int TotalTables { get; init; }
    public int RouteTables { get; init; }
    public int NormTables { get; init; }
    public long FileSizeBytes { get; init; }
    public bool HasTu3Data { get; init; }
    public bool HasYu7Data { get; init; }
    public int EstimatedRoutes { get; init; }
    public List<string> DetectedSections { get; init; } = new();
}

/// <summary>
/// Статистика парсинга
/// </summary>
public record ParsingStatistics
{
    public int TotalFilesProcessed { get; init; }
    public int TotalRoutesExtracted { get; init; }
    public int TotalDuplicatesFound { get; init; }
    public int TotalSectionsMerged { get; init; }
    public TimeSpan TotalProcessingTime { get; init; }
    public long TotalDataProcessed { get; init; }
    public DateTime LastProcessingTime { get; init; }

    public decimal AverageProcessingSpeed => TotalProcessingTime.TotalSeconds > 0 
        ? TotalRoutesExtracted / (decimal)TotalProcessingTime.TotalSeconds 
        : 0;
}

/// <summary>
/// Метаданные HTML файла
/// </summary>
public record HtmlMetadata
{
    public string Title { get; init; } = string.Empty;
    public DateTime? CreationDate { get; init; }
    public string Encoding { get; init; } = string.Empty;
    public List<string> DetectedTables { get; init; } = new();
    public Dictionary<string, string> CustomAttributes { get; init; } = new();
}

/// <summary>
/// Результат обработки одного файла
/// </summary>
public record FileProcessingResult
{
    public string FileName { get; init; } = string.Empty;
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public int RoutesExtracted { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public long FileSizeBytes { get; init; }
    public HtmlValidationResult ValidationResult { get; init; } = new();
}

/// <summary>
/// Статистика пакетной обработки
/// </summary>
public record BatchParsingStatistics
{
    public decimal AverageProcessingTimePerFile { get; init; }
    public decimal AverageRoutesPerFile { get; init; }
    public long TotalDataProcessed { get; init; }
    public decimal SuccessRate { get; init; }
    public string FastestFile { get; init; } = string.Empty;
    public string SlowestFile { get; init; } = string.Empty;
}

/// <summary>
/// ТУ3 данные
/// </summary>
public record Tu3Data
{
    public string SeriesNumber { get; init; } = string.Empty;
    public decimal ConsumptionRate { get; init; }
    public string Conditions { get; init; } = string.Empty;
    public DateTime ValidFrom { get; init; }
    public Dictionary<string, object> AdditionalData { get; init; } = new();
}

/// <summary>
/// Ю7 запись
/// </summary>
public record Yu7Record
{
    public string RecordId { get; init; } = string.Empty;
    public string RouteNumber { get; init; } = string.Empty;
    public decimal Distance { get; init; }
    public decimal Mass { get; init; }
    public string Notes { get; init; } = string.Empty;
    public DateTime RecordDate { get; init; }
}