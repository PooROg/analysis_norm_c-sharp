// Configuration/ConfigurationModels.cs
namespace AnalysisNorm.Configuration;

/// <summary>
/// Конфигурация экспорта в Excel
/// </summary>
public record ExcelExportConfiguration
{
    public string DefaultOutputPath { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Documents), "AnalysisNorm");
    public int MaxRowsPerSheet { get; init; } = 1000000;
    public bool AutoOpenAfterExport { get; init; } = true;
    public bool IncludeTimestampInFileName { get; init; } = true;
    public string DefaultFileNamePattern { get; init; } = "analysis_norm_{date:yyyyMMdd}_{time:HHmm}.xlsx";
    public bool EnableDataValidation { get; init; } = true;
    public bool CreateBackupCopy { get; init; } = false;
    public ExcelFormattingSettings Formatting { get; init; } = new();

    public static ExcelExportConfiguration Default => new();
}

/// <summary>
/// Настройки форматирования Excel
/// </summary>
public record ExcelFormattingSettings
{
    public bool EnableConditionalFormatting { get; init; } = true;
    public bool FreezeHeaderRow { get; init; } = true;
    public bool AutoFitColumns { get; init; } = false;
    public bool AlternateRowColors { get; init; } = true;
    public string HeaderFontName { get; init; } = "Arial";
    public int HeaderFontSize { get; init; } = 12;
    public string DataFontName { get; init; } = "Arial";
    public int DataFontSize { get; init; } = 10;
    public decimal DefaultRowHeight { get; init; } = 20;
}

/// <summary>
/// Конфигурация парсинга HTML
/// </summary>
public record HtmlParsingConfiguration
{
    public int ParsingTimeoutMs { get; init; } = 30000; // 30 секунд
    public int MaxFileSizeMB { get; init; } = 50;
    public int MaxConcurrentFiles { get; init; } = 4;
    public bool EnableCaching { get; init; } = true;
    public int CacheExpirationMinutes { get; init; } = 60;
    public bool StrictValidation { get; init; } = false;
    public bool PreserveOriginalData { get; init; } = true;
    public bool EnableDetailedLogging { get; init; } = false;
    public HtmlCleanupSettings Cleanup { get; init; } = new();
    public DeduplicationSettings Deduplication { get; init; } = new();

    public static HtmlParsingConfiguration Default => new();
}

/// <summary>
/// Настройки очистки HTML
/// </summary>
public record HtmlCleanupSettings
{
    public bool RemoveScriptTags { get; init; } = true;
    public bool RemoveStyleTags { get; init; } = true;
    public bool RemoveComments { get; init; } = true;
    public bool NormalizeWhitespace { get; init; } = true;
    public bool RemoveVchtRoutes { get; init; } = true;
    public List<string> CustomRemovalPatterns { get; init; } = new();
}

/// <summary>
/// Настройки дедупликации
/// </summary>
public record DeduplicationSettings
{
    public bool EnableAdvancedDeduplication { get; init; } = true;
    public bool PreserveDuplicateCount { get; init; } = true;
    public DuplicateSelectionStrategy SelectionStrategy { get; init; } = DuplicateSelectionStrategy.MostComplete;
    public bool AnalyzeDuplicateQuality { get; init; } = true;
    public decimal TolerancePercent { get; init; } = 5.0m;
}

/// <summary>
/// Стратегии выбора лучшего дубликата
/// </summary>
public enum DuplicateSelectionStrategy
{
    MostComplete,      // Наиболее полный (больше участков)
    HighestConsumption, // Наибольшее потребление
    MostRecent,        // Самый поздний по времени
    FirstFound         // Первый найденный
}

/// <summary>
/// Конфигурация производительности
/// </summary>
public record PerformanceConfiguration
{
    public int MaxMemoryUsageMB { get; init; } = 200;
    public int MaxProcessingTimeSeconds { get; init; } = 15;
    public int MaxUIResponseTimeMs { get; init; } = 100;
    public bool EnablePerformanceMonitoring { get; init; } = true;
    public bool AutoOptimizeMemory { get; init; } = true;
    public int GCCollectionThreshold { get; init; } = 50; // MB
    public int MaxCacheSize { get; init; } = 1000;
    public int ThreadPoolSize { get; init; } = Environment.ProcessorCount;
    public ProcessingLimits Limits { get; init; } = new();

    public static PerformanceConfiguration Default => new();
}

/// <summary>
/// Лимиты обработки
/// </summary>
public record ProcessingLimits
{
    public int MaxFilesPerOperation { get; init; } = 100;
    public int MaxRoutesPerFile { get; init; } = 10000;
    public int MaxSectionsPerRoute { get; init; } = 1000;
    public int MaxConcurrentOperations { get; init; } = 2;
    public TimeSpan MaxOperationDuration { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Конфигурация диагностики
/// </summary>
public record DiagnosticsConfiguration
{
    public string LogLevel { get; init; } = "Information";
    public bool EnableFileLogging { get; init; } = true;
    public bool EnableConsoleLogging { get; init; } = true;
    public bool EnablePerformanceLogging { get; init; } = true;
    public string LogFilePath { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnalysisNorm", "Logs");
    public int MaxLogFileSizeMB { get; init; } = 10;
    public int MaxLogFileCount { get; init; } = 10;
    public bool EnableDetailedExceptions { get; init; } = true;
    public bool EnableUserActivityLogging { get; init; } = false;
    public DiagnosticFeatures Features { get; init; } = new();

    public static DiagnosticsConfiguration Default => new();
}

/// <summary>
/// Диагностические функции
/// </summary>
public record DiagnosticFeatures
{
    public bool EnableHealthChecks { get; init; } = true;
    public bool EnableMetricsCollection { get; init; } = true;
    public bool EnableErrorReporting { get; init; } = true;
    public bool EnablePerformanceAlerts { get; init; } = true;
    public int HealthCheckIntervalSeconds { get; init; } = 30;
    public bool AutoExportDiagnostics { get; init; } = false;
    public string DiagnosticExportPath { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Documents), "AnalysisNorm", "Diagnostics");
}

/// <summary>
/// Конфигурация UI
/// </summary>
public record UIConfiguration
{
    public string Theme { get; init; } = "Light";
    public string Language { get; init; } = "ru-RU";
    public bool EnableAnimations { get; init; } = true;
    public bool ShowTooltips { get; init; } = true;
    public bool AutoSaveWorkspace { get; init; } = true;
    public int AutoSaveIntervalMinutes { get; init; } = 5;
    public WindowSettings MainWindow { get; init; } = new();
    public GridSettings DataGrid { get; init; } = new();

    public static UIConfiguration Default => new();
}

/// <summary>
/// Настройки окна приложения
/// </summary>
public record WindowSettings
{
    public int DefaultWidth { get; init; } = 1400;
    public int DefaultHeight { get; init; } = 900;
    public bool RememberPosition { get; init; } = true;
    public bool RememberSize { get; init; } = true;
    public bool StartMaximized { get; init; } = false;
    public bool MinimizeToTray { get; init; } = false;
}

/// <summary>
/// Настройки таблицы данных
/// </summary>
public record GridSettings
{
    public bool EnableVirtualization { get; init; } = true;
    public int PageSize { get; init; } = 1000;
    public bool EnableSorting { get; init; } = true;
    public bool EnableFiltering { get; init; } = true;
    public bool EnableGrouping { get; init; } = false;
    public bool ShowRowNumbers { get; init; } = true;
    public bool AlternateRowColors { get; init; } = true;
}

/// <summary>
/// Базовый интерфейс для всех конфигураций
/// </summary>
public interface IConfiguration
{
    /// <summary>
    /// Валидация конфигурации
    /// </summary>
    bool IsValid();
    
    /// <summary>
    /// Получение описания ошибок валидации
    /// </summary>
    IEnumerable<string> GetValidationErrors();
}

/// <summary>
/// Базовая реализация конфигурации с валидацией
/// </summary>
public abstract record BaseConfiguration : IConfiguration
{
    public virtual bool IsValid()
    {
        return !GetValidationErrors().Any();
    }

    public virtual IEnumerable<string> GetValidationErrors()
    {
        var errors = new List<string>();
        
        // Базовая валидация через рефлексию
        var properties = GetType().GetProperties();
        foreach (var property in properties)
        {
            var value = property.GetValue(this);
            
            // Проверка строковых свойств
            if (property.PropertyType == typeof(string) && value is string stringValue)
            {
                if (property.Name.Contains("Path") && string.IsNullOrWhiteSpace(stringValue))
                {
                    errors.Add($"{property.Name} не может быть пустым");
                }
            }
            
            // Проверка числовых свойств
            if (property.PropertyType == typeof(int) && value is int intValue)
            {
                if (property.Name.Contains("Max") && intValue <= 0)
                {
                    errors.Add($"{property.Name} должно быть больше 0");
                }
            }
        }
        
        return errors;
    }
}

/// <summary>
/// Расширенная конфигурация экспорта с валидацией
/// </summary>
public record ValidatedExcelExportConfiguration : ExcelExportConfiguration, IConfiguration
{
    public bool IsValid()
    {
        return !GetValidationErrors().Any();
    }

    public IEnumerable<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DefaultOutputPath))
            errors.Add("DefaultOutputPath не может быть пустым");

        if (MaxRowsPerSheet <= 0)
            errors.Add("MaxRowsPerSheet должно быть больше 0");

        if (!Directory.Exists(Path.GetDirectoryName(DefaultOutputPath)))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DefaultOutputPath)!);
            }
            catch
            {
                errors.Add($"Невозможно создать директорию: {Path.GetDirectoryName(DefaultOutputPath)}");
            }
        }

        return errors;
    }
}

/// <summary>
/// Фабрика конфигураций с предустановленными профилями
/// </summary>
public static class ConfigurationProfiles
{
    /// <summary>
    /// Профиль для разработки - подробное логирование, отключена оптимизация
    /// </summary>
    public static class Development
    {
        public static DiagnosticsConfiguration Diagnostics => new()
        {
            LogLevel = "Debug",
            EnableDetailedExceptions = true,
            EnableUserActivityLogging = true,
            Features = new DiagnosticFeatures
            {
                EnableHealthChecks = true,
                EnableMetricsCollection = true,
                AutoExportDiagnostics = true
            }
        };

        public static PerformanceConfiguration Performance => new()
        {
            EnablePerformanceMonitoring = true,
            AutoOptimizeMemory = false,
            MaxMemoryUsageMB = 500 // Больше памяти для разработки
        };
    }

    /// <summary>
    /// Продакшн профиль - оптимизированный для производительности
    /// </summary>
    public static class Production
    {
        public static DiagnosticsConfiguration Diagnostics => new()
        {
            LogLevel = "Warning",
            EnableDetailedExceptions = false,
            EnableUserActivityLogging = false
        };

        public static PerformanceConfiguration Performance => new()
        {
            EnablePerformanceMonitoring = true,
            AutoOptimizeMemory = true,
            MaxMemoryUsageMB = 200
        };

        public static HtmlParsingConfiguration HtmlParsing => new()
        {
            EnableDetailedLogging = false,
            StrictValidation = true,
            MaxConcurrentFiles = Environment.ProcessorCount
        };
    }

    /// <summary>
    /// Тестовый профиль - для юнит-тестов
    /// </summary>
    public static class Testing
    {
        public static DiagnosticsConfiguration Diagnostics => new()
        {
            LogLevel = "Error",
            EnableFileLogging = false,
            EnableConsoleLogging = false
        };

        public static PerformanceConfiguration Performance => new()
        {
            MaxMemoryUsageMB = 100,
            MaxProcessingTimeSeconds = 5
        };
    }
}
