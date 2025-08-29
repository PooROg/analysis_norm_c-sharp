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
    /// Обрабатывает один HTML файл маршрутов
    /// </summary>
    Task<ProcessingResult<IEnumerable<Route>>> ProcessHtmlFileAsync(
        string htmlFile,
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
    /// Обрабатывает один HTML файл норм
    /// </summary>
    Task<ProcessingResult<IEnumerable<Norm>>> ProcessHtmlFileAsync(
        string htmlFile,
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
    /// Анализирует маршруты
    /// </summary>
    Task<AnalysisResult> AnalyzeRoutesAsync(
        IEnumerable<Route> routes,
        Dictionary<string, InterpolationFunction>? normFunctions = null,
        AnalysisOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает все доступные участки
    /// </summary>
    Task<IEnumerable<string>> GetAvailableSectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает статистику по участку
    /// </summary>
    Task<SectionStatistics> GetSectionStatisticsAsync(string sectionName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Сервис интерполяции норм
/// Соответствует Python scipy interpolation
/// </summary>
public interface INormInterpolationService
{
    /// <summary>
    /// Создает функцию интерполяции для нормы
    /// </summary>
    Task<InterpolationFunction?> CreateInterpolationFunctionAsync(
        string normId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Интерполирует значение для заданной нагрузки
    /// </summary>
    double InterpolateValue(InterpolationFunction function, double load);

    /// <summary>
    /// Проверяет валидность функции интерполяции
    /// </summary>
    bool IsValidFunction(InterpolationFunction function);

    /// <summary>
    /// Получает диапазон валидных значений для функции
    /// </summary>
    (double MinLoad, double MaxLoad) GetValidRange(InterpolationFunction function);
}

/// <summary>
/// Сервис хранения норм
/// Соответствует Python NormStorage
/// </summary>
public interface INormStorageService
{
    /// <summary>
    /// Сохраняет нормы в базу данных
    /// </summary>
    Task SaveNormsAsync(IEnumerable<Norm> norms, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает все нормы
    /// </summary>
    Task<IEnumerable<Norm>> GetAllNormsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает норму по ID
    /// </summary>
    Task<Norm?> GetNormByIdAsync(string normId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет устаревшие нормы
    /// </summary>
    Task CleanupOldNormsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает информацию о хранилище норм
    /// </summary>
    Task<StorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Сервис кэширования анализа
/// </summary>
public interface IAnalysisCacheService
{
    /// <summary>
    /// Получает результат анализа из кэша
    /// </summary>
    Task<AnalysisResult?> GetCachedAnalysisAsync(
        string sectionName,
        string? normId,
        bool singleSectionOnly,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет результат анализа в кэш
    /// </summary>
    Task SaveAnalysisAsync(AnalysisResult analysis, CancellationToken cancellationToken = default);

    /// <summary>
    /// Очищает устаревший кэш
    /// </summary>
    Task CleanupExpiredCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает статистику кэша
    /// </summary>
    Task<CacheStatistics> GetCacheStatisticsAsync(CancellationToken cancellationToken = default);
}

// === VISUALIZATION SERVICES ===

/// <summary>
/// Сервис подготовки данных для визуализации
/// </summary>
public interface IVisualizationDataService
{
    /// <summary>
    /// Подготавливает данные для интерактивных графиков
    /// </summary>
    Task<ChartData> PrepareInteractiveChartDataAsync(
        string sectionName,
        IEnumerable<Route> routes,
        Dictionary<string, InterpolationFunction> normFunctions,
        string? selectedNorm = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Подготавливает данные для визуализации
    /// </summary>
    Task<VisualizationData> PrepareVisualizationDataAsync(
        AnalysisResult analysisResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создает данные для графика отклонений
    /// </summary>
    ChartData CreateDeviationChartData(IEnumerable<Route> routes);

    /// <summary>
    /// Экспортирует график в изображение
    /// </summary>
    Task<string> ExportPlotToImageAsync(
        ChartData chartData,
        string outputPath,
        ImageFormat format = ImageFormat.PNG,
        CancellationToken cancellationToken = default);
}

// === LOCOMOTIVE SERVICES ===

/// <summary>
/// Сервис работы с коэффициентами локомотивов
/// </summary>
public interface ILocomotiveCoefficientService
{
    /// <summary>
    /// Загружает коэффициенты из Excel файла
    /// </summary>
    Task<ProcessingResult<IEnumerable<LocomotiveCoefficient>>> LoadCoefficientsFromExcelAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает коэффициент для локомотива
    /// </summary>
    Task<LocomotiveCoefficient?> GetCoefficientAsync(
        string series,
        string? number = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает все коэффициенты
    /// </summary>
    Task<IEnumerable<LocomotiveCoefficient>> GetAllCoefficientsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает статистику коэффициентов
    /// </summary>
    Task<CoefficientStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Сервис фильтрации локомотивов
/// </summary>
public interface ILocomotiveFilterService
{
    /// <summary>
    /// Фильтрует маршруты по выбранным сериям локомотивов
    /// </summary>
    Task<IEnumerable<Route>> FilterRoutesByLocomotivesAsync(
        IEnumerable<Route> routes,
        IEnumerable<string> selectedSeries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает все доступные серии локомотивов
    /// </summary>
    Task<IEnumerable<string>> GetAvailableSeriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Группирует серии локомотивов по типам
    /// </summary>
    Task<Dictionary<string, IEnumerable<string>>> GroupSeriesByTypeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает коэффициенты для фильтрации
    /// </summary>
    Task<ProcessingResult<IEnumerable<LocomotiveCoefficient>>> LoadCoefficientsAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

// === EXPORT SERVICES ===

/// <summary>
/// Сервис экспорта в Excel
/// </summary>
public interface IExcelExportService
{
    /// <summary>
    /// Экспортирует маршруты в Excel
    /// </summary>
    Task<string> ExportRoutesToExcelAsync(
        IEnumerable<Route> routes,
        string filePath,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Экспортирует результаты анализа в Excel
    /// </summary>
    Task<string> ExportAnalysisResultsAsync(
        AnalysisResult analysisResult,
        string filePath,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет возможность создания файла по указанному пути
    /// </summary>
    bool CanCreateFile(string filePath);
}

// === SUPPORTING CLASSES ===

/// <summary>
/// Опции анализа
/// </summary>
public class AnalysisOptions
{
    public bool UseCoefficients { get; set; } = true;
    public bool IncludeDeviationAnalysis { get; set; } = true;
    public bool CalculateStatistics { get; set; } = true;
    public int MaxRoutes { get; set; } = 10000;
    public double MinLoad { get; set; } = 0.1;
    public double MaxLoad { get; set; } = 1000.0;
}

/// <summary>
/// Функция интерполяции (упрощенная для исправления ошибок)
/// </summary>
public class InterpolationFunction
{
    public string NormId { get; set; } = string.Empty;
    public string InterpolationType { get; set; } = "linear";
    public List<(double X, double Y)> Points { get; set; } = new List<(double, double)>();
    public double MinX { get; set; }
    public double MaxX { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public double Interpolate(double x)
    {
        if (Points.Count == 0) return 0;
        if (x <= Points[0].X) return Points[0].Y;
        if (x >= Points[^1].X) return Points[^1].Y;

        for (int i = 0; i < Points.Count - 1; i++)
        {
            if (x >= Points[i].X && x <= Points[i + 1].X)
            {
                var x1 = Points[i].X;
                var y1 = Points[i].Y;
                var x2 = Points[i + 1].X;
                var y2 = Points[i + 1].Y;

                return y1 + (y2 - y1) * (x - x1) / (x2 - x1);
            }
        }

        return 0;
    }
}

/// <summary>
/// Данные для графиков
/// </summary>
public class ChartData
{
    public string Title { get; set; } = string.Empty;
    public ChartAxes Axes { get; set; } = new ChartAxes(string.Empty, string.Empty);
    public List<ChartSeries> Series { get; set; } = new List<ChartSeries>();
    public ChartStyle Style { get; set; } = new ChartStyle();
}

/// <summary>
/// Оси графика
/// </summary>
public class ChartAxes
{
    public string XTitle { get; set; }
    public string YTitle { get; set; }
    public decimal? XMin { get; set; }
    public decimal? XMax { get; set; }
    public decimal? YMin { get; set; }
    public decimal? YMax { get; set; }

    public ChartAxes(string xTitle, string yTitle, decimal? xMin = null, decimal? xMax = null)
    {
        XTitle = xTitle;
        YTitle = yTitle;
        XMin = xMin;
        XMax = xMax;
    }
}

/// <summary>
/// Серия данных для графика
/// </summary>
public class ChartSeries
{
    public string Name { get; set; } = string.Empty;
    public List<ChartPoint> Points { get; set; } = new List<ChartPoint>();
    public string Color { get; set; } = "#000000";
    public string SeriesType { get; set; } = "line";
}

/// <summary>
/// Точка данных для графика
/// </summary>
public class ChartPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public string? Label { get; set; }
    public object? Tag { get; set; }
}

/// <summary>
/// Стиль графика
/// </summary>
public class ChartStyle
{
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public bool ShowLegend { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
}

/// <summary>
/// Данные для визуализации
/// </summary>
public class VisualizationData
{
    public ChartData NormsChart { get; set; } = new ChartData();
    public ChartData DeviationsChart { get; set; } = new ChartData();
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Статистика участка
/// </summary>
public class SectionStatistics
{
    public string SectionName { get; set; } = string.Empty;
    public int TotalRoutes { get; set; }
    public int ProcessedRoutes { get; set; }
    public decimal AverageDeviation { get; set; }
    public DateTime LastAnalysis { get; set; }
}

/// <summary>
/// Информация о хранилище
/// </summary>
public class StorageInfo
{
    public int TotalNorms { get; set; }
    public int TotalPoints { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public DateTime LastUpdate { get; set; }
}

/// <summary>
/// Статистика кэша
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int ExpiredEntries { get; set; }
    public long CacheSizeBytes { get; set; }
    public double HitRate { get; set; }
}

/// <summary>
/// Статистика коэффициентов
/// </summary>
public class CoefficientStatistics
{
    public int TotalCoefficients { get; set; }
    public int UniqueSeries { get; set; }
    public decimal AverageCoefficient { get; set; }
    public DateTime LastUpdate { get; set; }
}

/// <summary>
/// Формат изображения для экспорта
/// </summary>
public enum ImageFormat
{
    PNG,
    JPEG,
    SVG,
    PDF
}