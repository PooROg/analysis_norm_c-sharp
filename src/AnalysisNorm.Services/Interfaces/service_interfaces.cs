using AnalysisNorm.Core.Entities;

namespace AnalysisNorm.Services.Interfaces;

// === HTML PROCESSING SERVICES ===

/// <summary>
/// Сервис обработки HTML файлов маршрутов
/// Соответствует HTMLRouteProcessor из Python
/// </summary>
public interface IHtmlRouteProcessorService
{
    /// <summary>
    /// Обрабатывает список HTML файлов маршрутов
    /// </summary>
    Task<ProcessingResult<IEnumerable<Route>>> ProcessHtmlFilesAsync(
        IEnumerable<string> htmlFiles, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Получает статистику обработки
    /// </summary>
    ProcessingStatistics GetProcessingStatistics();
}

/// <summary>
/// Сервис обработки HTML файлов норм
/// Соответствует HTMLNormProcessor из Python
/// </summary>
public interface IHtmlNormProcessorService
{
    /// <summary>
    /// Обрабатывает список HTML файлов норм
    /// </summary>
    Task<ProcessingResult<IEnumerable<Norm>>> ProcessHtmlFilesAsync(
        IEnumerable<string> htmlFiles,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Получает статистику обработки норм
    /// </summary>
    ProcessingStatistics GetProcessingStatistics();
}

// === DATA ANALYSIS SERVICES ===

/// <summary>
/// Основной сервис анализа данных
/// Соответствует InteractiveNormsAnalyzer из Python
/// </summary>
public interface IDataAnalysisService
{
    /// <summary>
    /// Анализирует участок с построением результатов для визуализации
    /// </summary>
    Task<AnalysisResult> AnalyzeSectionAsync(
        string sectionName,
        string? normId = null,
        bool singleSectionOnly = false,
        AnalysisOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Получает список участков
    /// </summary>
    Task<IEnumerable<string>> GetSectionsListAsync();
    
    /// <summary>
    /// Получает нормы для участка с количествами маршрутов
    /// </summary>
    Task<IEnumerable<NormWithCount>> GetNormsWithCountsForSectionAsync(
        string sectionName, 
        bool singleSectionOnly = false);
}

/// <summary>
/// Сервис интерполяции норм
/// Соответствует функциональности norm_storage.py
/// </summary>
public interface INormInterpolationService
{
    /// <summary>
    /// Интерполирует значение нормы для заданной нагрузки
    /// </summary>
    Task<decimal?> InterpolateNormValueAsync(string normId, decimal loadValue);
    
    /// <summary>
    /// Создает функцию интерполяции для нормы
    /// </summary>
    Task<InterpolationFunction?> CreateInterpolationFunctionAsync(string normId);
    
    /// <summary>
    /// Валидирует нормы в хранилище
    /// </summary>
    Task<ValidationResults> ValidateNormsAsync();
}

// === LOCOMOTIVE SERVICES ===

/// <summary>
/// Сервис управления коэффициентами локомотивов
/// Соответствует LocomotiveCoefficientsManager из Python
/// </summary>
public interface ILocomotiveCoefficientService
{
    /// <summary>
    /// Загружает коэффициенты из Excel файла
    /// </summary>
    Task<bool> LoadCoefficientsAsync(string filePath, double minWorkThreshold = 0.0);
    
    /// <summary>
    /// Получает коэффициент для локомотива
    /// </summary>
    Task<decimal> GetCoefficientAsync(string series, int number);
    
    /// <summary>
    /// Получает статистику коэффициентов
    /// </summary>
    Task<CoefficientStatistics> GetStatisticsAsync();
    
    /// <summary>
    /// Применяет коэффициенты к маршрутам
    /// </summary>
    Task ApplyCoefficientsAsync(IEnumerable<Route> routes);
}

/// <summary>
/// Сервис фильтрации локомотивов
/// Соответствует LocomotiveFilter из Python
/// </summary>
public interface ILocomotiveFilterService
{
    /// <summary>
    /// Создает фильтр на основе маршрутов
    /// </summary>
    LocomotiveFilter CreateFilter(IEnumerable<Route> routes);
    
    /// <summary>
    /// Фильтрует маршруты по выбранным локомотивам
    /// </summary>
    IEnumerable<Route> FilterRoutes(IEnumerable<Route> routes, LocomotiveFilter filter);
    
    /// <summary>
    /// Получает локомотивы сгруппированные по сериям
    /// </summary>
    Task<Dictionary<string, List<int>>> GetLocomotivesBySeriesAsync();
}

// === STORAGE AND CACHING SERVICES ===

/// <summary>
/// Сервис хранения норм с кэшированием
/// Соответствует NormStorage из Python с улучшениями
/// </summary>
public interface INormStorageService
{
    /// <summary>
    /// Добавляет или обновляет нормы
    /// </summary>
    Task<Dictionary<string, string>> AddOrUpdateNormsAsync(IEnumerable<Norm> norms);
    
    /// <summary>
    /// Получает норму по ID
    /// </summary>
    Task<Norm?> GetNormAsync(string normId);
    
    /// <summary>
    /// Получает все нормы
    /// </summary>
    Task<IEnumerable<Norm>> GetAllNormsAsync();
    
    /// <summary>
    /// Получает информацию о хранилище
    /// </summary>
    Task<StorageInfo> GetStorageInfoAsync();
}

/// <summary>
/// Сервис кэширования результатов анализа
/// </summary>
public interface IAnalysisCacheService
{
    /// <summary>
    /// Получает результат анализа из кэша
    /// </summary>
    Task<AnalysisResult?> GetCachedAnalysisAsync(string analysisHash);
    
    /// <summary>
    /// Сохраняет результат анализа в кэш
    /// </summary>
    Task SaveAnalysisToCacheAsync(AnalysisResult analysisResult);
    
    /// <summary>
    /// Очищает устаревший кэш
    /// </summary>
    Task CleanupOldCacheAsync(TimeSpan maxAge);
}

// === EXPORT SERVICES ===

/// <summary>
/// Сервис экспорта в Excel
/// Соответствует export функциональности Python
/// </summary>
public interface IExcelExportService
{
    /// <summary>
    /// Экспортирует маршруты в Excel с форматированием
    /// </summary>
    Task<bool> ExportRoutesToExcelAsync(
        IEnumerable<Route> routes, 
        string outputPath,
        ExportOptions? options = null);
    
    /// <summary>
    /// Экспортирует результаты анализа в Excel
    /// </summary>
    Task<bool> ExportAnalysisToExcelAsync(
        AnalysisResult analysisResult, 
        string outputPath);
}

// === VISUALIZATION SERVICES ===

/// <summary>
/// Сервис подготовки данных для визуализации
/// Подготавливает данные для OxyPlot (аналог visualization.py)
/// </summary>
public interface IVisualizationDataService
{
    /// <summary>
    /// Подготавливает данные для интерактивного графика
    /// </summary>
    Task<VisualizationData> PrepareInteractiveChartDataAsync(
        string sectionName,
        IEnumerable<Route> routes,
        Dictionary<string, InterpolationFunction> normFunctions,
        string? specificNormId = null);
    
    /// <summary>
    /// Создает данные для графика отклонений
    /// </summary>
    ChartData CreateDeviationChartData(IEnumerable<Route> routes);
}

// === UTILITY SERVICES ===

/// <summary>
/// Детектор кодировки файлов
/// </summary>
public interface IFileEncodingDetector
{
    /// <summary>
    /// Определяет кодировку файла
    /// </summary>
    Task<string> DetectEncodingAsync(string filePath);
    
    /// <summary>
    /// Читает файл с автоматическим определением кодировки
    /// </summary>
    Task<string> ReadTextWithEncodingDetectionAsync(string filePath);
}

/// <summary>
/// Нормализатор текста
/// Соответствует normalize_text из Python utils.py
/// </summary>
public interface ITextNormalizer
{
    /// <summary>
    /// Нормализует текст (убирает лишние пробелы, nbsp и т.д.)
    /// </summary>
    string NormalizeText(string text);
    
    /// <summary>
    /// Безопасно конвертирует в decimal
    /// </summary>
    decimal SafeDecimal(object? value, decimal defaultValue = 0m);
    
    /// <summary>
    /// Безопасно конвертирует в int
    /// </summary>
    int SafeInt(object? value, int defaultValue = 0);
}

// === DATA TRANSFER OBJECTS ===

public record ProcessingResult<T>(bool Success, T? Data, string? ErrorMessage, ProcessingStatistics Statistics);

public record ProcessingStatistics
{
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int SkippedFiles { get; init; }
    public int TotalRoutes { get; init; }
    public int ProcessedRoutes { get; init; }
    public int DuplicateRoutes { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public Dictionary<string, object> Details { get; init; } = new();
}

public record NormWithCount(string NormId, int RouteCount);

public record AnalysisOptions
{
    public bool UseCoefficients { get; init; }
    public bool ExcludeLowWork { get; init; }
    public IEnumerable<(string Series, int Number)>? SelectedLocomotives { get; init; }
}

public record InterpolationFunction(string NormId, string NormType, decimal[] XValues, decimal[] YValues);

public record ValidationResults
{
    public IEnumerable<string> ValidNorms { get; init; } = [];
    public IEnumerable<string> InvalidNorms { get; init; } = [];
    public IEnumerable<string> Warnings { get; init; } = [];
}

public record LocomotiveFilter
{
    public IEnumerable<(string Series, int Number)> SelectedLocomotives { get; init; } = [];
    public bool UseCoefficients { get; init; }
    public bool ExcludeLowWork { get; init; }
}

public record StorageInfo
{
    public int TotalNorms { get; init; }
    public int TotalPoints { get; init; }
    public Dictionary<string, int> NormsByType { get; init; } = new();
    public DateTime LastUpdated { get; init; }
}

public record ExportOptions
{
    public bool IncludeFormatting { get; init; } = true;
    public bool HighlightDeviations { get; init; } = true;
    public bool IncludeStatistics { get; init; } = true;
}

public record VisualizationData
{
    public ChartData NormCurves { get; init; } = new();
    public ChartData RoutePoints { get; init; } = new();
    public ChartData DeviationAnalysis { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public record ChartData
{
    public IEnumerable<ChartSeries> Series { get; init; } = [];
    public ChartAxes Axes { get; init; } = new();
}

public record ChartSeries(string Name, decimal[] XValues, decimal[] YValues, string Color, string Type);

public record ChartAxes(string XTitle, string YTitle, decimal? XMin = null, decimal? XMax = null);