using System.Drawing;
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
/// ИСПРАВЛЕНО: Добавлен недостающий метод InterpolateNormValueAsync
/// </summary>
public interface INormInterpolationService
{
    /// <summary>
    /// Интерполирует значение нормы для заданной нагрузки
    /// ДОБАВЛЕНО: Недостающий метод из реализации
    /// </summary>
    Task<decimal?> InterpolateNormValueAsync(string normId, decimal loadValue);

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
/// ИСПРАВЛЕНО: GetNormAsync вместо GetNormByIdAsync
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
    /// ИСПРАВЛЕНО: Правильная сигнатура метода
    /// </summary>
    Task<Norm?> GetNormAsync(string normId, CancellationToken cancellationToken = default);

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
    /// Получает результат анализа из кэша по хэшу
    /// </summary>
    Task<AnalysisResult?> GetCachedAnalysisAsync(string analysisHash);

    /// <summary>
    /// Сохраняет результат анализа в кэш
    /// </summary>
    Task SaveAnalysisAsync(AnalysisResult analysisResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// Очищает устаревший кэш
    /// </summary>
    Task CleanupExpiredCacheAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает статистику использования кэша
    /// </summary>
    Task<CacheStatistics> GetCacheStatisticsAsync(CancellationToken cancellationToken = default);
}

// === VISUALIZATION SERVICES ===

/// <summary>
/// Сервис подготовки данных для визуализации
/// ИСПРАВЛЕНО: Совместимость с OxyPlot и правильные сигнатуры методов
/// </summary>
public interface IVisualizationDataService
{
    /// <summary>
    /// Подготавливает данные для интерактивного графика
    /// ИСПРАВЛЕНО: Правильная сигнатура с VisualizationData
    /// </summary>
    Task<VisualizationData> PrepareInteractiveChartDataAsync(
        string sectionName,
        IEnumerable<Route> routes,
        Dictionary<string, InterpolationFunction> normFunctions,
        string? specificNormId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Экспортирует график в файл изображения
    /// </summary>
    Task<bool> ExportChartToImageAsync(
        VisualizationData visualizationData,
        string outputPath,
        PlotExportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Подготавливает статистические данные
    /// </summary>
    Task<StatisticsData> PrepareStatisticsDataAsync(
        IEnumerable<Route> routes,
        string? sectionFilter = null,
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
    /// ИСПРАВЛЕНО: Правильный возвращаемый тип
    /// </summary>
    Task<bool> ExportRoutesToExcelAsync(
        IEnumerable<Route> routes,
        string outputPath,
        ExportOptions? options = null);

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
/// Опции анализа данных
/// </summary>
public class AnalysisOptions
{
    public bool UseCoefficients { get; set; } = true;
    public decimal TolerancePercent { get; set; } = 5.0m;
    public bool EnableCaching { get; set; } = true;
    public bool ValidateResults { get; set; } = true;
    public bool IncludeDeviationAnalysis { get; set; } = true;
    public bool CalculateStatistics { get; set; } = true;
    public int MaxRoutes { get; set; } = 10000;
    public double MinLoad { get; set; } = 0.1;
    public double MaxLoad { get; set; } = 1000.0;
}

/// <summary>
/// Статистика по участку
/// </summary>
public class SectionStatistics
{
    public string SectionName { get; set; } = string.Empty;
    public int TotalRoutes { get; set; }
    public int UniqueNorms { get; set; }
    public decimal AverageDeviation { get; set; }
    public Dictionary<string, int> StatusDistribution { get; set; } = new();
    public DateTime LastAnalysis { get; set; }
}

/// <summary>
/// Функция интерполяции
/// ИСПРАВЛЕНО: Совместимость с двумя форматами (record и class)
/// </summary>
public record InterpolationFunction(
    string NormId,
    string NormType,
    decimal[] XValues,
    decimal[] YValues)
{
    public bool IsValid => XValues.Length >= 2 && XValues.Length == YValues.Length;
    public decimal MinX => XValues.Min();
    public decimal MaxX => XValues.Max();
    public int PointCount => XValues.Length;

    // Дополнительные свойства для совместимости
    public string Id { get; init; } = NormId;
    public string Description { get; init; } = NormType;
    public string InterpolationType { get; init; } = "linear";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public double MinLoad => (double)MinX;
    public double MaxLoad => (double)MaxX;

    /// <summary>
    /// Простая линейная интерполяция
    /// </summary>
    public double Interpolate(double x)
    {
        if (XValues.Length == 0) return 0;
        if (x <= (double)XValues[0]) return (double)YValues[0];
        if (x >= (double)XValues[^1]) return (double)YValues[^1];

        for (int i = 0; i < XValues.Length - 1; i++)
        {
            var x1 = (double)XValues[i];
            var x2 = (double)XValues[i + 1];
            if (x >= x1 && x <= x2)
            {
                var y1 = (double)YValues[i];
                var y2 = (double)YValues[i + 1];
                return y1 + (y2 - y1) * (x - x1) / (x2 - x1);
            }
        }
        return 0;
    }
}

/// <summary>
/// Информация о хранилище
/// </summary>
public class StorageInfo
{
    public int TotalNorms { get; set; }
    public int ValidNorms { get; set; }
    public int InvalidNorms { get; set; }
    public DateTime LastUpdate { get; set; }
    public long StorageSize { get; set; }
    public int TotalPoints { get; set; }
    public long DatabaseSizeBytes { get; set; }
}

/// <summary>
/// Результат анализа
/// </summary>
public class AnalysisResult
{
    public string SectionName { get; set; } = string.Empty;
    public string? NormId { get; set; }
    public bool SingleSectionOnly { get; set; }
    public bool UseCoefficients { get; set; }
    public List<Route> Routes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string? AnalysisHash { get; set; }

    // Дополнительные статистические свойства
    public int TotalRoutes { get; set; }
    public int ProcessedRoutes { get; set; }
    public int AnalyzedRoutes { get; set; }
    public int Economy { get; set; }
    public int Normal { get; set; }
    public int Overrun { get; set; }
    public decimal AverageDeviation { get; set; }

    /// <summary>
    /// Генерирует хэш для кэширования
    /// </summary>
    public void GenerateAnalysisHash()
    {
        var hashInput = $"{SectionName}_{NormId ?? "all"}_{SingleSectionOnly}_{UseCoefficients}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hashInput));
        AnalysisHash = Convert.ToHexString(hashBytes)[..16]; // Первые 16 символов
    }
}

/// <summary>
/// Норма с количеством маршрутов
/// </summary>
public record NormWithCount(string NormId, int RouteCount);

/// <summary>
/// Статистика кэша
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int HitCount { get; set; }
    public int MissCount { get; set; }
    public decimal HitRatio => TotalEntries > 0 ? (decimal)HitCount / TotalEntries : 0;
    public DateTime LastCleanup { get; set; }
    public int ExpiredEntries { get; set; }
    public long CacheSizeBytes { get; set; }
    public double HitRate => TotalEntries > 0 ? (double)HitCount / TotalEntries : 0;
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
/// Результат обработки (обобщенный класс)
/// </summary>
public class ProcessingResult<T>
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Данные результата
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Статистика обработки
    /// </summary>
    public ProcessingStatistics? Statistics { get; set; }

    /// <summary>
    /// Создает успешный результат
    /// </summary>
    public static ProcessingResult<T> Success(T data, ProcessingStatistics? statistics = null)
    {
        return new ProcessingResult<T>
        {
            IsSuccess = true,
            Data = data,
            Statistics = statistics
        };
    }

    /// <summary>
    /// Создает результат с ошибкой
    /// </summary>
    public static ProcessingResult<T> Failure(string errorMessage)
    {
        return new ProcessingResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Статистика обработки
/// </summary>
public class ProcessingStatistics
{
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public int ErrorFiles { get; set; }
    public long ProcessingTimeMs { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }

    public double SuccessRate => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
}

/// <summary>
/// Опции экспорта
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Включить графики в экспорт
    /// </summary>
    public bool IncludeCharts { get; set; } = true;

    /// <summary>
    /// Включить детальную статистику
    /// </summary>
    public bool IncludeDetailedStatistics { get; set; } = true;

    /// <summary>
    /// Формат дат для экспорта
    /// </summary>
    public string DateFormat { get; set; } = "dd.MM.yyyy";

    /// <summary>
    /// Включить сводную информацию
    /// </summary>
    public bool IncludeSummary { get; set; } = true;

    /// <summary>
    /// Показывать только обработанные данные
    /// </summary>
    public bool ProcessedDataOnly { get; set; } = false;
}

/// <summary>
/// Данные для визуализации
/// ИСПРАВЛЕНО: Полное определение всех классов данных
/// </summary>
public class VisualizationData
{
    public string Title { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public ChartSeriesData NormCurves { get; set; } = new();
    public ChartSeriesData RoutePoints { get; set; } = new();
    public DeviationAnalysisData DeviationData { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Данные серий для графиков
/// </summary>
public class ChartSeriesData
{
    public string Title { get; set; } = string.Empty;
    public AxisConfiguration Axes { get; set; } = new();
    public List<SeriesData> Series { get; set; } = new();
}

/// <summary>
/// Данные одной серии
/// </summary>
public class SeriesData
{
    public string Name { get; set; } = string.Empty;
    public decimal[] XValues { get; set; } = Array.Empty<decimal>();
    public decimal[] YValues { get; set; } = Array.Empty<decimal>();
    public string Color { get; set; } = "#000000";
    public string SeriesType { get; set; } = "Line"; // Line, Scatter, etc.
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Конфигурация осей
/// </summary>
public class AxisConfiguration
{
    public string XAxisTitle { get; set; } = string.Empty;
    public string YAxisTitle { get; set; } = string.Empty;
    public string XAxisKey { get; set; } = "BottomAxis";
    public string YAxisKey { get; set; } = "LeftAxis";
}

/// <summary>
/// Данные анализа отклонений
/// </summary>
public class DeviationAnalysisData
{
    public int TotalRoutes { get; set; }
    public decimal AverageDeviation { get; set; }
    public Dictionary<string, int> DeviationRanges { get; set; } = new();
    public Dictionary<string, int> StatusDistribution { get; set; } = new();
}

/// <summary>
/// Данные статистики
/// </summary>
public class StatisticsData
{
    public int TotalRoutes { get; set; }
    public string? SectionName { get; set; }
    public decimal AverageDeviation { get; set; }
    public decimal MinDeviation { get; set; }
    public decimal MaxDeviation { get; set; }
    public decimal MedianDeviation { get; set; }
    public Dictionary<string, int> StatusDistribution { get; set; } = new();
    public Dictionary<string, int> NormDistribution { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Данные для графиков (оригинальная структура для совместимости)
/// </summary>
public class ChartData
{
    public string Title { get; set; } = string.Empty;
    public ChartAxes Axes { get; set; } = new("", "");
    public List<ChartSeries> Series { get; set; } = new();
    public ChartStyle Style { get; set; } = new();
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
    public List<ChartPoint> Points { get; set; } = new();
    public string Color { get; set; } = "#000000";
    public string SeriesType { get; set; } = "line";

    // Альтернативное представление для совместимости
    public decimal[] XValues => Points.Select(p => (decimal)p.X).ToArray();
    public decimal[] YValues => Points.Select(p => (decimal)p.Y).ToArray();
    public string Type => SeriesType;
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
/// Опции экспорта графика
/// </summary>
public class PlotExportOptions
{
    public int Width { get; set; } = 1200;
    public int Height { get; set; } = 800;
    public int Resolution { get; set; } = 96;
    public Color BackgroundColor { get; set; } = Color.White;
    public bool IncludeLegend { get; set; } = true;
    public string Format { get; set; } = "png";
    public ImageFormat ImageFormat { get; set; } = ImageFormat.PNG;
    public bool IncludeTitle { get; set; } = true;

    public static PlotExportOptions Default => new();
}

/// <summary>
/// Форматы экспорта изображений
/// </summary>
public enum ImageFormat
{
    PNG,
    JPEG,
    SVG,
    PDF
}