using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// CHAT 6 COMPLETE: Сервис подготовки данных для визуализации в OxyPlot
/// Соответствует PlotBuilder из Python analysis/visualization.py + экспорт изображений
/// </summary>
public class VisualizationDataService : IVisualizationDataService
{
    private readonly ILogger<VisualizationDataService> _logger;
    private readonly INormInterpolationService _interpolationService;
    private readonly ApplicationSettings _settings;

    // Цвета для различных типов данных (соответствуют Python ColorMapper)
    private static readonly Dictionary<string, string> NormTypeColors = new()
    {
        ["Нажатие"] = "#1f77b4",      // Синий
        ["Н/Ф"] = "#ff7f0e",          // Оранжевый  
        ["Уд. на работу"] = "#2ca02c", // Зеленый
        ["default"] = "#d62728"        // Красный
    };

    private static readonly Dictionary<string, string> StatusColors = new()
    {
        [DeviationStatus.EconomyStrong] = "#006400",   // DarkGreen
        [DeviationStatus.EconomyMedium] = "#008000",   // Green
        [DeviationStatus.EconomyWeak] = "#90EE90",     // LightGreen
        [DeviationStatus.Normal] = "#ADD8E6",          // LightBlue
        [DeviationStatus.OverrunWeak] = "#FFA500",     // Orange
        [DeviationStatus.OverrunMedium] = "#FF8C00",   // DarkOrange
        [DeviationStatus.OverrunStrong] = "#DC143C"    // Crimson
    };

    public VisualizationDataService(
        ILogger<VisualizationDataService> logger,
        INormInterpolationService interpolationService,
        IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _interpolationService = interpolationService;
        _settings = settings.Value;
    }

    #region Public Interface Implementation

    /// <summary>
    /// Подготавливает данные для интерактивного графика
    /// Соответствует create_interactive_plot из Python PlotBuilder
    /// </summary>
    public async Task<VisualizationData> PrepareInteractiveChartDataAsync(
        string sectionName,
        IEnumerable<Route> routes,
        Dictionary<string, InterpolationFunction> normFunctions,
        string? specificNormId = null)
    {
        var routesList = routes.ToList();
        _logger.LogInformation("Подготавливаем данные для визуализации участка {SectionName}: {RouteCount} маршрутов, {NormCount} функций норм", 
            sectionName, routesList.Count, normFunctions.Count);

        var visualizationData = new VisualizationData();

        try
        {
            // Подготавливаем кривые норм (верхний график)
            var normCurves = await PrepareNormCurvesAsync(normFunctions, specificNormId);
            visualizationData.NormCurves = new ChartData 
            { 
                Series = normCurves,
                Axes = new ChartAxes("Механическая работа, кВт·час", "Расход электроэнергии, кВт·час")
            };

            // Подготавливаем точки маршрутов (верхний график)
            var routePoints = PrepareRoutePoints(routesList);
            visualizationData.RoutePoints = new ChartData 
            { 
                Series = routePoints,
                Axes = new ChartAxes("Механическая работа, кВт·час", "Расход электроэнергии, кВт·час")
            };

            // Подготавливаем анализ отклонений (нижний график)
            var deviationData = CreateDeviationChartData(routesList);
            visualizationData.DeviationAnalysis = deviationData;

            // Метаданные для графика
            visualizationData.Metadata = CreateMetadata(sectionName, routesList, normFunctions.Count, specificNormId);

            _logger.LogDebug("Подготовка данных завершена: {NormCurves} кривых норм, {RoutePoints} точек маршрутов", 
                normCurves.Count(), routePoints.Count());

            return visualizationData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка подготовки данных для визуализации участка {SectionName}", sectionName);
            
            // Возвращаем пустые данные с ошибкой
            visualizationData.Metadata["Error"] = ex.Message;
            return visualizationData;
        }
    }

    /// <summary>
    /// Создает данные для графика отклонений
    /// Соответствует _add_deviation_analysis из Python PlotBuilder
    /// </summary>
    public ChartData CreateDeviationChartData(IEnumerable<Route> routes)
    {
        var routesList = routes.Where(r => r.DeviationPercent.HasValue).ToList();
        
        _logger.LogTrace("Создаем данные графика отклонений для {Count} маршрутов", routesList.Count);

        var series = new List<ChartSeries>();

        if (!routesList.Any())
        {
            return new ChartData { Series = series, Axes = new ChartAxes("Индекс маршрута", "Отклонение, %") };
        }

        // Группируем маршруты по статусам для цветового кодирования
        var statusGroups = routesList
            .Where(r => !string.IsNullOrEmpty(r.Status))
            .GroupBy(r => r.Status!)
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (var group in statusGroups)
        {
            var groupRoutes = group.OrderBy(r => r.CreatedAt).ToList();
            var xValues = Enumerable.Range(0, groupRoutes.Count).Select(i => (decimal)i).ToArray();
            var yValues = groupRoutes.Select(r => r.DeviationPercent!.Value).ToArray();
            
            var color = StatusColors.TryGetValue(group.Key, out var statusColor) 
                ? statusColor 
                : StatusColors["default"];

            series.Add(new ChartSeries(
                Name: $"{group.Key} ({group.Count()})",
                XValues: xValues,
                YValues: yValues,
                Color: color,
                Type: "scatter"
            ));
        }

        // Добавляем горизонтальные линии допусков
        var tolerancePercent = (decimal)_settings.DefaultTolerancePercent;
        var maxIndex = routesList.Count > 0 ? routesList.Count - 1 : 0;

        // Верхний допуск (+5%)
        series.Add(new ChartSeries(
            Name: $"Верхний допуск (+{tolerancePercent}%)",
            XValues: new[] { 0m, (decimal)maxIndex },
            YValues: new[] { tolerancePercent, tolerancePercent },
            Color: "#ff4444",
            Type: "line"
        ));

        // Нижний допуск (-5%)
        series.Add(new ChartSeries(
            Name: $"Нижний допуск (-{tolerancePercent}%)",
            XValues: new[] { 0m, (decimal)maxIndex },
            YValues: new[] { -tolerancePercent, -tolerancePercent },
            Color: "#44ff44",
            Type: "line"
        ));

        // Линия нормы (0%)
        series.Add(new ChartSeries(
            Name: "Норма (0%)",
            XValues: new[] { 0m, (decimal)maxIndex },
            YValues: new[] { 0m, 0m },
            Color: "#0066cc",
            Type: "line"
        ));

        return new ChartData 
        { 
            Series = series, 
            Axes = new ChartAxes("Индекс маршрута", "Отклонение от нормы, %") 
        };
    }

    /// <summary>
    /// CHAT 6 NEW: Экспорт графика в изображение
    /// Аналог Python plot export functionality с OxyPlot rendering
    /// </summary>
    public async Task<bool> ExportPlotToImageAsync(
        string outputPath,
        string sectionName,
        IEnumerable<Route> routes,
        Dictionary<string, object> normFunctions,
        string? specificNormId = null,
        bool singleSectionOnly = false,
        PlotExportOptions? options = null)
    {
        _logger.LogInformation("Экспортируем график участка {SectionName} в файл: {OutputPath}", 
            sectionName, outputPath);

        var routesList = routes.ToList();
        if (!routesList.Any())
        {
            _logger.LogWarning("Нет данных для экспорта графика");
            return false;
        }

        options ??= new PlotExportOptions();

        try
        {
            // Подготавливаем данные визуализации аналогично Python PlotBuilder
            var interpolationFunctions = ConvertToInterpolationFunctions(normFunctions);
            var visualizationData = await PrepareInteractiveChartDataAsync(
                sectionName, routesList, interpolationFunctions, specificNormId);

            // Создаем OxyPlot модель для экспорта - аналог Python dual subplot structure
            var plotModel = CreateExportPlotModel(visualizationData, options);

            // Экспортируем в файл используя OxyPlot exporters
            await ExportPlotModelToFileAsync(plotModel, outputPath, options);

            _logger.LogInformation("График успешно экспортирован: {OutputPath}", outputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта графика в изображение: {OutputPath}", outputPath);
            return false;
        }
    }

    #endregion

    #region Private Implementation Methods

    /// <summary>
    /// Подготавливает кривые норм - аналог Python _add_norm_curves
    /// </summary>
    private async Task<IEnumerable<ChartSeries>> PrepareNormCurvesAsync(
        Dictionary<string, InterpolationFunction> normFunctions, 
        string? specificNormId)
    {
        var series = new List<ChartSeries>();

        var functionsToRender = string.IsNullOrEmpty(specificNormId)
            ? normFunctions
            : normFunctions.Where(kvp => kvp.Key == specificNormId).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        _logger.LogTrace("Подготавливаем {Count} кривых норм", functionsToRender.Count);

        foreach (var (normId, function) in functionsToRender)
        {
            try
            {
                var curveSeries = await CreateNormCurveSeriesAsync(normId, function);
                if (curveSeries != null)
                {
                    series.Add(curveSeries);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка создания кривой для нормы {NormId}", normId);
            }
        }

        return series;
    }

    /// <summary>
    /// Создает серию данных для кривой нормы
    /// </summary>
    private async Task<ChartSeries?> CreateNormCurveSeriesAsync(string normId, InterpolationFunction function)
    {
        try
        {
            var minX = function.XValues.Min();
            var maxX = function.XValues.Max();
            var range = maxX - minX;
            
            // Создаем гладкую кривую с 100 точками
            const int pointCount = 100;
            var step = range / (pointCount - 1);
            
            var xValues = new List<decimal>();
            var yValues = new List<decimal>();

            for (int i = 0; i < pointCount; i++)
            {
                var x = minX + i * step;
                var interpolatedY = await _interpolationService.InterpolateNormValueAsync(normId, x);
                
                if (interpolatedY.HasValue)
                {
                    xValues.Add(x);
                    yValues.Add(interpolatedY.Value);
                }
            }

            if (!xValues.Any())
            {
                _logger.LogWarning("Не удалось создать точки кривой для нормы {NormId}", normId);
                return null;
            }

            var color = NormTypeColors.TryGetValue(function.NormType, out var typeColor) 
                ? typeColor 
                : NormTypeColors["default"];

            return new ChartSeries(
                Name: $"Норма {normId} ({function.NormType})",
                XValues: xValues.ToArray(),
                YValues: yValues.ToArray(),
                Color: color,
                Type: "line"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка интерполяции нормы {NormId}", normId);
            return null;
        }
    }

    /// <summary>
    /// Подготавливает точки маршрутов - аналог Python _add_route_points
    /// </summary>
    private IEnumerable<ChartSeries> PrepareRoutePoints(IList<Route> routes)
    {
        var series = new List<ChartSeries>();

        // Группируем маршруты по статусам отклонений для цветового кодирования
        var statusGroups = routes
            .Where(r => !string.IsNullOrEmpty(r.Status))
            .GroupBy(r => r.Status!)
            .ToList();

        foreach (var group in statusGroups)
        {
            var groupRoutes = group.ToList();
            var color = StatusColors.TryGetValue(group.Key, out var statusColor) 
                ? statusColor 
                : StatusColors["default"];

            series.Add(new ChartSeries(
                Name: $"Маршруты: {group.Key} ({groupRoutes.Count})",
                XValues: groupRoutes.Select(r => r.MechanicalWork).ToArray(),
                YValues: groupRoutes.Select(r => r.ElectricConsumption).ToArray(),
                Color: color,
                Type: "scatter"
            ));
        }

        return series;
    }

    /// <summary>
    /// Создает метаданные для графика
    /// </summary>
    private Dictionary<string, object> CreateMetadata(
        string sectionName, 
        IList<Route> routes, 
        int normFunctionsCount, 
        string? specificNormId)
    {
        var avgDeviation = routes.Where(r => r.DeviationPercent.HasValue)
                                .Average(r => r.DeviationPercent!.Value);

        var metadata = new Dictionary<string, object>
        {
            ["SectionName"] = sectionName,
            ["RouteCount"] = routes.Count,
            ["NormFunctionsCount"] = normFunctionsCount,
            ["AverageDeviation"] = avgDeviation,
            ["CreatedAt"] = DateTime.UtcNow,
            ["Title"] = CreatePlotTitle(sectionName, specificNormId),
            ["Subtitle"] = $"Анализ {routes.Count} маршрутов"
        };

        if (!string.IsNullOrEmpty(specificNormId))
        {
            metadata["SpecificNormId"] = specificNormId;
        }

        // Статистика по участкам
        var sectionStats = routes.SelectMany(r => r.SectionNames)
                                .GroupBy(s => s)
                                .ToDictionary(g => g.Key, g => g.Count());
        metadata["SectionStatistics"] = sectionStats;

        return metadata;
    }

    /// <summary>
    /// Создает заголовок графика
    /// </summary>
    private string CreatePlotTitle(string sectionName, string? specificNormId)
    {
        var title = $"Анализ норм расхода: {sectionName}";
        if (!string.IsNullOrEmpty(specificNormId))
        {
            title += $" (норма {specificNormId})";
        }
        return title;
    }

    #endregion

    #region CHAT 6: Image Export Implementation

    /// <summary>
    /// Создает OxyPlot модель для экспорта - объединяет dual subplot в один
    /// </summary>
    private PlotModel CreateExportPlotModel(VisualizationData visualizationData, PlotExportOptions options)
    {
        var plotModel = new PlotModel
        {
            Title = visualizationData.Metadata.GetValueOrDefault("Title", "График анализа норм")?.ToString(),
            Background = OxyColor.FromArgb(255, 
                options.BackgroundColor.R, 
                options.BackgroundColor.G, 
                options.BackgroundColor.B),
            TitleFontSize = 16,
            TitleFontWeight = FontWeights.Bold
        };

        // Создаем оси для объединенного графика
        var xAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = visualizationData.NormCurves.Axes.XAxisTitle,
            TitleFontSize = 12,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromArgb(40, 0, 0, 0)
        };

        var yAxisLeft = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = visualizationData.NormCurves.Axes.YAxisTitle,
            TitleFontSize = 12,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromArgb(40, 0, 0, 0),
            Key = "LeftAxis"
        };

        var yAxisRight = new LinearAxis
        {
            Position = AxisPosition.Right,
            Title = "Отклонение от нормы, %",
            TitleFontSize = 12,
            Key = "RightAxis"
        };

        plotModel.Axes.Add(xAxis);
        plotModel.Axes.Add(yAxisLeft);
        plotModel.Axes.Add(yAxisRight);

        // Добавляем кривые норм - аналог Python norm curves
        foreach (var normSeries in visualizationData.NormCurves.Series)
        {
            var lineSeries = new LineSeries
            {
                Title = normSeries.Name,
                Color = OxyColor.Parse(normSeries.Color),
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid,
                YAxisKey = "LeftAxis"
            };

            for (int i = 0; i < normSeries.XValues.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint((double)normSeries.XValues[i], (double)normSeries.YValues[i]));
            }

            plotModel.Series.Add(lineSeries);
        }

        // Добавляем точки маршрутов - аналог Python route points
        foreach (var routeSeries in visualizationData.RoutePoints.Series)
        {
            var scatterSeries = new ScatterSeries
            {
                Title = routeSeries.Name,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColor.Parse(routeSeries.Color),
                MarkerStroke = OxyColor.Parse(routeSeries.Color),
                YAxisKey = "LeftAxis"
            };

            for (int i = 0; i < routeSeries.XValues.Length; i++)
            {
                scatterSeries.Points.Add(new ScatterPoint((double)routeSeries.XValues[i], (double)routeSeries.YValues[i]));
            }

            plotModel.Series.Add(scatterSeries);
        }

        // Настраиваем легенду если требуется
        if (options.IncludeLegend)
        {
            plotModel.LegendTitle = "Легенда";
            plotModel.LegendPosition = LegendPosition.RightTop;
            plotModel.LegendPlacement = LegendPlacement.Outside;
            plotModel.LegendOrientation = LegendOrientation.Vertical;
        }

        return plotModel;
    }

    /// <summary>
    /// Экспортирует OxyPlot модель в файл изображения
    /// </summary>
    private async Task ExportPlotModelToFileAsync(PlotModel plotModel, string outputPath, PlotExportOptions options)
    {
        await Task.Run(() =>
        {
            var extension = Path.GetExtension(outputPath).ToLower();
            
            switch (extension)
            {
                case ".png":
                    var pngExporter = new PngExporter 
                    { 
                        Width = options.Width, 
                        Height = options.Height,
                        Resolution = options.Resolution,
                        Background = OxyColor.FromArgb(255, 
                            options.BackgroundColor.R, 
                            options.BackgroundColor.G, 
                            options.BackgroundColor.B)
                    };
                    pngExporter.ExportToFile(plotModel, outputPath);
                    break;
                    
                case ".jpg":
                case ".jpeg":
                    // Конвертируем PNG в JPEG так как OxyPlot не поддерживает прямой JPEG export
                    var tempPngPath = Path.ChangeExtension(outputPath, ".png");
                    var jpegPngExporter = new PngExporter 
                    { 
                        Width = options.Width, 
                        Height = options.Height,
                        Resolution = options.Resolution
                    };
                    jpegPngExporter.ExportToFile(plotModel, tempPngPath);
                    
                    // Конвертируем PNG в JPEG
                    ConvertPngToJpeg(tempPngPath, outputPath, 90); // 90% качество
                    File.Delete(tempPngPath); // Удаляем временный PNG
                    break;
                    
                case ".svg":
                    var svgExporter = new SvgExporter 
                    { 
                        Width = options.Width, 
                        Height = options.Height 
                    };
                    svgExporter.ExportToFile(plotModel, outputPath);
                    break;
                    
                case ".pdf":
                    var pdfExporter = new PdfExporter 
                    { 
                        Width = options.Width, 
                        Height = options.Height 
                    };
                    pdfExporter.ExportToFile(plotModel, outputPath);
                    break;
                    
                default:
                    throw new NotSupportedException($"Формат файла {extension} не поддерживается для экспорта");
            }
        });
    }

    /// <summary>
    /// Конвертирует PNG в JPEG с заданным качеством
    /// </summary>
    private void ConvertPngToJpeg(string pngPath, string jpegPath, long quality)
    {
        using var image = Image.FromFile(pngPath);
        var jpegEncoder = GetEncoder(System.Drawing.Imaging.ImageFormat.Jpeg);
        var qualityParams = new EncoderParameters(1);
        var qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        qualityParams.Param[0] = qualityParam;
        
        image.Save(jpegPath, jpegEncoder, qualityParams);
    }

    /// <summary>
    /// Получает JPEG encoder для конвертации
    /// </summary>
    private ImageCodecInfo GetEncoder(System.Drawing.Imaging.ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageDecoders();
        foreach (var codec in codecs)
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }
        throw new NotSupportedException("JPEG encoder не найден");
    }

    /// <summary>
    /// Конвертирует словарь норм в InterpolationFunction для совместимости
    /// </summary>
    private Dictionary<string, InterpolationFunction> ConvertToInterpolationFunctions(Dictionary<string, object> normFunctions)
    {
        var result = new Dictionary<string, InterpolationFunction>();
        
        foreach (var kvp in normFunctions)
        {
            if (kvp.Value is Dictionary<string, object> normData)
            {
                var points = normData.GetValueOrDefault("points", new List<NormPoint>()) as IEnumerable<NormPoint>;
                var normType = normData.GetValueOrDefault("norm_type", "Нажатие") as string;
                var description = normData.GetValueOrDefault("description", kvp.Key) as string;
                
                if (points?.Any() == true)
                {
                    var pointsList = points.ToList();
                    var interpolationFunction = new InterpolationFunction
                    {
                        Id = kvp.Key,
                        NormType = normType ?? "Нажатие",
                        Description = description ?? kvp.Key,
                        XValues = pointsList.Select(p => p.X).ToArray(),
                        YValues = pointsList.Select(p => p.Y).ToArray()
                    };
                    
                    result[kvp.Key] = interpolationFunction;
                }
            }
        }
        
        return result;
    }

    #endregion
}

#region Supporting Classes and Enums

/// <summary>
/// Структура данных для визуализации - аналог Python visualization data structure
/// </summary>
public class VisualizationData
{
    public ChartData NormCurves { get; set; } = new();
    public ChartData RoutePoints { get; set; } = new();
    public ChartData DeviationAnalysis { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Данные для построения графиков
/// </summary>
public class ChartData
{
    public IEnumerable<ChartSeries> Series { get; set; } = Array.Empty<ChartSeries>();
    public ChartAxes Axes { get; set; } = new("X", "Y");
}

/// <summary>
/// Серия данных для графика
/// </summary>
public record ChartSeries(
    string Name,
    decimal[] XValues,
    decimal[] YValues,
    string Color,
    string Type);

/// <summary>
/// Информация об осях графика
/// </summary>
public record ChartAxes(string XAxisTitle, string YAxisTitle);

/// <summary>
/// Опции экспорта изображения графика - CHAT 6 NEW
/// </summary>
public class PlotExportOptions
{
    public int Width { get; set; } = 1200;
    public int Height { get; set; } = 800;
    public int Resolution { get; set; } = 150;
    public ImageFormat Format { get; set; } = ImageFormat.PNG;
    public bool IncludeLegend { get; set; } = true;
    public bool IncludeTitle { get; set; } = true;
    public Color BackgroundColor { get; set; } = Color.White;
}

/// <summary>
/// Форматы экспорта изображений - CHAT 6 NEW
/// </summary>
public enum ImageFormat
{
    PNG,
    JPEG,
    SVG,
    PDF
}

/// <summary>
/// Функция интерполяции для норм - аналог Python interpolation function
/// </summary>
public class InterpolationFunction
{
    public string Id { get; set; } = string.Empty;
    public string NormType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal[] XValues { get; set; } = Array.Empty<decimal>();
    public decimal[] YValues { get; set; } = Array.Empty<decimal>();
}

/// <summary>
/// Статусы отклонений - константы для цветового кодирования
/// </summary>
public static class DeviationStatus
{
    public const string EconomyStrong = "Сильная экономия";
    public const string EconomyMedium = "Средняя экономия";
    public const string EconomyWeak = "Слабая экономия";
    public const string Normal = "В норме";
    public const string OverrunWeak = "Слабый перерасход";
    public const string OverrunMedium = "Средний перерасход";
    public const string OverrunStrong = "Сильный перерасход";
}

#endregion