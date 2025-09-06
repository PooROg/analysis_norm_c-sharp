// Services/Interfaces/IAdvancedExcelExporter.cs
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Расширенный интерфейс экспорта в Excel для CHAT 4
/// Наследует базовый IExcelExporter и добавляет продвинутые функции
/// Решает конфликт с существующим интерфейсом через наследование
/// </summary>
public interface IAdvancedExcelExporter : IExcelExporter
{
    /// <summary>
    /// Основной экспорт маршрутов в Excel с полным форматированием
    /// </summary>
    Task<ProcessingResult<string>> ExportRoutesToExcelAsync(
        IEnumerable<Route> routes, 
        string outputPath, 
        ExportOptions? options = null);

    /// <summary>
    /// Экспорт диагностических данных в отдельный файл
    /// </summary>
    Task<ProcessingResult<string>> ExportDiagnosticDataAsync(
        DuplicationAnalysis duplicationAnalysis,
        List<SectionMergeAnalysis> mergeAnalyses,
        string outputPath);

    /// <summary>
    /// Экспорт результатов анализа участков
    /// </summary>
    Task<ProcessingResult<string>> ExportAnalysisResultsAsync(
        Dictionary<string, AnalysisResult> analysisResults,
        string outputPath,
        ExportOptions? options = null);

    /// <summary>
    /// Быстрый экспорт без форматирования (для больших объемов данных)
    /// </summary>
    Task<ProcessingResult<string>> ExportRoutesSimpleAsync(
        IEnumerable<Route> routes,
        string outputPath);
}

/// <summary>
/// Опции экспорта Excel
/// </summary>
public record ExportOptions
{
    public bool IncludeFormatting { get; init; } = true;
    public bool IncludeDiagnostics { get; init; } = false;
    public bool AutoFitColumns { get; init; } = true;
    public bool FreezeHeaders { get; init; } = true;
    public bool ApplyConditionalFormatting { get; init; } = true;
    public int MaxRowsPerSheet { get; init; } = 1000000;
    public string? CustomTemplate { get; init; }
}

/// <summary>
/// Анализ дублирования для экспорта
/// </summary>
public record DuplicationAnalysis
{
    public int TotalRoutes { get; init; }
    public int UniqueRoutes { get; init; }
    public int DuplicateRoutes { get; init; }
    public decimal DuplicationPercentage { get; init; }
    public List<DuplicateGroup> DuplicateGroups { get; init; } = new();
    public DateTime AnalysisTime { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Группа дублированных маршрутов
/// </summary>
public record DuplicateGroup
{
    public string GroupKey { get; init; } = string.Empty;
    public List<string> RouteIds { get; init; } = new();
    public int Count { get; init; }
    public string Resolution { get; init; } = string.Empty;
}

/// <summary>
/// Анализ объединения участков
/// </summary>
public record SectionMergeAnalysis
{
    public string SectionName { get; init; } = string.Empty;
    public int OriginalCount { get; init; }
    public int MergedCount { get; init; }
    public decimal ReductionPercentage { get; init; }
    public List<string> MergeReasons { get; init; } = new();
    public DateTime MergeTime { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Результат анализа участка
/// </summary>
public record AnalysisResult
{
    public string SectionName { get; init; } = string.Empty;
    public int TotalRoutes { get; init; }
    public decimal AverageDeviation { get; init; }
    public DeviationStatus OverallStatus { get; init; }
    public Dictionary<DeviationStatus, int> StatusDistribution { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
    public DateTime AnalysisTime { get; init; } = DateTime.UtcNow;
}