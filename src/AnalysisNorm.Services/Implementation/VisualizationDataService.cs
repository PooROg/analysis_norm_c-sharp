using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Сервис подготовки данных для визуализации в OxyPlot
/// Соответствует PlotBuilder из Python analysis/visualization.py
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
                Axes = new ChartAxes("Нагрузка на ось, т/ось", "Расход, кВт·ч/10⁴ ткм")
            };

            // Подготавливаем точки маршрутов (верхний график)
            var routePoints = PrepareRoutePoints(routesList);
            visualizationData.RoutePoints = new ChartData 
            { 
                Series = routePoints,
                Axes = new ChartAxes("Нагрузка на ось, т/ось", "Расход, кВт·ч/10⁴ ткм")
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
        var maxIndex = routesList.Count > 0 ? routesList.Count - 1 : 10;

        // Линия верхнего допуска
        series.Add(new ChartSeries(
            Name: $"+{tolerancePercent}% допуск",
            XValues: new[] { 0m, maxIndex },
            YValues: new[] { tolerancePercent, tolerancePercent },
            Color: "#FF0000",
            Type: "line"
        ));

        // Линия нижнего допуска
        series.Add(new ChartSeries(
            Name: $"-{tolerancePercent}% допуск", 
            XValues: new[] { 0m, maxIndex },
            YValues: new[] { -tolerancePercent, -tolerancePercent },
            Color: "#FF0000",
            Type: "line"
        ));

        // Линия нормы (0%)
        series.Add(new ChartSeries(
            Name: "Норма",
            XValues: new[] { 0m, maxIndex },
            YValues: new[] { 0m, 0m },
            Color: "#000000",
            Type: "line"
        ));

        return new ChartData 
        { 
            Series = series, 
            Axes = new ChartAxes("Индекс маршрута", "Отклонение, %", 0, maxIndex) 
        };
    }

    /// <summary>
    /// Создает сводные данные для дашборда
    /// </summary>
    public async Task<DashboardData> PrepareDashboardDataAsync(
        IEnumerable<Route> routes,
        Dictionary<string, InterpolationFunction> normFunctions)
    {
        var routesList = routes.ToList();
        
        _logger.LogDebug("Подготавливаем данные дашборда для {RouteCount} маршрутов", routesList.Count);

        var dashboardData = new DashboardData();

        try
        {
            // Основная статистика
            var routesWithDeviations = routesList.Where(r => r.DeviationPercent.HasValue).ToList();
            
            if (routesWithDeviations.Any())
            {
                var deviations = routesWithDeviations.Select(r => r.DeviationPercent!.Value).ToList();
                
                dashboardData.TotalRoutes = routesList.Count;
                dashboardData.AnalyzedRoutes = routesWithDeviations.Count;
                dashboardData.AverageDeviation = deviations.Average();
                dashboardData.MinDeviation = deviations.Min();
                dashboardData.MaxDeviation = deviations.Max();
                
                // Распределение по статусам
                dashboardData.StatusDistribution = routesWithDeviations
                    .GroupBy(r => r.Status ?? "Неизвестно")
                    .ToDictionary(g => g.Key, g => g.Count());

                // Топ участков по отклонениям
                dashboardData.TopSectionsByDeviation = routesWithDeviations
                    .Where(r => !string.IsNullOrEmpty(r.SectionName))
                    .GroupBy(r => r.SectionName!)
                    .Select(g => new SectionSummary
                    {
                        SectionName = g.Key,
                        RouteCount = g.Count(),
                        AverageDeviation = g.Average(r => r.DeviationPercent!.Value)
                    })
                    .OrderByDescending(s => Math.Abs(s.AverageDeviation))
                    .Take(10)
                    .ToList();

                // Топ локомотивов по отклонениям  
                dashboardData.TopLocomotivesByDeviation = routesWithDeviations
                    .Where(r => !string.IsNullOrEmpty(r.LocomotiveSeries) && r.LocomotiveNumber.HasValue)
                    .GroupBy(r => $"{r.LocomotiveSeries}-{r.LocomotiveNumber}")
                    .Select(g => new LocomotiveSummary
                    {
                        LocomotiveId = g.Key,
                        RouteCount = g.Count(),
                        AverageDeviation = g.Average(r => r.DeviationPercent!.Value)
                    })
                    .OrderByDescending(l => Math.Abs(l.AverageDeviation))
                    .Take(10)
                    .ToList();
            }

            // Статистика норм
            dashboardData.NormStatistics = new NormStatistics
            {
                TotalNorms = normFunctions.Count,
                NormsByType = normFunctions.GroupBy(nf => nf.Value.NormType)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            await Task.CompletedTask;
            return dashboardData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка подготовки данных дашборда");
            return dashboardData;
        }
    }

    /// <summary>
    /// Подготавливает кривые норм для графика
    /// Соответствует _add_norm_curves из Python PlotBuilder
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
            _logger.LogError(ex, "Ошибка интерполяции кривой нормы {NormId}", normId);
            return null;
        }
    }

    /// <summary>
    /// Подготавливает точки маршрутов для отображения на графике
    /// Соответствует _add_route_points из Python PlotBuilder
    /// </summary>
    private IEnumerable<ChartSeries> PrepareRoutePoints(List<Route> routes)
    {
        var series = new List<ChartSeries>();

        var validRoutes = routes.Where(r => 
            r.AxleLoad.HasValue && 
            r.FactUd.HasValue && 
            r.AxleLoad > 0 && 
            r.FactUd > 0
        ).ToList();

        if (!validRoutes.Any())
        {
            _logger.LogTrace("Нет валидных маршрутов для отображения точек");
            return series;
        }

        // Группируем по статусам для цветового кодирования
        var statusGroups = validRoutes
            .Where(r => !string.IsNullOrEmpty(r.Status))
            .GroupBy(r => r.Status!)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var group in statusGroups)
        {
            var groupRoutes = group.ToList();
            var xValues = groupRoutes.Select(r => r.AxleLoad!.Value).ToArray();
            var yValues = groupRoutes.Select(r => r.FactUd!.Value).ToArray();
            
            var color = StatusColors.TryGetValue(group.Key, out var statusColor) 
                ? statusColor 
                : "#808080"; // Gray для неизвестных статусов

            series.Add(new ChartSeries(
                Name: $"{group.Key} ({group.Count()})",
                XValues: xValues,
                YValues: yValues,
                Color: color,
                Type: "scatter"
            ));
        }

        _logger.LogTrace("Подготовлено {SeriesCount} серий точек маршрутов ({TotalPoints} точек)", 
            series.Count, validRoutes.Count);

        return series;
    }

    /// <summary>
    /// Создает метаданные для графика
    /// </summary>
    private Dictionary<string, object> CreateMetadata(
        string sectionName, 
        List<Route> routes, 
        int normFunctionCount,
        string? specificNormId)
    {
        var routesWithDeviations = routes.Where(r => r.DeviationPercent.HasValue).ToList();
        
        var metadata = new Dictionary<string, object>
        {
            ["SectionName"] = sectionName,
            ["TotalRoutes"] = routes.Count,
            ["RoutesWithDeviations"] = routesWithDeviations.Count,
            ["NormFunctions"] = normFunctionCount,
            ["SpecificNormId"] = specificNormId ?? "все",
            ["CreatedAt"] = DateTime.UtcNow,
            ["DataQuality"] = CalculateDataQuality(routes)
        };

        if (routesWithDeviations.Any())
        {
            var deviations = routesWithDeviations.Select(r => r.DeviationPercent!.Value).ToList();
            
            metadata["AverageDeviation"] = Math.Round(deviations.Average(), 2);
            metadata["MinDeviation"] = deviations.Min();
            metadata["MaxDeviation"] = deviations.Max();
            metadata["StandardDeviation"] = Math.Round(CalculateStandardDeviation(deviations), 2);
        }

        return metadata;
    }

    /// <summary>
    /// Вычисляет качество данных (процент маршрутов с полными данными)
    /// </summary>
    private decimal CalculateDataQuality(List<Route> routes)
    {
        if (!routes.Any()) return 0;

        var completeRoutes = routes.Count(r => 
            !string.IsNullOrEmpty(r.RouteNumber) &&
            !string.IsNullOrEmpty(r.SectionName) &&
            !string.IsNullOrEmpty(r.NormNumber) &&
            r.AxleLoad.HasValue &&
            r.FactConsumption.HasValue &&
            r.AxleLoad > 0 &&
            r.FactConsumption > 0
        );

        return Math.Round((decimal)completeRoutes / routes.Count * 100, 1);
    }

    /// <summary>
    /// Вычисляет стандартное отклонение
    /// </summary>
    private decimal CalculateStandardDeviation(List<decimal> values)
    {
        if (values.Count <= 1) return 0;
        
        var mean = values.Average();
        var variance = values.Sum(x => (decimal)Math.Pow((double)(x - mean), 2)) / (values.Count - 1);
        
        return (decimal)Math.Sqrt((double)variance);
    }

    /// <summary>
    /// Создает данные для экспорта графика
    /// </summary>
    public async Task<ExportData> PrepareExportDataAsync(
        VisualizationData visualizationData, 
        ExportFormat format)
    {
        _logger.LogDebug("Подготавливаем данные для экспорта в формате {Format}", format);

        var exportData = new ExportData
        {
            Format = format,
            CreatedAt = DateTime.UtcNow,
            Data = new Dictionary<string, object>()
        };

        try
        {
            switch (format)
            {
                case ExportFormat.Csv:
                    exportData.Data["CsvData"] = await PrepareCsvExportAsync(visualizationData);
                    break;
                    
                case ExportFormat.Json:
                    exportData.Data["JsonData"] = visualizationData;
                    break;
                    
                case ExportFormat.Image:
                    exportData.Data["ImageSettings"] = PrepareImageExportSettings(visualizationData);
                    break;
                    
                default:
                    throw new ArgumentException($"Неподдерживаемый формат экспорта: {format}");
            }

            return exportData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка подготовки данных для экспорта в формате {Format}", format);
            exportData.Data["Error"] = ex.Message;
            return exportData;
        }
    }

    /// <summary>
    /// Подготавливает данные для CSV экспорта
    /// </summary>
    private async Task<string> PrepareCsvExportAsync(VisualizationData visualizationData)
    {
        var csv = new List<string> { "Series,X,Y,Color,Type" };

        // Экспортируем все серии данных
        foreach (var series in visualizationData.NormCurves.Series.Concat(visualizationData.RoutePoints.Series))
        {
            for (int i = 0; i < series.XValues.Length; i++)
            {
                csv.Add($"{series.Name},{series.XValues[i]},{series.YValues[i]},{series.Color},{series.Type}");
            }
        }

        await Task.CompletedTask;
        return string.Join("\n", csv);
    }

    /// <summary>
    /// Подготавливает настройки для экспорта изображения
    /// </summary>
    private Dictionary<string, object> PrepareImageExportSettings(VisualizationData visualizationData)
    {
        return new Dictionary<string, object>
        {
            ["Width"] = 1200,
            ["Height"] = 800,
            ["DPI"] = 300,
            ["Format"] = "PNG",
            ["Title"] = visualizationData.Metadata.ContainsKey("SectionName") 
                ? $"Анализ участка: {visualizationData.Metadata["SectionName"]}"
                : "Анализ норм расхода",
            ["SeriesCount"] = visualizationData.NormCurves.Series.Count() + visualizationData.RoutePoints.Series.Count()
        };
    }
}

/// <summary>
/// Данные дашборда для обзорной визуализации
/// </summary>
public class DashboardData
{
    public int TotalRoutes { get; set; }
    public int AnalyzedRoutes { get; set; }
    public decimal AverageDeviation { get; set; }
    public decimal MinDeviation { get; set; }
    public decimal MaxDeviation { get; set; }
    public Dictionary<string, int> StatusDistribution { get; set; } = new();
    public List<SectionSummary> TopSectionsByDeviation { get; set; } = new();
    public List<LocomotiveSummary> TopLocomotivesByDeviation { get; set; } = new();
    public NormStatistics NormStatistics { get; set; } = new();
}

/// <summary>
/// Сводка по участку
/// </summary>
public class SectionSummary
{
    public string SectionName { get; set; } = string.Empty;
    public int RouteCount { get; set; }
    public decimal AverageDeviation { get; set; }
}

/// <summary>
/// Сводка по локомотиву
/// </summary>
public class LocomotiveSummary
{
    public string LocomotiveId { get; set; } = string.Empty;
    public int RouteCount { get; set; }
    public decimal AverageDeviation { get; set; }
}

/// <summary>
/// Статистика норм
/// </summary>
public class NormStatistics
{
    public int TotalNorms { get; set; }
    public Dictionary<string, int> NormsByType { get; set; } = new();
}

/// <summary>
/// Данные для экспорта
/// </summary>
public class ExportData
{
    public ExportFormat Format { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Форматы экспорта
/// </summary>
public enum ExportFormat
{
    Csv,
    Json,
    Image,
    Excel
}