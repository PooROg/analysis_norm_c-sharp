// Configuration/ConfigurationModels.cs
namespace AnalysisNorm.Configuration;

/// <summary>
/// Конфигурация Excel экспорта
/// </summary>
public class ExcelExportConfiguration
{
    public string DefaultOutputPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Documents);
    public int MaxRowsPerSheet { get; set; } = 1000000;
    public bool AutoFitColumns { get; set; } = true;
    public bool FreezeHeaders { get; set; } = true;
    public bool ApplyConditionalFormatting { get; set; } = true;
    public string DateFormat { get; set; } = "dd.MM.yyyy";
    public string NumberFormat { get; set; } = "#,##0.00";
    public bool IncludeDiagnostics { get; set; } = true;
    public Dictionary<string, object> CustomFormats { get; set; } = new();
}

/// <summary>
/// Конфигурация HTML парсинга
/// </summary>
public class HtmlParsingConfiguration
{
    public int ParsingTimeoutMs { get; set; } = 120000; // 2 минуты
    public bool EnableStrictValidation { get; set; } = false;
    public bool EnableAdvancedDeduplication { get; set; } = true;
    public bool EnableSectionMerging { get; set; } = true;
    public int MaxConcurrentFiles { get; set; } = 4;
    public bool PreserveOriginalData { get; set; } = true;
    public Dictionary<string, string> CustomSelectors { get; set; } = new();
    public List<string> IgnoredSections { get; set; } = new();
}

/// <summary>
/// Конфигурация производительности
/// </summary>
public class PerformanceConfiguration
{
    public int MaxMemoryUsageMB { get; set; } = 200;
    public int MaxProcessingTimeSeconds { get; set; } = 15;
    public int MaxUIResponseTimeMs { get; set; } = 100;
    public bool EnablePerformanceLogging { get; set; } = true;
    public int PerformanceLogIntervalMs { get; set; } = 5000;
    public bool EnableMemoryOptimization { get; set; } = true;
    public int GCCollectionThresholdMB { get; set; } = 150;
}

/// <summary>
/// Конфигурация диагностики
/// </summary>
public class DiagnosticsConfiguration
{
    public bool EnableHealthChecks { get; set; } = true;
    public bool EnableMetricsCollection { get; set; } = true;
    public bool EnableErrorReporting { get; set; } = true;
    public bool EnablePerformanceAlerts { get; set; } = true;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public bool AutoExportDiagnostics { get; set; } = false;
    public string DiagnosticExportPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public Dictionary<string, bool> ComponentHealthChecks { get; set; } = new();
}

/// <summary>
/// Конфигурация UI
/// </summary>
public class UIConfiguration
{
    public string Theme { get; set; } = "Light";
    public string Language { get; set; } = "ru-RU";
    public bool EnableAnimations { get; set; } = true;
    public bool ShowTooltips { get; set; } = true;
    public bool AutoSaveWorkspace { get; set; } = true;
    public int AutoSaveIntervalMinutes { get; set; } = 5;
    public bool RememberWindowPosition { get; set; } = true;
    public bool RememberWindowSize { get; set; } = true;
    public bool StartMaximized { get; set; } = false;
    public bool MinimizeToTray { get; set; } = false;
    public int DefaultGridPageSize { get; set; } = 1000;
    public bool EnableVirtualization { get; set; } = true;
    public Dictionary<string, object> CustomUISettings { get; set; } = new();
}

/// <summary>
/// Конфигурация логирования
/// </summary>
public class LoggingConfiguration
{
    public string LogLevel { get; set; } = "Information";
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;
    public string LogFilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "AnalysisNorm", "Logs");
    public int MaxLogFileSizeMB { get; set; } = 10;
    public int MaxLogFileCount { get; set; } = 10;
    public bool EnableDetailedExceptions { get; set; } = true;
    public bool EnableUserActivityLogging { get; set; } = false;
}