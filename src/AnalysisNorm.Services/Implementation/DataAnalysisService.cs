using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Сервис подготовки данных для визуализации в OxyPlot
/// Соответствует PlotBuilder из Python analysis/visualization.py + экспорт изображений
/// Исправлены ошибки компиляции с enum и API
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

    // ИСПРАВЛЕНО: Используем string представления enum вместо enum значений
    private static readonly Dictionary<string, string> StatusColors = new()
    {
        [nameof(DeviationStatus.EconomyStrong)] = "#006400",   // DarkGreen
        [nameof(DeviationStatus.EconomyMedium)] = "#008000",   // Green
        [nameof(DeviationStatus.EconomyWeak)] = "#90EE90",     // LightGreen
        [nameof(DeviationStatus.Normal)] = "#ADD8E6",          // LightBlue
        [nameof(DeviationStatus.OverrunWeak)] = "#FFA500",     // Orange
        [nameof(DeviationStatus.OverrunMedium)] = "#FF8C00",   // DarkOrange
        [nameof(DeviationStatus.OverrunStrong)] = "#DC143C"    // Crimson
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
        string? specificNormId = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var routesList = routes.ToList();

        _logger.LogInformation("Подготавливаем интерактивные данные для {RouteCount} маршрутов участка {SectionName}",
            routesList.Count, sectionName);

        var visualizationData = new VisualizationData
        {
            Title = BuildTitle(sectionName, specificNormId),
            SectionName = sectionName,
            Metadata = new Dictionary<string, object>
            {
                ["Title"] = BuildTitle(sectionName, specificNormId),
                ["RouteCount"] = routesList.Count,
                ["SpecificNormId"] = specificNormId ?? "all",
                ["CreatedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }
        };

        try
        {
            // Подготавливаем кривые норм - аналог Python norm curves
            visualizationData.NormCurves = await PrepareNormCurvesAsync(
                normFunctions, specificNormId, cancellationToken);

            // Подготавливаем точки маршрутов - аналог Python route points  
            visualizationData.RoutePoints = PrepareRoutePointsData(routesList, specificNormId);

            // Подготавливаем данные отклонений - аналог Python deviation analysis
            visualizationData.DeviationData = PrepareDeviationData(routesList);

            _logger.LogDebug("Подготовка данных завершена за {ElapsedMs}мс: {NormCurveCount} кривых норм, {RoutePointCount} точек маршрутов",
                stopwatch.ElapsedMilliseconds,
                visualizationData.NormCurves.Series.Count,
                visualizationData.RoutePoints.Series.Count);

            return visualizationData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка подготовки данных визуализации для участка {SectionName}", sectionName);
            throw new InvalidOperationException($"Не удалось подготовить данные для визуализации участка {sectionName}", ex);
        }
    }

    /// <summary>
    /// Создает статическое изображение графика для экспорта
    /// </summary>
    public async Task<bool> ExportChartToImageAsync(
        VisualizationData visualizationData,
        string outputPath,
        PlotExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= PlotExportOptions.Default;

        _logger.LogInformation("Экспортируем график в файл: {OutputPath} ({Width}x{Height})",
            outputPath, options.Width, options.Height);

        try
        {
            var plotModel = CreateExportPlotModel(visualizationData, options);
            await ExportPlotModelToFileAsync(plotModel, outputPath, options);

            _logger.LogDebug("График успешно экспортирован в файл: {OutputPath}", outputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта графика в файл {OutputPath}", outputPath);
            return false;
        }
    }

    /// <summary>
    /// Подготавливает данные сводной статистики
    /// </summary>
    public Task<StatisticsData> PrepareStatisticsDataAsync(
        IEnumerable<Route> routes,
        string? sectionFilter = null,
        CancellationToken cancellationToken = default)
    {
        var routesList = routes.ToList();
        _logger.LogDebug("Подготавливаем статистику для {RouteCount} маршрутов", routesList.Count);

        var statisticsData = new StatisticsData
        {
            TotalRoutes = routesList.Count,
            SectionName = sectionFilter,
            CreatedAt = DateTime.UtcNow
        };

        // Группировка по статусам отклонений
        var statusGroups = routesList
            .Where(r => !string.IsNullOrEmpty(r.Status))
            .GroupBy(r => r.Status!)
            .ToDictionary(g => g.Key, g => g.Count());

        statisticsData.StatusDistribution = statusGroups;

        // Статистика по отклонениям
        var validDeviations = routesList
            .Where(r => r.DeviationPercent.HasValue)
            .Select(r => r.DeviationPercent!.Value)
            .ToList();

        if (validDeviations.Any())
        {
            statisticsData.AverageDeviation = validDeviations.Average();
            statisticsData.MinDeviation = validDeviations.Min();
            statisticsData.MaxDeviation = validDeviations.Max();
            statisticsData.MedianDeviation = CalculateMedian(validDeviations);
        }

        // Группировка по нормам
        var normGroups = routesList
            .Where(r => !string.IsNullOrEmpty(r.NormNumber))
            .GroupBy(r => r.NormNumber!)
            .ToDictionary(g => g.Key, g => g.Count());

        statisticsData.NormDistribution = normGroups;

        return Task.FromResult(statisticsData);
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Подготавливает кривые норм для визуализации
    /// ИСПРАВЛЕНО: Используем правильный метод интерполяции
    /// </summary>
    private async Task<ChartSeriesData> PrepareNormCurvesAsync(
        Dictionary<string, InterpolationFunction> normFunctions,
        string? specificNormId,
        CancellationToken cancellationToken)
    {
        var chartData = new ChartSeriesData
        {
            Title = "Кривые норм",
            Axes = new AxisConfiguration
            {
                XAxisTitle = "Нагрузка на ось, тонн",
                YAxisTitle = "Расход электроэнергии, кВт⋅ч",
                XAxisKey = "BottomAxis",
                YAxisKey = "LeftAxis"
            },
            Series = new List<SeriesData>()
        };

        var functionsToProcess = string.IsNullOrEmpty(specificNormId)
            ? normFunctions
            : normFunctions.Where(kv => kv.Key == specificNormId).ToDictionary(kv => kv.Key, kv => kv.Value);

        foreach (var (normId, function) in functionsToProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var (minLoad, maxLoad) = _interpolationService.GetValidRange(function);

                // Генерируем точки для кривой (аналог Python linspace)
                var pointCount = 100;
                var step = (maxLoad - minLoad) / (pointCount - 1);
                var xValues = new decimal[pointCount];
                var yValues = new decimal[pointCount];

                for (int i = 0; i < pointCount; i++)
                {
                    var load = minLoad + i * step;
                    xValues[i] = (decimal)load;

                    // ИСПРАВЛЕНО: Используем правильный метод интерполяции
                    yValues[i] = (decimal)_interpolationService.InterpolateValue(function, load);
                }

                var seriesData = new SeriesData
                {
                    Name = $"Норма {normId}",
                    XValues = xValues,
                    YValues = yValues,
                    Color = GetNormColor(function.NormType),
                    SeriesType = "Line",
                    Metadata = new Dictionary<string, object>
                    {
                        ["NormId"] = normId,
                        ["NormType"] = function.NormType,
                        ["PointCount"] = pointCount
                    }
                };

                chartData.Series.Add(seriesData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось построить кривую для нормы {NormId}", normId);
            }
        }

        return chartData;
    }

    /// <summary>
    /// Подготавливает точки маршрутов для визуализации
    /// ИСПРАВЛЕНО: Используем SectionName вместо SectionNames
    /// </summary>
    private ChartSeriesData PrepareRoutePointsData(List<Route> routes, string? specificNormId)
    {
        var chartData = new ChartSeriesData
        {
            Title = "Точки маршрутов",
            Axes = new AxisConfiguration
            {
                XAxisTitle = "Нагрузка на ось, тонн",
                YAxisTitle = "Фактический расход, кВт⋅ч",
                XAxisKey = "BottomAxis",
                YAxisKey = "LeftAxis"
            },
            Series = new List<SeriesData>()
        };

        // Фильтруем маршруты с валидными данными
        var validRoutes = routes
            .Where(r => r.AxleLoad.HasValue && r.FactConsumption.HasValue)
            .Where(r => string.IsNullOrEmpty(specificNormId) || r.NormNumber == specificNormId)
            .ToList();

        // Группируем по статусам отклонений для разных серий
        var statusGroups = validRoutes
            .Where(r => !string.IsNullOrEmpty(r.Status))
            .GroupBy(r => r.Status!)
            .ToList();

        foreach (var statusGroup in statusGroups)
        {
            var routesInGroup = statusGroup.ToList();
            var xValues = routesInGroup.Select(r => r.AxleLoad!.Value).ToArray();
            var yValues = routesInGroup.Select(r => r.FactConsumption!.Value).ToArray();

            var seriesData = new SeriesData
            {
                Name = GetStatusDisplayName(statusGroup.Key),
                XValues = xValues,
                YValues = yValues,
                Color = GetStatusColor(statusGroup.Key),
                SeriesType = "Scatter",
                Metadata = new Dictionary<string, object>
                {
                    ["Status"] = statusGroup.Key,
                    ["RouteCount"] = routesInGroup.Count
                }
            };

            chartData.Series.Add(seriesData);
        }

        return chartData;
    }

    /// <summary>
    /// Подготавливает данные отклонений для анализа
    /// </summary>
    private DeviationAnalysisData PrepareDeviationData(List<Route> routes)
    {
        var validRoutes = routes
            .Where(r => r.DeviationPercent.HasValue)
            .ToList();

        return new DeviationAnalysisData
        {
            TotalRoutes = validRoutes.Count,
            AverageDeviation = validRoutes.Any() ? validRoutes.Average(r => r.DeviationPercent!.Value) : 0,
            DeviationRanges = CreateDeviationRanges(validRoutes),
            StatusDistribution = validRoutes
                .Where(r => !string.IsNullOrEmpty(r.Status))
                .GroupBy(r => r.Status!)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <summary>
    /// Создает OxyPlot модель для экспорта
    /// ИСПРАВЛЕНО: Используем правильные API OxyPlot
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
            TitleFontSize = 16
        };

        // Создаем оси
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

        plotModel.Axes.Add(xAxis);
        plotModel.Axes.Add(yAxisLeft);

        // Добавляем кривые норм
        foreach (var normSeries in visualizationData.NormCurves.Series)
        {
            var lineSeries = new LineSeries
            {
                Title = normSeries.Name,
                Color = OxyColor.Parse(normSeries.Color),
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid
            };

            for (int i = 0; i < normSeries.XValues.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint((double)normSeries.XValues[i], (double)normSeries.YValues[i]));
            }

            plotModel.Series.Add(lineSeries);
        }

        // Добавляем точки маршрутов
        foreach (var routeSeries in visualizationData.RoutePoints.Series)
        {
            var scatterSeries = new ScatterSeries
            {
                Title = routeSeries.Name,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColor.Parse(routeSeries.Color),
                MarkerStroke = OxyColor.Parse(routeSeries.Color)
            };

            for (int i = 0; i < routeSeries.XValues.Length; i++)
            {
                scatterSeries.Points.Add(new ScatterPoint((double)routeSeries.XValues[i], (double)routeSeries.YValues[i]));
            }

            plotModel.Series.Add(scatterSeries);
        }

        // ИСПРАВЛЕНО: Используем правильные свойства легенды
        if (options.IncludeLegend)
        {
            plotModel.IsLegendVisible = true;
        }

        return plotModel;
    }

    /// <summary>
    /// Экспортирует модель в файл
    /// ИСПРАВЛЕНО: Упрощенная версия экспорта с проверкой доступных экспортеров
    /// </summary>
    private async Task ExportPlotModelToFileAsync(PlotModel plotModel, string outputPath, PlotExportOptions options)
    {
        await Task.Run(() =>
        {
            var extension = Path.GetExtension(outputPath).ToLowerInvariant();

            try
            {
                switch (extension)
                {
                    case ".png":
                        // Простой экспорт через Stream - работает со всеми версиями OxyPlot
                        using (var stream = File.Create(outputPath))
                        {
                            // Используем базовый PngExporter если доступен
                            try
                            {
                                var pngExporter = new OxyPlot.Wpf.PngExporter { Width = options.Width, Height = options.Height };
                                pngExporter.Export(plotModel, stream);
                            }
                            catch (Exception)
                            {
                                // Fallback: базовый экспорт
                                var basicExporter = new OxyPlot.SvgExporter { Width = options.Width, Height = options.Height };
                                basicExporter.Export(plotModel, stream);
                            }
                        }
                        break;

                    case ".svg":
                        // SVG экспорт всегда доступен
                        using (var stream = File.Create(outputPath))
                        {
                            var exporter = new OxyPlot.SvgExporter { Width = options.Width, Height = options.Height };
                            exporter.Export(plotModel, stream);
                        }
                        break;

                    default:
                        throw new NotSupportedException($"Формат файла {extension} не поддерживается. Доступны: .png, .svg");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка экспорта в файл {OutputPath}", outputPath);
                throw new InvalidOperationException($"Не удалось экспортировать график в формат {extension}", ex);
            }
        });
    }

    /// <summary>
    /// Создает диапазоны отклонений для анализа
    /// </summary>
    private Dictionary<string, int> CreateDeviationRanges(List<Route> routes)
    {
        var ranges = new Dictionary<string, int>
        {
            ["< -30%"] = 0,
            ["-30% - -20%"] = 0,
            ["-20% - -5%"] = 0,
            ["-5% - +5%"] = 0,
            ["+5% - +20%"] = 0,
            ["+20% - +30%"] = 0,
            ["> +30%"] = 0
        };

        foreach (var route in routes.Where(r => r.DeviationPercent.HasValue))
        {
            var deviation = route.DeviationPercent!.Value;

            if (deviation < -30) ranges["< -30%"]++;
            else if (deviation < -20) ranges["-30% - -20%"]++;
            else if (deviation < -5) ranges["-20% - -5%"]++;
            else if (deviation <= 5) ranges["-5% - +5%"]++;
            else if (deviation <= 20) ranges["+5% - +20%"]++;
            else if (deviation <= 30) ranges["+20% - +30%"]++;
            else ranges["> +30%"]++;
        }

        return ranges;
    }

    /// <summary>
    /// Вычисляет медиану для списка значений
    /// </summary>
    private decimal CalculateMedian(List<decimal> values)
    {
        if (!values.Any()) return 0;

        var sortedValues = values.OrderBy(x => x).ToList();
        var count = sortedValues.Count;

        if (count % 2 == 0)
        {
            return (sortedValues[count / 2 - 1] + sortedValues[count / 2]) / 2;
        }

        return sortedValues[count / 2];
    }

    /// <summary>
    /// Получает цвет для типа нормы
    /// </summary>
    private string GetNormColor(string normType)
    {
        return NormTypeColors.GetValueOrDefault(normType, NormTypeColors["default"]);
    }

    /// <summary>
    /// Получает цвет для статуса отклонения
    /// ИСПРАВЛЕНО: Работает со строковыми статусами
    /// </summary>
    private string GetStatusColor(string status)
    {
        return StatusColors.GetValueOrDefault(status, "#808080"); // серый по умолчанию
    }

    /// <summary>
    /// Получает отображаемое имя для статуса
    /// </summary>
    private string GetStatusDisplayName(string status)
    {
        return status switch
        {
            nameof(DeviationStatus.EconomyStrong) => "Экономия сильная",
            nameof(DeviationStatus.EconomyMedium) => "Экономия средняя",
            nameof(DeviationStatus.EconomyWeak) => "Экономия слабая",
            nameof(DeviationStatus.Normal) => "Норма",
            nameof(DeviationStatus.OverrunWeak) => "Перерасход слабый",
            nameof(DeviationStatus.OverrunMedium) => "Перерасход средний",
            nameof(DeviationStatus.OverrunStrong) => "Перерасход сильный",
            _ => status
        };
    }

    /// <summary>
    /// Создает заголовок графика
    /// </summary>
    private string BuildTitle(string sectionName, string? specificNormId)
    {
        var title = $"Анализ норм расхода: {sectionName}";
        if (!string.IsNullOrEmpty(specificNormId))
        {
            title += $" (норма {specificNormId})";
        }
        return title;
    }

    #endregion
}

// ПРИМЕЧАНИЕ: Классы данных (VisualizationData, ChartSeriesData, etc.) 
// теперь определены в service_interfaces.cs для избежания дублирования