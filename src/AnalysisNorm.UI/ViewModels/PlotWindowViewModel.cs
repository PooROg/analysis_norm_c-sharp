using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Legends;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.UI.ViewModels;

/// <summary>
/// ВОССТАНОВЛЕНО: ViewModel для окна графиков с восстановленной исходной функциональностью
/// + исправления из второго этапа
/// </summary>
public class PlotWindowViewModel : INotifyPropertyChanged
{
    #region Fields

    private readonly ILogger<PlotWindowViewModel> _logger;
    private readonly IVisualizationDataService _visualizationService;

    private PlotModel? _normsPlotModel;
    private PlotModel? _deviationsPlotModel;
    private bool _showPoints = true;
    private bool _showLegend = true;
    private string _selectedDisplayMode = "Все";
    private ObservableCollection<Route> _routes = new();
    private string _statusMessage = string.Empty;
    private bool _isLoading = false;

    // ИСПРАВЛЕНО: Правильные команды без дублирования
    private ICommand? _showDataTableCommand;
    private ICommand? _exportImageCommand;
    private ICommand? _refreshCommand;

    #endregion

    #region Constructor

    public PlotWindowViewModel(
        ILogger<PlotWindowViewModel> logger,
        IVisualizationDataService visualizationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _visualizationService = visualizationService ?? throw new ArgumentNullException(nameof(visualizationService));

        InitializeCommands();
        InitializePlotModels();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Модель графика норм
    /// </summary>
    public PlotModel? NormsPlotModel
    {
        get => _normsPlotModel;
        set => SetProperty(ref _normsPlotModel, value);
    }

    /// <summary>
    /// Модель графика отклонений
    /// </summary>
    public PlotModel? DeviationsPlotModel
    {
        get => _deviationsPlotModel;
        set => SetProperty(ref _deviationsPlotModel, value);
    }

    /// <summary>
    /// Показывать ли точки на графике
    /// </summary>
    public bool ShowPoints
    {
        get => _showPoints;
        set
        {
            if (SetProperty(ref _showPoints, value))
            {
                UpdatePointsVisibility();
            }
        }
    }

    /// <summary>
    /// Показывать ли легенду
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
    /// Выбранный режим отображения
    /// </summary>
    public string SelectedDisplayMode
    {
        get => _selectedDisplayMode;
        set
        {
            if (SetProperty(ref _selectedDisplayMode, value))
            {
                _ = UpdatePlotsForDisplayModeAsync();
            }
        }
    }

    /// <summary>
    /// Коллекция маршрутов для отображения
    /// </summary>
    public ObservableCollection<Route> Routes
    {
        get => _routes;
        set => SetProperty(ref _routes, value);
    }

    /// <summary>
    /// Сообщение о статусе
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Флаг загрузки данных
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// Доступные режимы отображения
    /// </summary>
    public ObservableCollection<string> DisplayModes { get; } = new()
    {
        "Все",
        "Только экономия",
        "Только перерасход",
        "Только норма",
        "По локомотивам",
        "По участкам"
    };

    #endregion

    #region Commands - ИСПРАВЛЕНО

    /// <summary>
    /// Команда показа таблицы данных
    /// </summary>
    public ICommand ShowDataTableCommand => _showDataTableCommand ??=
        new RelayCommand(ShowDataTable);

    /// <summary>
    /// Команда экспорта изображения
    /// </summary>
    public ICommand ExportImageCommand => _exportImageCommand ??=
        new AsyncRelayCommand(ExportImageAsync);

    /// <summary>
    /// Команда обновления данных
    /// </summary>
    public ICommand RefreshCommand => _refreshCommand ??=
        new AsyncRelayCommand(RefreshDataAsync);

    #endregion

    #region Initialization

    /// <summary>
    /// Инициализирует команды
    /// </summary>
    private void InitializeCommands()
    {
        // Команды инициализируются через lazy properties выше
    }

    /// <summary>
    /// Инициализирует модели графиков
    /// </summary>
    private void InitializePlotModels()
    {
        try
        {
            // Создаем модель для графика норм
            NormsPlotModel = new PlotModel
            {
                Title = "График норм расхода электроэнергии",
                Background = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray,
                TextColor = OxyColors.Black
            };

            // ИСПРАВЛЕНО: правильное создание легенды
            NormsPlotModel.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.RightTop,
                LegendPlacement = LegendPlacement.Outside,
                LegendOrientation = LegendOrientation.Vertical
            });

            // Добавляем оси
            NormsPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Нагрузка на ось, т/ось",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineColor = OxyColors.LightGray
            });

            NormsPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Удельный расход, кВт*ч/(т*км)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineColor = OxyColors.LightGray
            });

            // Создаем модель для графика отклонений
            DeviationsPlotModel = new PlotModel
            {
                Title = "График отклонений от нормы",
                Background = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray,
                TextColor = OxyColors.Black
            };

            // ИСПРАВЛЕНО: правильное создание легенды
            DeviationsPlotModel.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.RightTop,
                LegendPlacement = LegendPlacement.Outside,
                LegendOrientation = LegendOrientation.Vertical
            });

            // Добавляем оси для графика отклонений
            DeviationsPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Нагрузка на ось, т/ось",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineColor = OxyColors.LightGray
            });

            DeviationsPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Отклонение от нормы, %",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineColor = OxyColors.LightGray
            });

            _logger.LogInformation("Модели графиков успешно инициализированы");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации моделей графиков");
            StatusMessage = "Ошибка инициализации графиков";
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Загружает данные для отображения на графиках
    /// </summary>
    public async Task LoadDataAsync(IEnumerable<Route> routes, Dictionary<string, InterpolationFunction>? normFunctions = null)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Загрузка данных...";

            _logger.LogInformation("Начало загрузки данных для графиков");

            var routesList = routes.ToList();
            Routes.Clear();

            foreach (var route in routesList)
            {
                Routes.Add(route);
            }

            await CreateNormsPlotAsync(routesList, normFunctions);
            await CreateDeviationsPlotAsync(routesList);

            StatusMessage = $"Загружено маршрутов: {routesList.Count}";

            _logger.LogInformation("Данные для графиков успешно загружены");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке данных для графиков");
            StatusMessage = "Ошибка загрузки данных";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Добавляет новый маршрут на графики
    /// </summary>
    public async Task AddRouteAsync(Route route)
    {
        try
        {
            if (route == null) return;

            Routes.Add(route);
            await RefreshPlotsAsync();

            _logger.LogDebug("Маршрут добавлен на графики: {RouteName}", route.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при добавлении маршрута на графики");
        }
    }

    /// <summary>
    /// Очищает все данные графиков
    /// </summary>
    public void ClearData()
    {
        Routes.Clear();

        NormsPlotModel?.Series.Clear();
        DeviationsPlotModel?.Series.Clear();

        NormsPlotModel?.InvalidatePlot(true);
        DeviationsPlotModel?.InvalidatePlot(true);

        StatusMessage = "Данные очищены";

        _logger.LogInformation("Данные графиков очищены");
    }

    #endregion

    #region Private Plot Creation Methods

    /// <summary>
    /// Создает график норм расхода
    /// </summary>
    private async Task CreateNormsPlotAsync(List<Route> routes, Dictionary<string, InterpolationFunction>? normFunctions)
    {
        if (NormsPlotModel == null) return;

        try
        {
            NormsPlotModel.Series.Clear();

            // Создаем серию точек для маршрутов
            var routesSeries = new ScatterSeries
            {
                Title = "Фактические данные",
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Blue,
                MarkerStroke = OxyColors.DarkBlue
            };

            // Группируем маршруты для отображения
            var validRoutes = routes.Where(r => r.AxleLoad > 0 && (r.ElectricConsumption.HasValue || r.ActualConsumption.HasValue)).ToList();

            foreach (var route in validRoutes)
            {
                // ИСПРАВЛЕНО: правильная работа с восстановленными свойствами
                var consumption = route.ElectricConsumption ?? route.ActualConsumption ?? 0;
                var yValue = route.SpecificConsumption.HasValue
                    ? (double)route.SpecificConsumption.Value
                    : (double)(consumption / Math.Max(1, route.Distance * route.TrainMass) * 1000);

                routesSeries.Points.Add(new ScatterPoint((double)route.AxleLoad, yValue)
                {
                    Tag = route // Сохраняем ссылку на маршрут для обработки кликов
                });
            }

            NormsPlotModel.Series.Add(routesSeries);

            // Добавляем кривые норм если есть функции интерполяции
            if (normFunctions != null)
            {
                await AddNormCurvesToPlot(normFunctions);
            }

            // Обновляем график
            NormsPlotModel.InvalidatePlot(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании графика норм");
        }
    }

    /// <summary>
    /// Добавляет кривые норм на график
    /// </summary>
    private async Task AddNormCurvesToPlot(Dictionary<string, InterpolationFunction> normFunctions)
    {
        if (NormsPlotModel == null) return;

        var colors = new[] { OxyColors.Red, OxyColors.Green, OxyColors.Orange, OxyColors.Purple, OxyColors.Brown };
        int colorIndex = 0;

        foreach (var kvp in normFunctions.Take(5)) // Ограничиваем количество кривых для читаемости
        {
            try
            {
                var normSeries = new LineSeries
                {
                    Title = $"Норма: {kvp.Key}",
                    Color = colors[colorIndex % colors.Length],
                    StrokeThickness = 2,
                    MarkerType = MarkerType.None
                };

                // Генерируем точки кривой нормы
                var minLoad = kvp.Value.XValues.Min();
                var maxLoad = kvp.Value.XValues.Max();
                var step = (maxLoad - minLoad) / 100;

                for (double load = (double)minLoad; load <= (double)maxLoad; load += step)
                {
                    var consumption = kvp.Value.Interpolate(load);
                    if (consumption > 0)
                    {
                        normSeries.Points.Add(new DataPoint(load, consumption));
                    }
                }

                NormsPlotModel.Series.Add(normSeries);
                colorIndex++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении кривой нормы {NormId}", kvp.Key);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Создает график отклонений от норм
    /// </summary>
    private async Task CreateDeviationsPlotAsync(List<Route> routes)
    {
        if (DeviationsPlotModel == null) return;

        try
        {
            DeviationsPlotModel.Series.Clear();

            // Группируем маршруты по статусам отклонений
            var groupedRoutes = routes
                .Where(r => r.AxleLoad > 0 && (r.DeviationPercentage.HasValue || r.DeviationPercent.HasValue))
                .GroupBy(r => r.Status)
                .ToList();

            var statusColors = new Dictionary<DeviationStatus, OxyColor>
            {
                { DeviationStatus.StrongEconomy, OxyColors.DarkGreen },
                { DeviationStatus.MediumEconomy, OxyColors.Green },
                { DeviationStatus.WeakEconomy, OxyColors.LightGreen },
                { DeviationStatus.Normal, OxyColors.Blue },
                { DeviationStatus.WeakOverrun, OxyColors.Orange },
                { DeviationStatus.MediumOverrun, OxyColors.OrangeRed },
                { DeviationStatus.StrongOverrun, OxyColors.Red }
            };

            foreach (var group in groupedRoutes)
            {
                var series = new ScatterSeries
                {
                    Title = DeviationStatusHelper.GetDescription(group.Key),
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    MarkerFill = statusColors.GetValueOrDefault(group.Key, OxyColors.Gray),
                    MarkerStroke = statusColors.GetValueOrDefault(group.Key, OxyColors.Gray)
                };

                foreach (var route in group)
                {
                    // ИСПРАВЛЕНО: правильная работа с восстановленными свойствами
                    var deviationPercent = route.DeviationPercentage ?? route.DeviationPercent ?? 0;

                    series.Points.Add(new ScatterPoint(
                        (double)route.AxleLoad,
                        (double)deviationPercent)
                    {
                        Tag = route
                    });
                }

                DeviationsPlotModel.Series.Add(series);
            }

            // Добавляем горизонтальные линии для границ норм
            AddDeviationBoundaryLines();

            DeviationsPlotModel.InvalidatePlot(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании графика отклонений");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Добавляет граничные линии отклонений
    /// </summary>
    private void AddDeviationBoundaryLines()
    {
        if (DeviationsPlotModel == null) return;

        var boundaries = new[] { -10, -5, -2, 2, 5, 10 };
        var colors = new[] { OxyColors.DarkGreen, OxyColors.Green, OxyColors.LightGreen,
                           OxyColors.LightCoral, OxyColors.Orange, OxyColors.Red };

        for (int i = 0; i < boundaries.Length; i++)
        {
            var line = new LineSeries
            {
                Title = $"{(boundaries[i] > 0 ? "+" : "")}{boundaries[i]}%",
                Color = colors[i],
                LineStyle = LineStyle.Dash,
                StrokeThickness = 1
            };

            // Создаем горизонтальную линию через весь график
            line.Points.Add(new DataPoint(0, boundaries[i]));
            line.Points.Add(new DataPoint(50, boundaries[i])); // Предполагаем максимум 50 т/ось

            DeviationsPlotModel.Series.Add(line);
        }
    }

    #endregion

    #region Event Handlers and Updates

    /// <summary>
    /// Обработчик клика по точке на графике
    /// ИСПРАВЛЕНО: правильная работа с HitTestResult
    /// </summary>
    public void HandlePlotClick(object sender, OxyMouseEventArgs e)
    {
        try
        {
            var plotModel = sender as PlotModel;
            if (plotModel == null) return;

            // ИСПРАВЛЕНО: правильное использование HitTest
            var hitResult = plotModel.HitTest(new HitTestArguments(e.Position, 5));

            if (hitResult != null)
            {
                // Находим серию и точку
                if (hitResult.Item is ScatterSeries series && hitResult.Index >= 0)
                {
                    if (hitResult.Index < series.Points.Count)
                    {
                        var point = series.Points[hitResult.Index];
                        if (point.Tag is Route route)
                        {
                            ShowRouteDetailsDialog(route);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке клика по графику");
        }
    }

    /// <summary>
    /// Обновляет видимость точек
    /// </summary>
    private void UpdatePointsVisibility()
    {
        try
        {
            UpdateSeriesPointsVisibility(NormsPlotModel);
            UpdateSeriesPointsVisibility(DeviationsPlotModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении видимости точек");
        }
    }

    /// <summary>
    /// Обновляет видимость точек для конкретной модели
    /// </summary>
    private void UpdateSeriesPointsVisibility(PlotModel? plotModel)
    {
        if (plotModel == null) return;

        foreach (var series in plotModel.Series.OfType<ScatterSeries>())
        {
            series.MarkerType = ShowPoints ? MarkerType.Circle : MarkerType.None;
        }

        plotModel.InvalidatePlot(true);
    }

    /// <summary>
    /// Обновляет видимость легенды
    /// </summary>
    private void UpdateLegendVisibility()
    {
        try
        {
            UpdatePlotLegendVisibility(NormsPlotModel);
            UpdatePlotLegendVisibility(DeviationsPlotModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении видимости легенды");
        }
    }

    /// <summary>
    /// Обновляет видимость легенды для конкретной модели
    /// </summary>
    private void UpdatePlotLegendVisibility(PlotModel? plotModel)
    {
        if (plotModel == null) return;

        // ИСПРАВЛЕНО: правильная работа с легендой OxyPlot
        foreach (var legend in plotModel.Legends)
        {
            legend.IsLegendVisible = ShowLegend;
        }

        plotModel.InvalidatePlot(true);
    }

    /// <summary>
    /// Обновляет графики по режиму отображения
    /// </summary>
    private async Task UpdatePlotsForDisplayModeAsync()
    {
        try
        {
            StatusMessage = "Обновление графиков...";

            var filteredRoutes = FilterRoutesByDisplayMode(Routes.ToList());

            await CreateNormsPlotAsync(filteredRoutes, null);
            await CreateDeviationsPlotAsync(filteredRoutes);

            StatusMessage = $"Отображено маршрутов: {filteredRoutes.Count}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении графиков по режиму отображения");
            StatusMessage = "Ошибка обновления";
        }
    }

    /// <summary>
    /// Фильтрует маршруты по выбранному режиму отображения
    /// </summary>
    private List<Route> FilterRoutesByDisplayMode(List<Route> routes)
    {
        return SelectedDisplayMode switch
        {
            "Только экономия" => routes.Where(r => r.Status.IsEconomy()).ToList(),
            "Только перерасход" => routes.Where(r => r.Status.IsOverrun()).ToList(),
            "Только норма" => routes.Where(r => r.Status.IsNormal()).ToList(),
            "По локомотивам" => routes.OrderBy(r => r.LocomotiveSeries).ThenBy(r => r.LocomotiveNumber).ToList(),
            "По участкам" => routes.OrderBy(r => string.Join(",", r.SectionNames)).ToList(),
            _ => routes
        };
    }

    #endregion

    #region Command Implementations

    /// <summary>
    /// Показывает таблицу данных
    /// </summary>
    private void ShowDataTable()
    {
        try
        {
            if (!Routes.Any())
            {
                MessageBox.Show("Нет данных для отображения", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Создаем простое окно с таблицей данных
            var dataWindow = new Window
            {
                Title = "Данные маршрутов",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var dataGrid = new System.Windows.Controls.DataGrid
            {
                ItemsSource = Routes,
                AutoGenerateColumns = true,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false
            };

            dataWindow.Content = dataGrid;
            dataWindow.Show();

            _logger.LogInformation("Открыто окно с таблицей данных");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при показе таблицы данных");
            MessageBox.Show("Ошибка при открытии таблицы данных", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Экспортирует изображение графика
    /// </summary>
    private async Task ExportImageAsync()
    {
        try
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить график как изображение",
                Filter = "PNG файлы (*.png)|*.png|JPEG файлы (*.jpg)|*.jpg|Все файлы (*.*)|*.*",
                DefaultExt = "png"
            };

            if (saveDialog.ShowDialog() == true)
            {
                StatusMessage = "Экспорт изображения...";

                // Экспортируем график норм
                if (NormsPlotModel != null)
                {
                    var pngExporter = new OxyPlot.SkiaSharp.PngExporter { Width = 800, Height = 600 };
                    await using var stream = File.Create(saveDialog.FileName);
                    pngExporter.Export(NormsPlotModel, stream);
                }

                StatusMessage = "Изображение сохранено";
                _logger.LogInformation("График экспортирован в файл: {FilePath}", saveDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при экспорте изображения");
            StatusMessage = "Ошибка экспорта";
            MessageBox.Show("Ошибка при экспорте изображения", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Обновляет данные графиков
    /// </summary>
    private async Task RefreshDataAsync()
    {
        try
        {
            StatusMessage = "Обновление данных...";
            await RefreshPlotsAsync();
            StatusMessage = "Данные обновлены";

            _logger.LogInformation("Данные графиков обновлены");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении данных");
            StatusMessage = "Ошибка обновления";
        }
    }

    /// <summary>
    /// ВОССТАНОВЛЕНО: Показывает детальную информацию о маршруте с исходной логикой
    /// </summary>
    private void ShowRouteDetailsDialog(Route route)
    {
        try
        {
            // ВОССТАНОВЛЕНО: исходная логика формирования деталей из оригинального файла
            var details = new StringBuilder();
            details.AppendLine("ОСНОВНАЯ ИНФОРМАЦИЯ О МАРШРУТЕ");
            details.AppendLine("=".PadRight(40, '='));
            details.AppendLine($"Номер маршрута: {route.RouteNumber ?? route.Name}");
            details.AppendLine($"Дата поездки: {(route.TripDate != default ? route.TripDate : route.Date):dd.MM.yyyy}");
            details.AppendLine($"Участок: {route.SectionName}");
            details.AppendLine();

            details.AppendLine("ЛОКОМОТИВ:");
            details.AppendLine("-".PadRight(15, '-'));
            details.AppendLine($"Серия: {route.LocomotiveSeries}");
            details.AppendLine($"Номер: {route.LocomotiveNumber}");
            details.AppendLine($"Тип: {route.LocomotiveType}");
            details.AppendLine($"Депо: {route.Depot}");
            details.AppendLine();

            details.AppendLine("ЭНЕРГЕТИЧЕСКИЕ ПОКАЗАТЕЛИ:");
            details.AppendLine("-".PadRight(30, '-'));

            // ВОССТАНОВЛЕНО: работа с исходными свойствами ElectricConsumption и MechanicalWork
            var electricConsumption = route.ElectricConsumption ?? route.FactConsumption ?? route.ActualConsumption;
            var mechanicalWork = route.MechanicalWork;
            var specificConsumption = route.SpecificConsumption ?? route.FactUd;

            if (mechanicalWork.HasValue)
                details.AppendLine($"Механическая работа: {mechanicalWork:F2} кВт⋅час");

            if (electricConsumption.HasValue)
                details.AppendLine($"Расход электроэнергии: {electricConsumption:F2} кВт⋅час");

            if (specificConsumption.HasValue)
                details.AppendLine($"Удельный расход: {specificConsumption:F3} кВт⋅час/ткм⋅км");

            // ВОССТАНОВЛЕНО: расчет эффективности из исходного файла
            if (mechanicalWork.HasValue && electricConsumption.HasValue && electricConsumption > 0)
            {
                var efficiency = mechanicalWork / electricConsumption * 100;
                details.AppendLine($"КПД: {efficiency:F2}%");
            }
            else if (route.Efficiency.HasValue)
            {
                details.AppendLine($"Эффективность: {route.Efficiency:F2}%");
            }

            details.AppendLine();

            details.AppendLine("АНАЛИЗ ОТКЛОНЕНИЙ:");
            details.AppendLine("-".PadRight(20, '-'));

            var deviationPercent = route.DeviationPercentage ?? route.DeviationPercent;
            if (deviationPercent.HasValue)
            {
                details.AppendLine($"Отклонение от нормы: {deviationPercent:+F1;-F1;0.0}%");
            }

            details.AppendLine($"Статус: {DeviationStatusHelper.GetDescription(route.Status)}");

            var normConsumption = route.NormativeConsumption ?? route.NormConsumption;
            if (normConsumption.HasValue)
                details.AppendLine($"Норма расхода: {normConsumption:F2} кВт⋅час");

            details.AppendLine();

            details.AppendLine("ЭКСПЛУАТАЦИОННЫЕ УСЛОВИЯ:");
            details.AppendLine("-".PadRight(25, '-'));

            // ВОССТАНОВЛЕНО: работа с различными вариантами массы из исходного файла
            var trainMass = route.TrainMass > 0 ? route.TrainMass :
                           route.TrainWeight ?? route.BruttoTons ?? route.NettoTons ?? 0;
            if (trainMass > 0)
                details.AppendLine($"Масса состава: {trainMass:F0} т");

            if (route.Distance > 0)
                details.AppendLine($"Расстояние: {route.Distance:F1} км");

            if (route.TravelTime != default(TimeSpan))
                details.AppendLine($"Время в пути: {route.TravelTime:hh\\:mm}");

            // ВОССТАНОВЛЕНО: расчет средней скорости из исходного файла
            if (route.AverageSpeed.HasValue)
            {
                details.AppendLine($"Средняя скорость: {route.AverageSpeed:F1} км/ч");
            }
            else if (route.Distance > 0 && route.TravelTime.TotalHours > 0)
            {
                var avgSpeed = route.Distance / (decimal)route.TravelTime.TotalHours;
                details.AppendLine($"Средняя скорость: {avgSpeed:F1} км/ч");
            }

            if (route.AxleLoad > 0)
                details.AppendLine($"Нагрузка на ось: {route.AxleLoad:F1} т/ось");

            // ВОССТАНОВЛЕНО: дополнительные поля из исходного файла
            if (!string.IsNullOrEmpty(route.WeatherConditions))
            {
                details.AppendLine($"Погодные условия: {route.WeatherConditions}");
            }

            if (route.SectionNames.Any())
            {
                details.AppendLine($"Участки маршрута: {string.Join(", ", route.SectionNames)}");
            }

            if (!string.IsNullOrEmpty(route.Comments))
            {
                details.AppendLine();
                details.AppendLine("ДОПОЛНИТЕЛЬНАЯ ИНФОРМАЦИЯ:");
                details.AppendLine("-".PadRight(30, '-'));
                details.AppendLine(route.Comments);
            }

            // ВОССТАНОВЛЕНО: показ диалога из исходного файла
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var window = System.Windows.Application.Current.Windows
                    .OfType<Windows.PlotWindow>()
                    .FirstOrDefault();

                if (window != null)
                {
                    // Если есть специальный метод показа деталей
                    var method = window.GetType().GetMethod("ShowPointDetailsDialog");
                    if (method != null)
                    {
                        method.Invoke(window, new object[] {
                            $"Маршрут {route.RouteNumber ?? route.Name} - Детальная информация",
                            details.ToString()
                        });
                    }
                    else
                    {
                        // Иначе показываем стандартный MessageBox
                        MessageBox.Show(details.ToString(),
                            $"Детали маршрута: {route.RouteNumber ?? route.Name}",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Fallback для случая, когда окно не найдено
                    MessageBox.Show(details.ToString(),
                        $"Детали маршрута: {route.RouteNumber ?? route.Name}",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при формировании детальной информации о маршруте");
            MessageBox.Show("Ошибка при отображении деталей маршрута", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Обновляет все графики
    /// </summary>
    private async Task RefreshPlotsAsync()
    {
        var routes = Routes.ToList();
        await CreateNormsPlotAsync(routes, null);
        await CreateDeviationsPlotAsync(routes);
    }

    /// <summary>
    /// ВОССТАНОВЛЕНО: Классификатор отклонений из исходного файла
    /// </summary>
    private string ClassifyDeviation(Route route)
    {
        var deviation = route.DeviationPercentage ?? route.DeviationPercent ?? 0;

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

    #endregion

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion

    #region IDisposable Implementation

    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // PlotModel не реализует IDisposable в OxyPlot
                // Просто обнуляем ссылки
                NormsPlotModel = null;
                DeviationsPlotModel = null;
                Routes.Clear();
            }

            _disposed = true;
        }
    }

    #endregion
}

/// <summary>
/// Простая реализация ICommand для синхронных операций
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// Реализация ICommand для асинхронных операций
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;

        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}