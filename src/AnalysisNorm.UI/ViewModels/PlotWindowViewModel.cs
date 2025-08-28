using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.UI.Commands;

namespace AnalysisNorm.UI.ViewModels;

/// <summary>
/// ViewModel для окна графиков - аналог Python PlotBuilder класса
/// Реализует dual subplot визуализацию с интерактивными возможностями
/// </summary>
public class PlotWindowViewModel : INotifyPropertyChanged, IDisposable
{
    #region Поля - аналогично Python PlotBuilder instance variables

    private readonly ILogger<PlotWindowViewModel> _logger;
    private readonly IVisualizationDataService _visualizationService;
    private readonly IDataAnalysisService _dataAnalysisService;

    // Данные для графиков - аналог Python routes_df + norm_functions
    private List<Route> _routesData = new();
    private Dictionary<string, object> _normFunctions = new();
    private AnalysisResult? _analysisResult;

    // UI состояние графиков
    private PlotModel _normsPlotModel = new();
    private PlotModel _deviationsPlotModel = new();
    private bool _isLoadingPlot;
    private string _plotLoadingMessage = string.Empty;
    private double _plotLoadingProgress;
    private string _selectedDisplayMode = "Удельный расход";
    private bool _showDataPoints = true;
    private bool _showLegend = true;
    private string _selectedPointInfo = string.Empty;

    // Параметры анализа - аналог Python method parameters
    private readonly string _sectionName;
    private readonly string? _specificNormId;
    private readonly bool _singleSectionOnly;

    #endregion

    #region Конструктор

    /// <summary>
    /// Конструктор ViewModel графиков
    /// Аналог Python PlotBuilder.__init__() + create_interactive_plot параметры
    /// </summary>
    public PlotWindowViewModel(
        ILogger<PlotWindowViewModel> logger,
        IVisualizationDataService visualizationService,
        IDataAnalysisService dataAnalysisService,
        string sectionName,
        List<Route> routesData,
        Dictionary<string, object> normFunctions,
        string? specificNormId = null,
        bool singleSectionOnly = false)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _visualizationService = visualizationService ?? throw new ArgumentNullException(nameof(visualizationService));
        _dataAnalysisService = dataAnalysisService ?? throw new ArgumentNullException(nameof(dataAnalysisService));

        _sectionName = sectionName;
        _routesData = routesData ?? throw new ArgumentNullException(nameof(routesData));
        _normFunctions = normFunctions ?? throw new ArgumentNullException(nameof(normFunctions));
        _specificNormId = specificNormId;
        _singleSectionOnly = singleSectionOnly;

        // Инициализируем коллекции
        DisplayModes = new ObservableCollection<string> { "Удельный расход", "Норма/Факт" };

        // Генерируем заголовки - аналог Python title generation
        GenerateTitles();

        // Инициализируем команды
        InitializeCommands();

        _logger.LogInformation("PlotWindowViewModel инициализирован для участка: {SectionName}", _sectionName);
    }

    #endregion

    #region Properties для UI Binding

    /// <summary>
    /// Заголовок окна - аналог Python window title
    /// </summary>
    public string WindowTitle { get; private set; } = string.Empty;

    /// <summary>
    /// Заголовок общего графика - аналог Python plot title
    /// </summary>
    public string PlotTitle { get; private set; } = string.Empty;

    /// <summary>
    /// Заголовок графика норм - аналог Python subplot 1 title
    /// </summary>
    public string NormsPlotTitle { get; private set; } = string.Empty;

    /// <summary>
    /// Заголовок графика отклонений - аналог Python subplot 2 title
    /// </summary>
    public string DeviationsPlotTitle { get; private set; } = string.Empty;

    /// <summary>
    /// Модель графика норм - OxyPlot аналог Python subplot 1
    /// </summary>
    public PlotModel NormsPlotModel
    {
        get => _normsPlotModel;
        set => SetProperty(ref _normsPlotModel, value);
    }

    /// <summary>
    /// Модель графика отклонений - OxyPlot аналог Python subplot 2
    /// </summary>
    public PlotModel DeviationsPlotModel
    {
        get => _deviationsPlotModel;
        set => SetProperty(ref _deviationsPlotModel, value);
    }

    /// <summary>
    /// Состояние загрузки графика
    /// </summary>
    public bool IsLoadingPlot
    {
        get => _isLoadingPlot;
        set => SetProperty(ref _isLoadingPlot, value);
    }

    /// <summary>
    /// Сообщение во время загрузки графика
    /// </summary>
    public string PlotLoadingMessage
    {
        get => _plotLoadingMessage;
        set => SetProperty(ref _plotLoadingMessage, value);
    }

    /// <summary>
    /// Прогресс загрузки графика
    /// </summary>
    public double PlotLoadingProgress
    {
        get => _plotLoadingProgress;
        set => SetProperty(ref _plotLoadingProgress, value);
    }

    /// <summary>
    /// Режим отображения данных - аналог Python norm types
    /// </summary>
    public string SelectedDisplayMode
    {
        get => _selectedDisplayMode;
        set
        {
            if (SetProperty(ref _selectedDisplayMode, value))
            {
                // Обновляем график при смене режима - аналог Python режим переключения
                _ = UpdatePlotsForDisplayModeAsync();
            }
        }
    }

    /// <summary>
    /// Показывать точки данных на графике
    /// </summary>
    public bool ShowDataPoints
    {
        get => _showDataPoints;
        set
        {
            if (SetProperty(ref _showDataPoints, value))
            {
                UpdatePointsVisibility();
            }
        }
    }

    /// <summary>
    /// Показывать легенду графиков
    /// </summary>
    public bool ShowLegend
    {
        get => _showLegend;
        set
        {
            if (SetProperty(ref _showLegend, value))
            {
                UpdateLegendVisibility();
            }
        }
    }

    /// <summary>
    /// Информация о выбранной точке - аналог Python hover information
    /// </summary>
    public string SelectedPointInfo
    {
        get => _selectedPointInfo;
        set => SetProperty(ref _selectedPointInfo, value);
    }

    /// <summary>
    /// Информация о данных графика
    /// </summary>
    public string DataInfo { get; private set; } = string.Empty;

    /// <summary>
    /// Доступные режимы отображения
    /// </summary>
    public ObservableCollection<string> DisplayModes { get; }

    #endregion

    #region Commands

    public ICommand ResetZoomCommand { get; private set; } = null!;
    public ICommand ExportImageCommand { get; private set; } = null!;
    public ICommand ShowDataTableCommand { get; private set; } = null!;

    #endregion

    #region Инициализация

    /// <summary>
    /// Инициализирует команды управления графиком
    /// </summary>
    private void InitializeCommands()
    {
        ResetZoomCommand = new RelayCommand(ResetZoom);
        ExportImageCommand = new AsyncCommand(ExportImageAsync);
        ShowDataTableCommand = new RelayCommand(ShowDataTable);
    }

    /// <summary>
    /// Генерирует заголовки графиков - аналог Python title generation
    /// </summary>
    private void GenerateTitles()
    {
        var titleSuffix = !string.IsNullOrEmpty(_specificNormId) ? $" (норма {_specificNormId})" : "";
        var filterSuffix = _singleSectionOnly ? " [только один участок]" : "";

        WindowTitle = $"График анализа - {_sectionName}{titleSuffix}{filterSuffix}";
        PlotTitle = $"Участок: {_sectionName}{titleSuffix}{filterSuffix}";
        
        // Аналог Python subplot_titles
        NormsPlotTitle = $"Нормы расхода для участка: {_sectionName}{titleSuffix}{filterSuffix}";
        DeviationsPlotTitle = "Отклонение фактического расхода от нормы";

        DataInfo = $"Маршрутов: {_routesData.Count} | Норм: {_normFunctions.Count}";
    }

    #endregion

    #region Главный метод построения графиков

    /// <summary>
    /// Асинхронная инициализация и построение графиков
    /// Полный аналог Python create_interactive_plot()
    /// </summary>
    public async Task InitializePlotsAsync()
    {
        try
        {
            IsLoadingPlot = true;
            PlotLoadingMessage = "Подготовка данных графика...";
            PlotLoadingProgress = 10;

            _logger.LogInformation("Начато построение графиков для участка: {SectionName}", _sectionName);

            // Шаг 1: Создаем базовую структуру графиков - аналог Python _create_base_structure
            PlotLoadingMessage = "Создание структуры графиков...";
            PlotLoadingProgress = 30;
            await CreateBasePlotStructureAsync();

            // Шаг 2: Добавляем кривые норм - аналог Python _add_norm_curves  
            PlotLoadingMessage = "Построение кривых норм...";
            PlotLoadingProgress = 50;
            await AddNormCurvesToPlotsAsync();

            // Шаг 3: Добавляем точки маршрутов - аналог Python _add_route_points
            PlotLoadingMessage = "Добавление точек маршрутов...";
            PlotLoadingProgress = 70;
            await AddRoutePointsToPlotsAsync();

            // Шаг 4: Добавляем анализ отклонений - аналог Python _add_deviation_analysis
            PlotLoadingMessage = "Анализ отклонений...";
            PlotLoadingProgress = 90;
            await AddDeviationAnalysisAsync();

            // Шаг 5: Финальная конфигурация - аналог Python _configure_layout
            PlotLoadingMessage = "Финализация графиков...";
            PlotLoadingProgress = 100;
            ConfigureFinalLayout();

            _logger.LogInformation("Графики успешно построены");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка построения графиков");
            throw;
        }
        finally
        {
            IsLoadingPlot = false;
            PlotLoadingMessage = string.Empty;
            PlotLoadingProgress = 0;
        }
    }

    #endregion

    #region Методы построения графиков - аналоги Python PlotBuilder methods

    /// <summary>
    /// Создает базовую структуру графиков - аналог Python _create_base_structure
    /// </summary>
    private async Task CreateBasePlotStructureAsync()
    {
        await Task.Run(() =>
        {
            // График норм расхода (верхний)
            var normsModel = new PlotModel
            {
                Title = null, // Заголовок будет в XAML
                Background = OxyColors.White,
                PlotAreaBorderThickness = new OxyThickness(1),
                PlotAreaBorderColor = OxyColors.Gray
            };

            // График отклонений (нижний) 
            var deviationsModel = new PlotModel
            {
                Title = null,
                Background = OxyColors.White,
                PlotAreaBorderThickness = new OxyThickness(1),
                PlotAreaBorderColor = OxyColors.Gray
            };

            // Настраиваем оси для графика норм - аналог Python axes configuration
            SetupNormsAxes(normsModel);
            SetupDeviationsAxes(deviationsModel);

            // Обновляем модели в UI потоке
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                NormsPlotModel = normsModel;
                DeviationsPlotModel = deviationsModel;
            });
        });
    }

    /// <summary>
    /// Настраивает оси для графика норм - аналог Python axes setup
    /// </summary>
    private void SetupNormsAxes(PlotModel model)
    {
        // Ось X: механическая работа (кВт*час) - аналог Python X axis
        var xAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Механическая работа (кВт⋅час)",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColors.LightGray,
            MinorGridlineColor = OxyColors.LightGray,
            FontSize = 11
        };
        model.Axes.Add(xAxis);

        // Ось Y: расход электроэнергии - аналог Python Y axis
        var yAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Расход электроэнергии (кВт⋅час)",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColors.LightGray,
            MinorGridlineColor = OxyColors.LightGray,
            FontSize = 11
        };
        model.Axes.Add(yAxis);
    }

    /// <summary>
    /// Настраивает оси для графика отклонений - аналог Python deviation axes
    /// </summary>
    private void SetupDeviationsAxes(PlotModel model)
    {
        // Ось X: механическая работа (общая с верхним графиком)
        var xAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Механическая работа (кВт⋅час)",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColors.LightGray,
            MinorGridlineColor = OxyColors.LightGray,
            FontSize = 11
        };
        model.Axes.Add(xAxis);

        // Ось Y: отклонение в процентах
        var yAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Отклонение от нормы (%)",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColors.LightGray,
            MinorGridlineColor = OxyColors.LightGray,
            FontSize = 11
        };
        model.Axes.Add(yAxis);

        // Добавляем горизонтальную линию нулевого отклонения
        var zeroLine = new FunctionSeries(x => 0, 0, 10000, 0.1)
        {
            Color = OxyColors.Gray,
            StrokeThickness = 1,
            LineStyle = LineStyle.Dash,
            Title = "Нулевое отклонение"
        };
        model.Series.Add(zeroLine);
    }

    /// <summary>
    /// Добавляет кривые норм на графики - аналог Python _add_norm_curves
    /// </summary>
    private async Task AddNormCurvesToPlotsAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                foreach (var normFunction in _normFunctions)
                {
                    // Если анализируем конкретную норму, показываем только её
                    if (!string.IsNullOrEmpty(_specificNormId) && 
                        normFunction.Key != _specificNormId)
                        continue;

                    AddSingleNormCurve(normFunction.Key, (Dictionary<string, object>)normFunction.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка добавления кривых норм");
            }
        });
    }

    /// <summary>
    /// Добавляет одну кривую нормы - аналог Python single norm processing
    /// </summary>
    private void AddSingleNormCurve(string normId, Dictionary<string, object> normData)
    {
        try
        {
            // Извлекаем точки нормы - аналог Python norm points extraction
            if (!normData.ContainsKey("points") || 
                normData["points"] is not List<NormPoint> normPoints ||
                !normPoints.Any())
                return;

            // Создаем интерполированную кривую для плавного отображения
            var normSeries = CreateInterpolatedNormCurve(normId, normPoints);

            // Добавляем серию в модель в UI потоке
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                NormsPlotModel.Series.Add(normSeries);
                NormsPlotModel.InvalidatePlot(true);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка добавления кривой нормы {NormId}", normId);
        }
    }

    /// <summary>
    /// Добавляет точки маршрутов на графики - аналог Python _add_route_points
    /// </summary>
    private async Task AddRoutePointsToPlotsAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                // Группируем маршруты по статусу отклонения - аналог Python status classification
                var routesByStatus = _routesData
                    .Where(r => r.MechanicalWork > 0 && r.ElectricConsumption > 0)
                    .GroupBy(r => ClassifyDeviation(r))
                    .ToList();

                foreach (var statusGroup in routesByStatus)
                {
                    AddRoutePointsForStatus(statusGroup.Key, statusGroup.ToList());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка добавления точек маршрутов");
            }
        });
    }

    /// <summary>
    /// Добавляет точки маршрутов для конкретного статуса отклонения
    /// </summary>
    private void AddRoutePointsForStatus(string status, List<Route> routes)
    {
        if (!routes.Any()) return;

        // Создаем серию точек - аналог Python scatter series
        var pointSeries = new ScatterSeries
        {
            Title = $"{status} ({routes.Count})",
            MarkerType = MarkerType.Circle,
            MarkerSize = 4,
            MarkerFill = GetStatusColor(status),
            MarkerStroke = OxyColors.DarkGray,
            MarkerStrokeThickness = 0.5
        };

        // Добавляем точки маршрутов
        foreach (var route in routes)
        {
            pointSeries.Points.Add(new ScatterPoint(
                route.MechanicalWork, 
                route.ElectricConsumption,
                tag: route // Сохраняем ссылку на маршрут для hover
            ));
        }

        // Добавляем серию в модель
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            NormsPlotModel.Series.Add(pointSeries);
        });
    }

    /// <summary>
    /// Добавляет анализ отклонений на нижний график - аналог Python _add_deviation_analysis
    /// </summary>
    private async Task AddDeviationAnalysisAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                // Группируем маршруты по статусу для графика отклонений
                var routesByStatus = _routesData
                    .Where(r => r.MechanicalWork > 0)
                    .GroupBy(r => ClassifyDeviation(r))
                    .ToList();

                foreach (var statusGroup in routesByStatus)
                {
                    AddDeviationPointsForStatus(statusGroup.Key, statusGroup.ToList());
                }

                // Добавляем границы зон отклонений - аналог Python zone boundaries
                AddDeviationZoneBoundaries();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка анализа отклонений");
            }
        });
    }

    /// <summary>
    /// Добавляет точки отклонений для конкретного статуса
    /// </summary>
    private void AddDeviationPointsForStatus(string status, List<Route> routes)
    {
        if (!routes.Any()) return;

        var pointSeries = new ScatterSeries
        {
            Title = $"{status} ({routes.Count})",
            MarkerType = MarkerType.Circle,
            MarkerSize = 4,
            MarkerFill = GetStatusColor(status),
            MarkerStroke = OxyColors.DarkGray,
            MarkerStrokeThickness = 0.5
        };

        // Добавляем точки отклонений
        foreach (var route in routes)
        {
            pointSeries.Points.Add(new ScatterPoint(
                route.MechanicalWork,
                route.DeviationPercent,
                tag: route
            ));
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            DeviationsPlotModel.Series.Add(pointSeries);
        });
    }

    /// <summary>
    /// Добавляет границы зон отклонений - аналог Python zone indicators
    /// </summary>
    private void AddDeviationZoneBoundaries()
    {
        var boundaries = new[] { -30, -20, -5, 5, 20, 30 };
        
        foreach (var boundary in boundaries)
        {
            var line = new FunctionSeries(x => boundary, 0, 10000, 0.1)
            {
                Color = OxyColors.LightGray,
                StrokeThickness = 1,
                LineStyle = LineStyle.Dot,
                Title = null // Не показываем в легенде
            };

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                DeviationsPlotModel.Series.Add(line);
            });
        }
    }

    /// <summary>
    /// Финальная конфигурация графиков - аналог Python _configure_layout
    /// </summary>
    private void ConfigureFinalLayout()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // Настраиваем автомасштабирование
            foreach (var axis in NormsPlotModel.Axes)
            {
                axis.Reset();
            }
            
            foreach (var axis in DeviationsPlotModel.Axes)
            {
                axis.Reset();
            }

            // Настраиваем легенду - аналог Python legend configuration
            NormsPlotModel.LegendPosition = ShowLegend ? LegendPosition.RightTop : LegendPosition.None;
            DeviationsPlotModel.LegendPosition = ShowLegend ? LegendPosition.RightTop : LegendPosition.None;

            // Настраиваем интерактивность - аналог Python hover/click setup
            NormsPlotModel.MouseDown += (sender, e) => HandlePlotMouseDown(NormsPlotModel, e);
            DeviationsPlotModel.MouseDown += (sender, e) => HandlePlotMouseDown(DeviationsPlotModel, e);

            // Финальное обновление графиков
            NormsPlotModel.InvalidatePlot(true);
            DeviationsPlotModel.InvalidatePlot(true);
        });
    }

    /// <summary>
    /// Обрабатывает клик по графику - универсальный handler
    /// </summary>
    private void HandlePlotMouseDown(PlotModel plotModel, OxyMouseDownEventArgs e)
    {
        try
        {
            if (e.ClickCount == 2) // Двойной клик для детальной информации
            {
                var hit = plotModel.HitTest(new HitTestArguments(e.Position, 10));
                
                if (hit?.Element is ScatterSeries series && hit.Index >= 0)
                {
                    var point = series.Points[hit.Index];
                    if (point.Tag is Route route)
                    {
                        ShowRouteDetailsDialog(route);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки клика по графику");
        }
    }

    /// <summary>
    /// Создает интерполированную кривую нормы - аналог Python scipy interpolation
    /// </summary>
    private LineSeries CreateInterpolatedNormCurve(string normId, List<NormPoint> normPoints)
    {
        try
        {
            // Подготавливаем данные для интерполяции
            var sortedPoints = normPoints
                .Where(p => p.MechanicalWork > 0 && p.ElectricConsumption > 0)
                .OrderBy(p => p.MechanicalWork)
                .ToList();

            if (sortedPoints.Count < 2)
            {
                _logger.LogWarning("Недостаточно точек для интерполяции нормы {NormId}", normId);
                return CreateSimpleLineSeries(normId, sortedPoints);
            }

            // Создаем интерполированную кривую с высоким разрешением
            var minWork = sortedPoints.First().MechanicalWork;
            var maxWork = sortedPoints.Last().MechanicalWork;
            var step = (maxWork - minWork) / 200; // 200 точек для плавной кривой

            var interpolatedSeries = new LineSeries
            {
                Title = $"Норма {normId}",
                Color = GetNormColor(normId),
                StrokeThickness = 2.5,
                Smooth = true
            };

            // Простая линейная интерполяция (можно заменить на сплайн-интерполяцию)
            for (double work = minWork; work <= maxWork; work += step)
            {
                var consumption = InterpolateConsumption(sortedPoints, work);
                interpolatedSeries.Points.Add(new DataPoint(work, consumption));
            }

            return interpolatedSeries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания интерполированной кривой для нормы {NormId}", normId);
            return CreateSimpleLineSeries(normId, normPoints);
        }
    }

    /// <summary>
    /// Создает простую линейную серию без интерполяции
    /// </summary>
    private LineSeries CreateSimpleLineSeries(string normId, List<NormPoint> points)
    {
        var series = new LineSeries
        {
            Title = $"Норма {normId}",
            Color = GetNormColor(normId),
            StrokeThickness = 2
        };

        foreach (var point in points.OrderBy(p => p.MechanicalWork))
        {
            series.Points.Add(new DataPoint(point.MechanicalWork, point.ElectricConsumption));
        }

        return series;
    }

    /// <summary>
    /// Интерполяция потребления для заданной механической работы
    /// Простая линейная интерполяция между ближайшими точками
    /// </summary>
    private double InterpolateConsumption(List<NormPoint> sortedPoints, double targetWork)
    {
        // Находим ближайшие точки
        var beforePoint = sortedPoints.LastOrDefault(p => p.MechanicalWork <= targetWork);
        var afterPoint = sortedPoints.FirstOrDefault(p => p.MechanicalWork >= targetWork);

        if (beforePoint == null) return afterPoint?.ElectricConsumption ?? 0;
        if (afterPoint == null) return beforePoint.ElectricConsumption;
        if (Math.Abs(beforePoint.MechanicalWork - afterPoint.MechanicalWork) < 0.001) 
            return beforePoint.ElectricConsumption;

        // Линейная интерполяция
        var ratio = (targetWork - beforePoint.MechanicalWork) / 
                   (afterPoint.MechanicalWork - beforePoint.MechanicalWork);
        
        return beforePoint.ElectricConsumption + 
               ratio * (afterPoint.ElectricConsumption - beforePoint.ElectricConsumption);
    }

    #endregion

    #region Вспомогательные методы

    /// <summary>
    /// Классифицирует отклонение маршрута - аналог Python StatusClassifier
    /// </summary>
    private string ClassifyDeviation(Route route)
    {
        // Реализация классификации на основе отклонения от нормы
        var deviation = route.DeviationPercent;
        
        return deviation switch
        {
            < -30 => "Экономия сильная",
            < -20 => "Экономия средняя", 
            < -5 => "Экономия слабая",
            <= 5 => "В норме",
            <= 20 => "Перерасход слабый",
            <= 30 => "Перерасход средний",
            _ => "Перерасход сильный"
        };
    }

    /// <summary>
    /// Получает цвет для нормы по ID
    /// </summary>
    private OxyColor GetNormColor(string normId)
    {
        // Генерируем консистентные цвета для норм
        var hash = normId.GetHashCode();
        var colors = new[] { 
            OxyColors.Blue, OxyColors.Red, OxyColors.Green, 
            OxyColors.Purple, OxyColors.Orange, OxyColors.Brown 
        };
        return colors[Math.Abs(hash) % colors.Length];
    }

    /// <summary>
    /// Получает цвет для статуса отклонения - аналог Python status colors
    /// </summary>
    private OxyColor GetStatusColor(string status)
    {
        return status switch
        {
            "Экономия сильная" => OxyColors.DarkGreen,
            "Экономия средняя" => OxyColors.Green,
            "Экономия слабая" => OxyColors.LightGreen,
            "В норме" => OxyColors.Blue,
            "Перерасход слабый" => OxyColors.Orange,
            "Перерасход средний" => OxyColors.OrangeRed,
            "Перерасход сильный" => OxyColors.Red,
            _ => OxyColors.Gray
        };
    }

    #endregion

    #region Commands Implementation

    private void ResetZoom()
    {
        NormsPlotModel.ResetAllAxes();
        DeviationsPlotModel.ResetAllAxes();
        NormsPlotModel.InvalidatePlot(true);
        DeviationsPlotModel.InvalidatePlot(true);
    }

    private async Task ExportImageAsync(object? parameter)
    {
        // Реализация экспорта изображения
        _logger.LogInformation("Экспорт графика в изображение...");
    }

    private void ShowDataTable()
    {
        // Реализация показа таблицы данных
        _logger.LogInformation("Показ таблицы данных...");
    }

    private async Task UpdatePlotsForDisplayModeAsync()
    {
        // Обновление графиков при смене режима отображения
        await Task.CompletedTask;
    }

    private void UpdatePointsVisibility()
    {
        // Обновление видимости точек
    }

    private void UpdateLegendVisibility()
    {
        // Обновление видимости легенды
    }

    #endregion

    #region Mouse Event Handlers - аналоги Python hover/click

    /// <summary>
    /// Обработчик клика по графику норм - аналог Python plot click handlers
    /// </summary>
    public void OnNormsPlotMouseDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (e.ClickCount == 2) // Двойной клик для детальной информации
            {
                var plotView = e.Source as OxyPlot.Wpf.PlotView;
                var position = e.GetPosition(plotView);
                
                // Преобразуем позицию мыши в координаты графика
                var screenPoint = new ScreenPoint(position.X, position.Y);
                var hit = NormsPlotModel.HitTest(new HitTestArguments(screenPoint, 10));
                
                if (hit?.Element is ScatterSeries series && hit.Index >= 0)
                {
                    var point = series.Points[hit.Index];
                    if (point.Tag is Route route)
                    {
                        ShowRouteDetailsDialog(route);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки клика по графику норм");
        }
    }

    /// <summary>
    /// Обработчик движения мыши по графику норм - аналог Python hover handlers
    /// </summary>
    public void OnNormsPlotMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        try
        {
            var plotView = e.Source as OxyPlot.Wpf.PlotView;
            var position = e.GetPosition(plotView);
            var screenPoint = new ScreenPoint(position.X, position.Y);
            
            // Ищем ближайшую точку для hover эффекта
            var hit = NormsPlotModel.HitTest(new HitTestArguments(screenPoint, 15));
            
            if (hit?.Element is ScatterSeries series && hit.Index >= 0)
            {
                var point = series.Points[hit.Index];
                if (point.Tag is Route route)
                {
                    // Обновляем информацию о выбранной точке - аналог Python hover info
                    SelectedPointInfo = $"Маршрут {route.RouteNumber}: {route.MechanicalWork:F0} кВт⋅час → {route.ElectricConsumption:F0} кВт⋅час ({route.DeviationPercent:+F1}%)";
                }
            }
            else
            {
                SelectedPointInfo = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки hover графика норм");
        }
    }

    /// <summary>
    /// Обработчик клика по графику отклонений - аналог Python deviation click handlers
    /// </summary>
    public void OnDeviationsPlotMouseDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (e.ClickCount == 2) // Двойной клик
            {
                var plotView = e.Source as OxyPlot.Wpf.PlotView;
                var position = e.GetPosition(plotView);
                var screenPoint = new ScreenPoint(position.X, position.Y);
                var hit = DeviationsPlotModel.HitTest(new HitTestArguments(screenPoint, 10));
                
                if (hit?.Element is ScatterSeries series && hit.Index >= 0)
                {
                    var point = series.Points[hit.Index];
                    if (point.Tag is Route route)
                    {
                        ShowRouteDetailsDialog(route);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки клика по графику отклонений");
        }
    }

    /// <summary>
    /// Обработчик движения мыши по графику отклонений
    /// </summary>
    public void OnDeviationsPlotMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        try
        {
            var plotView = e.Source as OxyPlot.Wpf.PlotView;
            var position = e.GetPosition(plotView);
            var screenPoint = new ScreenPoint(position.X, position.Y);
            var hit = DeviationsPlotModel.HitTest(new HitTestArguments(screenPoint, 15));
            
            if (hit?.Element is ScatterSeries series && hit.Index >= 0)
            {
                var point = series.Points[hit.Index];
                if (point.Tag is Route route)
                {
                    SelectedPointInfo = $"Маршрут {route.RouteNumber}: отклонение {route.DeviationPercent:+F1}% от нормы";
                }
            }
            else
            {
                SelectedPointInfo = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки hover графика отклонений");
        }
    }

    /// <summary>
    /// Показывает модальное окно с детальной информацией о маршруте
    /// Аналог Python modal dialog с детальными данными
    /// </summary>
    private void ShowRouteDetailsDialog(Route route)
    {
        try
        {
            var details = new System.Text.StringBuilder();
            
            details.AppendLine($"ДЕТАЛЬНАЯ ИНФОРМАЦИЯ О МАРШРУТЕ");
            details.AppendLine("=".PadRight(50, '='));
            details.AppendLine();
            details.AppendLine($"Номер маршрута: {route.RouteNumber}");
            details.AppendLine($"Участки: {string.Join(" → ", route.SectionNames)}");
            details.AppendLine($"Локомотив: {route.LocomotiveSeries} (тип: {route.LocomotiveType})");
            details.AppendLine($"Норма ID: {route.NormId}");
            details.AppendLine();
            details.AppendLine("ЭНЕРГЕТИЧЕСКИЕ ПОКАЗАТЕЛИ:");
            details.AppendLine("-".PadRight(30, '-'));
            details.AppendLine($"Механическая работа: {route.MechanicalWork:F2} кВт⋅час");
            details.AppendLine($"Расход электроэнергии: {route.ElectricConsumption:F2} кВт⋅час");
            details.AppendLine($"Удельный расход: {route.SpecificConsumption:F3} кВт⋅час/ткм⋅км");
            details.AppendLine($"Отклонение от нормы: {route.DeviationPercent:+F1}%");
            details.AppendLine();
            details.AppendLine("ЭКСПЛУАТАЦИОННЫЕ ДАННЫЕ:");
            details.AppendLine("-".PadRight(30, '-'));
            details.AppendLine($"Масса состава: {route.TrainWeight:F0} т");
            details.AppendLine($"Расстояние: {route.Distance:F1} км");
            details.AppendLine($"Время поездки: {route.TravelTime}");
            
            if (route.WeatherConditions != null)
            {
                details.AppendLine($"Погодные условия: {route.WeatherConditions}");
            }
            
            if (!string.IsNullOrEmpty(route.Comments))
            {
                details.AppendLine();
                details.AppendLine("КОММЕНТАРИИ:");
                details.AppendLine("-".PadRight(15, '-'));
                details.AppendLine(route.Comments);
            }

            // Показываем диалог в UI потоке
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var window = System.Windows.Application.Current.Windows
                    .OfType<Windows.PlotWindow>()
                    .FirstOrDefault();
                
                window?.ShowPointDetailsDialog(
                    $"Маршрут {route.RouteNumber}",
                    details.ToString()
                );
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка показа детальной информации о маршруте");
        }
    }

    #endregion

    #region Commands Implementation - завершение

    /// <summary>
    /// Экспорт графика в изображение - аналог Python export plot
    /// </summary>
    private async Task ExportImageAsync(object? parameter)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Экспорт графика",
                Filter = "PNG изображения (*.png)|*.png|JPEG изображения (*.jpg)|*.jpg|Все файлы (*.*)|*.*",
                DefaultExt = "png",
                FileName = $"График_{_sectionName}_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                await ExportToImageFile(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта изображения");
            System.Windows.MessageBox.Show(
                $"Ошибка экспорта: {ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    /// <summary>
    /// Экспорт в файл изображения
    /// </summary>
    private async Task ExportToImageFile(string filePath)
    {
        await Task.Run(() =>
        {
            try
            {
                // Экспорт OxyPlot в PNG
                var width = 1200;
                var height = 800;

                // Создаем объединенный график для экспорта
                var exportModel = new PlotModel
                {
                    Title = PlotTitle,
                    Background = OxyColors.White
                };

                // Копируем серии из обоих графиков
                foreach (var series in NormsPlotModel.Series)
                {
                    exportModel.Series.Add(series);
                }

                // Копируем оси
                foreach (var axis in NormsPlotModel.Axes)
                {
                    exportModel.Axes.Add(axis);
                }

                // Экспортируем в файл
                var exporter = new OxyPlot.Wpf.PngExporter { Width = width, Height = height };
                exporter.ExportToFile(exportModel, filePath);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show(
                        $"График успешно экспортирован:\n{filePath}",
                        "Экспорт завершен",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка записи файла изображения");
                throw;
            }
        });
    }

    /// <summary>
    /// Показывает таблицу данных - аналог Python data table view
    /// </summary>
    private void ShowDataTable()
    {
        try
        {
            var dataTableContent = new System.Text.StringBuilder();
            
            dataTableContent.AppendLine("ТАБЛИЦА ДАННЫХ АНАЛИЗА");
            dataTableContent.AppendLine("=".PadRight(80, '='));
            dataTableContent.AppendLine();
            
            dataTableContent.AppendLine($"{"№ Маршрута",-12} | {"Мех. работа",-12} | {"Электр. расх.",-13} | {"Отклонение",-12} | {"Статус",-15}");
            dataTableContent.AppendLine("-".PadRight(80, '-'));
            
            foreach (var route in _routesData.OrderBy(r => r.RouteNumber))
            {
                var status = ClassifyDeviation(route);
                dataTableContent.AppendLine(
                    $"{route.RouteNumber,-12} | " +
                    $"{route.MechanicalWork,-12:F0} | " +
                    $"{route.ElectricConsumption,-13:F0} | " +
                    $"{route.DeviationPercent,-12:+F1}% | " +
                    $"{status,-15}"
                );
            }

            // Показываем в модальном окне
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var window = System.Windows.Application.Current.Windows
                    .OfType<Windows.PlotWindow>()
                    .FirstOrDefault();
                
                window?.ShowPointDetailsDialog(
                    "Таблица данных анализа",
                    dataTableContent.ToString()
                );
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка показа таблицы данных");
        }
    }

    /// <summary>
    /// Обновление графиков при смене режима отображения
    /// Аналог Python режим переключения "Уд. на работу" / "Н/Ф"
    /// </summary>
    private async Task UpdatePlotsForDisplayModeAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Очищаем текущие серии точек (оставляем нормы)
                    var seriesToRemove = NormsPlotModel.Series
                        .OfType<ScatterSeries>()
                        .ToList();
                    
                    foreach (var series in seriesToRemove)
                    {
                        NormsPlotModel.Series.Remove(series);
                    }

                    // Обновляем оси в зависимости от режима
                    if (SelectedDisplayMode == "Удельный расход")
                    {
                        UpdateAxesForSpecificConsumption();
                    }
                    else
                    {
                        UpdateAxesForAbsoluteValues();
                    }

                    // Добавляем точки заново с новым масштабом
                    _ = AddRoutePointsToPlotsAsync();
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка смены режима отображения");
        }
    }

    /// <summary>
    /// Настраивает оси для режима удельного расхода
    /// </summary>
    private void UpdateAxesForSpecificConsumption()
    {
        var yAxis = NormsPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
        if (yAxis != null)
        {
            yAxis.Title = "Удельный расход (кВт⋅час/ткм⋅км)";
        }
    }

    /// <summary>
    /// Настраивает оси для режима абсолютных значений
    /// </summary>
    private void UpdateAxesForAbsoluteValues()
    {
        var yAxis = NormsPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
        if (yAxis != null)
        {
            yAxis.Title = "Расход электроэнергии (кВт⋅час)";
        }
    }

    /// <summary>
    /// Обновляет видимость точек данных
    /// </summary>
    private void UpdatePointsVisibility()
    {
        try
        {
            foreach (var series in NormsPlotModel.Series.OfType<ScatterSeries>())
            {
                series.MarkerSize = ShowDataPoints ? 4 : 0;
            }
            
            foreach (var series in DeviationsPlotModel.Series.OfType<ScatterSeries>())
            {
                series.MarkerSize = ShowDataPoints ? 4 : 0;
            }

            NormsPlotModel.InvalidatePlot(true);
            DeviationsPlotModel.InvalidatePlot(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления видимости точек");
        }
    }

    /// <summary>
    /// Обновляет видимость легенды
    /// </summary>
    private void UpdateLegendVisibility()
    {
        try
        {
            var position = ShowLegend ? LegendPosition.RightTop : LegendPosition.None;
            
            NormsPlotModel.LegendPosition = position;
            DeviationsPlotModel.LegendPosition = position;
            
            NormsPlotModel.InvalidatePlot(true);
            DeviationsPlotModel.InvalidatePlot(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления легенды");
        }
    }

    /// <summary>
    /// Показывает детальную информацию о маршруте в модальном окне
    /// Полный аналог Python modal dialog functionality
    /// </summary>
    private void ShowRouteDetailsDialog(Route route)
    {
        try
        {
            var details = new System.Text.StringBuilder();
            
            details.AppendLine($"ДЕТАЛЬНАЯ ИНФОРМАЦИЯ О МАРШРУТЕ {route.RouteNumber}");
            details.AppendLine("=".PadRight(60, '='));
            details.AppendLine();
            
            details.AppendLine("ОСНОВНЫЕ ДАННЫЕ:");
            details.AppendLine("-".PadRight(20, '-'));
            details.AppendLine($"Номер маршрута: {route.RouteNumber}");
            details.AppendLine($"Участки: {string.Join(" → ", route.SectionNames)}");
            details.AppendLine($"Дата поездки: {route.TripDate:dd.MM.yyyy HH:mm}");
            details.AppendLine();
            
            details.AppendLine("ЛОКОМОТИВ:");
            details.AppendLine("-".PadRight(15, '-'));
            details.AppendLine($"Серия: {route.LocomotiveSeries}");
            details.AppendLine($"Тип: {route.LocomotiveType}");
            details.AppendLine($"Номер: {route.LocomotiveNumber}");
            details.AppendLine();
            
            details.AppendLine("ЭНЕРГЕТИЧЕСКИЕ ПОКАЗАТЕЛИ:");
            details.AppendLine("-".PadRight(30, '-'));
            details.AppendLine($"Механическая работа: {route.MechanicalWork:F2} кВт⋅час");
            details.AppendLine($"Расход электроэнергии: {route.ElectricConsumption:F2} кВт⋅час");
            details.AppendLine($"Удельный расход: {route.SpecificConsumption:F3} кВт⋅час/ткм⋅км");
            details.AppendLine($"КПД: {route.Efficiency:F2}%");
            details.AppendLine();
            
            details.AppendLine("АНАЛИЗ ОТКЛОНЕНИЙ:");
            details.AppendLine("-".PadRight(20, '-'));
            details.AppendLine($"Отклонение от нормы: {route.DeviationPercent:+F1}%");
            details.AppendLine($"Статус: {ClassifyDeviation(route)}");
            details.AppendLine($"Норма расхода: {route.NormConsumption:F2} кВт⋅час");
            details.AppendLine();
            
            details.AppendLine("ЭКСПЛУАТАЦИОННЫЕ УСЛОВИЯ:");
            details.AppendLine("-".PadRight(25, '-'));
            details.AppendLine($"Масса состава: {route.TrainWeight:F0} т");
            details.AppendLine($"Расстояние: {route.Distance:F1} км");
            details.AppendLine($"Время в пути: {route.TravelTime}");
            details.AppendLine($"Средняя скорость: {route.AverageSpeed:F1} км/ч");
            
            if (!string.IsNullOrEmpty(route.WeatherConditions))
            {
                details.AppendLine($"Погода: {route.WeatherConditions}");
            }
            
            if (!string.IsNullOrEmpty(route.Comments))
            {
                details.AppendLine();
                details.AppendLine("ДОПОЛНИТЕЛЬНАЯ ИНФОРМАЦИЯ:");
                details.AppendLine("-".PadRight(30, '-'));
                details.AppendLine(route.Comments);
            }

            // Показываем диалог в UI потоке
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var window = System.Windows.Application.Current.Windows
                    .OfType<Windows.PlotWindow>()
                    .FirstOrDefault();
                
                window?.ShowPointDetailsDialog(
                    $"Маршрут {route.RouteNumber} - Детальная информация",
                    details.ToString()
                );
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка формирования детальной информации о маршруте");
        }
    }

    #endregion

    #region INotifyPropertyChanged + IDisposable

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public void Dispose()
    {
        _routesData.Clear();
        _normFunctions.Clear();
        NormsPlotModel?.Dispose();
        DeviationsPlotModel?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}