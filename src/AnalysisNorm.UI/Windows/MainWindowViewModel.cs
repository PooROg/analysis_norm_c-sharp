using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.UI.Commands;
using AnalysisNorm.UI.Windows;

namespace AnalysisNorm.UI.ViewModels;

/// <summary>
/// Главный ViewModel приложения - аналог Python NormsAnalyzerGUI класса
/// Реализует MVVM паттерн для взаимодействия с UI и бизнес-логикой
/// </summary>
public class MainWindowViewModel : INotifyPropertyChanged
{
    #region Поля - аналогично Python instance variables

    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHtmlRouteProcessorService _htmlRouteProcessor;
    private readonly IHtmlNormProcessorService _htmlNormProcessor;
    private readonly IDataAnalysisService _dataAnalysisService;
    private readonly IVisualizationDataService _visualizationService;
    private readonly IExcelExportService _excelExportService;
    private readonly ILocomotiveFilterService _locomotiveFilterService;

    // Состояние загрузки - аналог Python threading операций
    private bool _isLoading;
    private string _loadingMessage = string.Empty;
    private double _loadingProgress;

    // Данные приложения - аналог Python analyzer, locomotive_filter и пр.
    private List<Route> _loadedRoutes = new();
    private List<Norm> _loadedNorms = new();
    private List<LocomotiveCoefficient> _locomotiveCoefficients = new();

    // UI состояние - аналог Python GUI variables
    private string _selectedSection = string.Empty;
    private string _selectedNorm = "Все нормы";
    private bool _singleSectionOnly;
    private string _logText = string.Empty;
    private string _sectionInfo = string.Empty;
    private string _filterInfo = string.Empty;
    private string _statisticsText = string.Empty;
    private bool _useCoefficients;
    private bool _excludeLowWork;

    #endregion

    #region Конструктор - аналог Python __init__

    /// <summary>
    /// Конструктор ViewModel с dependency injection
    /// Эквивалент Python def __init__(self, root: tk.Tk)
    /// </summary>
    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        ILoggerFactory loggerFactory,
        IHtmlRouteProcessorService htmlRouteProcessor,
        IHtmlNormProcessorService htmlNormProcessor,
        IDataAnalysisService dataAnalysisService,
        IVisualizationDataService visualizationService,
        IExcelExportService excelExportService,
        ILocomotiveFilterService locomotiveFilterService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _htmlRouteProcessor = htmlRouteProcessor ?? throw new ArgumentNullException(nameof(htmlRouteProcessor));
        _htmlNormProcessor = htmlNormProcessor ?? throw new ArgumentNullException(nameof(htmlNormProcessor));
        _dataAnalysisService = dataAnalysisService ?? throw new ArgumentNullException(nameof(dataAnalysisService));
        _visualizationService = visualizationService ?? throw new ArgumentNullException(nameof(visualizationService));
        _excelExportService = excelExportService ?? throw new ArgumentNullException(nameof(excelExportService));
        _locomotiveFilterService = locomotiveFilterService ?? throw new ArgumentNullException(nameof(locomotiveFilterService));

        // Инициализируем коллекции - аналог Python instance variables
        Sections = new ObservableCollection<string>();
        NormsWithCounts = new ObservableCollection<string>();
        
        // Устанавливаем начальное состояние UI
        StatisticsText = "Статистика будет отображена\nпосле выполнения анализа.\n\nДля начала:\n1. Выберите участок\n2. Настройте фильтры\n3. Нажмите 'Выполнить анализ'";
        
        // Инициализируем команды - аналог Python callback connections
        InitializeCommands();

        _logger.LogInformation("MainWindowViewModel инициализирован");
    }

    #endregion

    #region Методы жизненного цикла

    /// <summary>
    /// Генерирует статистику из результата анализа - аналог Python format statistics
    /// </summary>
    private string GenerateStatisticsFromAnalysis(AnalysisResult analysisResult)
    {
        try
        {
            if (analysisResult == null || !analysisResult.ProcessedRoutes.Any())
                return "Нет данных для отображения статистики.";

            var sb = new StringBuilder();
            var routes = analysisResult.ProcessedRoutes;
            var total = routes.Count;

            // Основные показатели - аналог Python main statistics
            sb.AppendLine($"Всего маршрутов: {total}");
            sb.AppendLine($"Обработано: {total}");

            // Категории отклонений с процентами
            var economy = routes.Count(r => r.DeviationPercent < -5);
            var normal = routes.Count(r => Math.Abs(r.DeviationPercent) <= 5);
            var overrun = routes.Count(r => r.DeviationPercent > 5);

            sb.AppendLine($"Экономия: {economy} ({economy / (double)total * 100:F1}%)");
            sb.AppendLine($"В норме: {normal} ({normal / (double)total * 100:F1}%)");
            sb.AppendLine($"Перерасход: {overrun} ({overrun / (double)total * 100:F1}%)");

            // Средние показатели
            var deviations = routes.Where(r => r.DeviationPercent != 0).Select(r => r.DeviationPercent).ToList();
            if (deviations.Any())
            {
                sb.AppendLine($"Среднее отклонение: {deviations.Average():F2}%");
                sb.AppendLine($"Медианное отклонение: {GetMedian(deviations):F2}%");
            }

            sb.AppendLine();

            // Детальная статистика - аналог Python detailed_stats
            sb.AppendLine("Детально:");
            var categories = new Dictionary<string, (Func<double, bool> condition, string name)>
            {
                { "economy_strong", (d => d < -30, "Экономия сильная (>30%)") },
                { "economy_medium", (d => d >= -30 && d < -20, "Экономия средняя (20-30%)") },
                { "economy_weak", (d => d >= -20 && d < -5, "Экономия слабая (5-20%)") },
                { "normal", (d => Math.Abs(d) <= 5, "Норма (±5%)") },
                { "overrun_weak", (d => d > 5 && d <= 20, "Перерасход слабый (5-20%)") },
                { "overrun_medium", (d => d > 20 && d <= 30, "Перерасход средний (20-30%)") },
                { "overrun_strong", (d => d > 30, "Перерасход сильный (>30%)") }
            };

            foreach (var (key, (condition, name)) in categories)
            {
                var count = routes.Count(r => condition(r.DeviationPercent));
                if (count > 0)
                {
                    var percent = count / (double)total * 100;
                    sb.AppendLine($"{name}: {count} ({percent:F1}%)");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации статистики из анализа");
            return "Ошибка генерации статистики.";
        }
    }

    /// <summary>
    /// Вычисляет медиану списка значений
    /// </summary>
    private static double GetMedian(List<double> values)
    {
        if (!values.Any()) return 0;

        var sorted = values.OrderBy(x => x).ToList();
        var count = sorted.Count;

        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }

        return sorted[count / 2];
    }

    #endregion

    #region Properties - аналог Python properties и UI bindings

    /// <summary>
    /// Состояние загрузки - показывает overlay прогресса
    /// Аналог Python threading операций с UI блокировкой
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// Сообщение во время загрузки
    /// </summary>
    public string LoadingMessage
    {
        get => _loadingMessage;
        set => SetProperty(ref _loadingMessage, value);
    }

    /// <summary>
    /// Прогресс загрузки в процентах (0-100)
    /// </summary>
    public double LoadingProgress
    {
        get => _loadingProgress;
        set => SetProperty(ref _loadingProgress, value);
    }

    /// <summary>
    /// Выбранный участок - аналог Python section_var
    /// </summary>
    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                // Обновляем нормы при смене участка - аналог Python _on_section_selected
                _ = UpdateNormsAndSectionInfoAsync();
            }
        }
    }

    /// <summary>
    /// Выбранная норма - аналог Python norm_var
    /// </summary>
    public string SelectedNorm
    {
        get => _selectedNorm;
        set => SetProperty(ref _selectedNorm, value);
    }

    /// <summary>
    /// Фильтр "только один участок" - аналог Python single_section_only
    /// </summary>
    public bool SingleSectionOnly
    {
        get => _singleSectionOnly;
        set
        {
            if (SetProperty(ref _singleSectionOnly, value))
            {
                // Обновляем статистику при изменении фильтра - аналог Python _on_single_section_changed
                _ = UpdateNormsAndSectionInfoAsync();
            }
        }
    }

    /// <summary>
    /// Текст журнала операций - аналог Python log_text
    /// </summary>
    public string LogText
    {
        get => _logText;
        private set => SetProperty(ref _logText, value);
    }

    /// <summary>
    /// Использовать коэффициенты локомотивов
    /// </summary>
    public bool UseCoefficients
    {
        get => _useCoefficients;
        set => SetProperty(ref _useCoefficients, value);
    }

    /// <summary>
    /// Исключать маршруты с низкой работой
    /// </summary>
    public bool ExcludeLowWork
    {
        get => _excludeLowWork;
        set => SetProperty(ref _excludeLowWork, value);
    }

    /// <summary>
    /// Коллекция участков для ComboBox - аналог Python sections list
    /// </summary>
    public ObservableCollection<string> Sections { get; }

    /// <summary>
    /// Коллекция норм с количеством маршрутов - аналог Python norms_with_counts
    /// </summary>
    public ObservableCollection<string> NormsWithCounts { get; }

    /// <summary>
    /// Информация об участке - аналог Python section_info_label
    /// </summary>
    public string SectionInfo
    {
        get => _sectionInfo;
        set => SetProperty(ref _sectionInfo, value);
    }

    /// <summary>
    /// Информация о фильтрах - аналог Python filter_info_label
    /// </summary>
    public string FilterInfo
    {
        get => _filterInfo;
        set => SetProperty(ref _filterInfo, value);
    }

    /// <summary>
    /// Текст статистики - аналог Python stats_text
    /// </summary>
    public string StatisticsText
    {
        get => _statisticsText;
        set => SetProperty(ref _statisticsText, value);
    }

    #endregion

    #region Commands - аналог Python callbacks

    public ICommand LoadRoutesCommand { get; private set; } = null!;
    public ICommand LoadNormsCommand { get; private set; } = null!;
    public ICommand AnalyzeCommand { get; private set; } = null!;
    public ICommand FilterLocomotivesCommand { get; private set; } = null!;
    public ICommand EditNormsCommand { get; private set; } = null!;
    public ICommand ExportExcelCommand { get; private set; } = null!;
    public ICommand ExportPlotCommand { get; private set; } = null!;
    public ICommand OpenPlotCommand { get; private set; } = null!;
    public ICommand ShowNormStorageInfoCommand { get; private set; } = null!;
    public ICommand ValidateNormsCommand { get; private set; } = null!;
    public ICommand ShowRoutesStatisticsCommand { get; private set; } = null!;
    public ICommand ClearLogsCommand { get; private set; } = null!;

    #endregion

    #region Инициализация - аналог Python _setup_gui + _connect_callbacks

    /// <summary>
    /// Инициализирует команды интерфейса - аналог Python _connect_callbacks()
    /// </summary>
    private void InitializeCommands()
    {
        // Асинхронные команды для файловых операций - аналог Python _run_async calls
        LoadRoutesCommand = new AsyncCommand(LoadRoutesAsync, () => !IsLoading);
        LoadNormsCommand = new AsyncCommand(LoadNormsAsync, () => !IsLoading);
        AnalyzeCommand = new AsyncCommand(AnalyzeAsync, CanAnalyze);
        ExportExcelCommand = new AsyncCommand(ExportExcelAsync, () => _loadedRoutes.Any() && !IsLoading);
        ExportPlotCommand = new AsyncCommand(ExportPlotAsync, () => _loadedRoutes.Any() && !IsLoading);

        // Синхронные команды для UI операций
        FilterLocomotivesCommand = new RelayCommand(FilterLocomotives, CanFilterLocomotives);
        EditNormsCommand = new RelayCommand(EditNorms, () => _loadedNorms.Any());
        OpenPlotCommand = new RelayCommand(OpenPlot, () => _loadedRoutes.Any());
        ShowNormStorageInfoCommand = new AsyncCommand(ShowNormStorageInfoAsync, () => !IsLoading);
        ValidateNormsCommand = new AsyncCommand(ValidateNormsAsync, () => !IsLoading);
        ShowRoutesStatisticsCommand = new AsyncCommand(ShowRoutesStatisticsAsync, () => _loadedRoutes.Any());
        ClearLogsCommand = new RelayCommand(ClearLogs);
    }

    /// <summary>
    /// Асинхронная инициализация после загрузки UI - аналог Python _setup_gui()
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            LoadingMessage = "Инициализация приложения...";
            LoadingProgress = 10;

            // Загружаем коэффициенты локомотивов - аналог Python coefficients_manager
            LoadingMessage = "Загрузка коэффициентов локомотивов...";
            LoadingProgress = 50;
            _locomotiveCoefficients = await _locomotiveFilterService.LoadCoefficientsAsync();
            
            LoadingMessage = "Настройка интерфейса...";
            LoadingProgress = 90;
            
            // Добавляем приветственное сообщение в лог - аналог Python logger.info("GUI инициализирован")
            AppendToLog("Анализатор норм расхода электроэнергии РЖД запущен");
            AppendToLog("Готов к работе. Выберите HTML файлы для анализа.");

            LoadingProgress = 100;
            await Task.Delay(500); // Короткая пауза для плавности
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации MainWindowViewModel");
            AppendToLog($"Ошибка инициализации: {ex.Message}");
            throw;
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
            LoadingProgress = 0;
        }
    }

    #endregion

    #region Методы команд - аналоги Python event handlers

    /// <summary>
    /// Загрузка HTML файлов маршрутов - аналог Python _on_routes_loaded
    /// </summary>
    private async Task LoadRoutesAsync(object? parameter)
    {
        try
        {
            var filePaths = parameter as string[] ?? throw new ArgumentException("Неверный параметр файлов");
            
            IsLoading = true;
            LoadingMessage = "Загрузка HTML файлов маршрутов...";
            LoadingProgress = 0;

            AppendToLog($"Начата загрузка {filePaths.Length} файлов маршрутов");

            // Обрабатываем файлы с прогрессом
            var routes = new List<Route>();
            for (int i = 0; i < filePaths.Length; i++)
            {
                LoadingProgress = (i + 1.0) / filePaths.Length * 80; // 80% на загрузку файлов
                LoadingMessage = $"Обработка файла {i + 1} из {filePaths.Length}...";
                
                var fileRoutes = await _htmlRouteProcessor.ProcessHtmlFileAsync(filePaths[i]);
                routes.AddRange(fileRoutes);
            }

            // Обновляем состояние - аналог Python _update_after_routes_loaded
            LoadingMessage = "Обновление интерфейса...";
            LoadingProgress = 90;
            
            _loadedRoutes = routes;
            await UpdateSectionsAsync();

            LoadingProgress = 100;
            AppendToLog($"Загружено {routes.Count} маршрутов из {filePaths.Length} файлов");
            AppendToLog("Маршруты успешно обработаны. Выберите участок для анализа.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки HTML файлов маршрутов");
            AppendToLog($"Ошибка загрузки маршрутов: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
            LoadingProgress = 0;
        }
    }

    /// <summary>
    /// Загрузка HTML файлов норм - аналог Python _on_norms_loaded
    /// </summary>
    private async Task LoadNormsAsync(object? parameter)
    {
        try
        {
            var filePaths = parameter as string[] ?? throw new ArgumentException("Неверный параметр файлов");
            
            IsLoading = true;
            LoadingMessage = "Загрузка HTML файлов норм...";
            LoadingProgress = 0;

            AppendToLog($"Начата загрузка {filePaths.Length} файлов норм");

            // Обрабатываем файлы норм
            var norms = new List<Norm>();
            for (int i = 0; i < filePaths.Length; i++)
            {
                LoadingProgress = (i + 1.0) / filePaths.Length * 80;
                LoadingMessage = $"Обработка файла норм {i + 1} из {filePaths.Length}...";
                
                var fileNorms = await _htmlNormProcessor.ProcessHtmlFileAsync(filePaths[i]);
                norms.AddRange(fileNorms);
            }

            LoadingMessage = "Валидация норм...";
            LoadingProgress = 90;
            
            _loadedNorms = norms;
            await UpdateNormsAndSectionInfoAsync();

            LoadingProgress = 100;
            AppendToLog($"Загружено {norms.Count} норм из {filePaths.Length} файлов");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки HTML файлов норм");
            AppendToLog($"Ошибка загрузки норм: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
            LoadingProgress = 0;
        }
    }

    /// <summary>
    /// Выполнение анализа - аналог Python _on_analyze_clicked
    /// </summary>
    private async Task AnalyzeAsync(object? parameter)
    {
        try
        {
            if (string.IsNullOrEmpty(SelectedSection))
            {
                AppendToLog("Предупреждение: Выберите участок для анализа");
                return;
            }

            IsLoading = true;
            LoadingMessage = "Выполнение анализа...";
            LoadingProgress = 0;

            AppendToLog($"Начат анализ участка: {SelectedSection}");
            if (SingleSectionOnly)
                AppendToLog("Применен фильтр: только маршруты с одним участком");

            // Фильтруем маршруты по участку и настройкам
            LoadingMessage = "Фильтрация маршрутов...";
            LoadingProgress = 20;
            
            var filteredRoutes = _loadedRoutes
                .Where(r => r.SectionNames.Contains(SelectedSection))
                .Where(r => !SingleSectionOnly || r.SectionNames.Count == 1)
                .ToList();

            // Выполняем анализ данных
            LoadingMessage = "Анализ данных...";
            LoadingProgress = 60;
            
            var analysisResult = await _dataAnalysisService.AnalyzeRoutesAsync(
                filteredRoutes, 
                SelectedSection, 
                SelectedNorm != "Все нормы" ? ExtractNormId(SelectedNorm) : null);

            // Подготавливаем данные для визуализации
            LoadingMessage = "Подготовка визуализации...";
            LoadingProgress = 90;
            
            await _visualizationService.PrepareVisualizationDataAsync(analysisResult);

            // Обновляем статистику в UI - аналог Python update_statistics
            var statistics = GenerateStatisticsFromAnalysis(analysisResult);
            StatisticsText = statistics;

            LoadingProgress = 100;
            AppendToLog($"Анализ завершен. Обработано {filteredRoutes.Count} маршрутов");
            AppendToLog("График готов к просмотру");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка выполнения анализа");
            AppendToLog($"Ошибка анализа: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
            LoadingProgress = 0;
        }
    }

    // Остальные команды будут реализованы в следующем артефакте...

    #endregion

    #region Вспомогательные методы - аналоги Python private methods

    /// <summary>
    /// Обновляет список участков - аналог Python _update_sections
    /// </summary>
    private async Task UpdateSectionsAsync()
    {
        await Task.Run(() =>
        {
            var sections = _loadedRoutes
                .SelectMany(r => r.SectionNames)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            // Обновляем UI в главном потоке
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Sections.Clear();
                foreach (var section in sections)
                {
                    Sections.Add(section);
                }
            });
        });
    }

    /// <summary>
    /// Обновляет информацию о нормах и участке - аналог Python _update_norms_and_section_info
    /// </summary>
    private async Task UpdateNormsAndSectionInfoAsync()
    {
        if (string.IsNullOrEmpty(SelectedSection))
            return;

        await Task.Run(() =>
        {
            try
            {
                // Фильтруем маршруты по выбранному участку и настройкам фильтра
                var sectionRoutes = _loadedRoutes
                    .Where(r => r.SectionNames.Contains(SelectedSection))
                    .Where(r => !SingleSectionOnly || r.SectionNames.Count == 1)
                    .ToList();

                // Подсчитываем нормы для данного участка
                var normCounts = new Dictionary<string, int>();
                
                if (_loadedNorms.Any())
                {
                    foreach (var norm in _loadedNorms)
                    {
                        var matchingRoutes = sectionRoutes.Count(r => 
                            r.NormId == norm.Id || r.NormId?.ToString() == norm.Id);
                        
                        if (matchingRoutes > 0)
                            normCounts[norm.Id] = matchingRoutes;
                    }
                }

                // Обновляем UI в главном потоке
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Обновляем список норм с количеством маршрутов
                    NormsWithCounts.Clear();
                    NormsWithCounts.Add("Все нормы");

                    foreach (var (normId, count) in normCounts.OrderBy(kvp => kvp.Key))
                    {
                        NormsWithCounts.Add($"Норма {normId} ({count} маршрутов)");
                    }

                    // Обновляем информационные сообщения
                    var totalRoutes = _loadedRoutes.Count(r => r.SectionNames.Contains(SelectedSection));
                    var filteredRoutes = sectionRoutes.Count;
                    
                    SectionInfo = $"Всего маршрутов: {totalRoutes}";
                    if (SingleSectionOnly && filteredRoutes != totalRoutes)
                        SectionInfo += $" | После фильтра: {filteredRoutes}";

                    FilterInfo = SingleSectionOnly ? 
                        "Применен фильтр: только маршруты с одним участком" : 
                        "";

                    AppendToLog($"Участок '{SelectedSection}': найдено {totalRoutes} маршрутов, {normCounts.Count} норм");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обновления информации о нормах и участке");
                AppendToLog($"Ошибка обновления данных: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Добавляет сообщение в журнал операций - аналог Python log append
    /// </summary>
    private void AppendToLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}";
        
        // Обновляем UI в главном потоке
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            LogText += logEntry + Environment.NewLine;
        });
        
        _logger.LogInformation(message);
    }

    /// <summary>
    /// Извлекает ID нормы из строки вида "Норма 123 (45 маршрутов)"
    /// </summary>
    private string? ExtractNormId(string normText)
    {
        if (string.IsNullOrEmpty(normText) || normText == "Все нормы")
            return null;
            
        var match = System.Text.RegularExpressions.Regex.Match(normText, @"Норма (\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    #endregion

    #region Условия выполнения команд - аналоги Python button states

    private bool CanAnalyze() => 
        _loadedRoutes.Any() && 
        !string.IsNullOrEmpty(SelectedSection) && 
        !IsLoading;

    private bool CanFilterLocomotives() => 
        _locomotiveCoefficients.Any() && 
        !IsLoading;

    private void FilterLocomotives() 
    {
        try
        {
            if (!_loadedRoutes.Any())
            {
                AppendToLog("Предупреждение: Сначала загрузите данные маршрутов");
                return;
            }

            // Создаем ViewModel для диалога фильтра - аналог Python LocomotiveSelectorDialog
            var filterViewModel = new LocomotiveFilterViewModel(
                _loggerFactory.CreateLogger<LocomotiveFilterViewModel>(),
                _locomotiveFilterService,
                _loadedRoutes
            );

            // Показываем диалог фильтра
            var filterWindow = new LocomotiveFilterWindow(filterViewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var dialogResult = filterWindow.ShowDialog();

            // Обрабатываем результат - аналог Python _handle_filter_results
            if (dialogResult == true && filterWindow.DialogResult != null)
            {
                var result = filterWindow.DialogResult;
                
                UseCoefficients = result.UseCoefficients;
                ExcludeLowWork = result.ExcludeLowWork;
                
                // Обновляем информацию о примененном фильтре
                FilterInfo = $"Выбрано локомотивов: {result.SelectedLocomotives.Count}";
                if (result.UseCoefficients && result.Coefficients.Any())
                {
                    FilterInfo += $" | Коэффициенты: {result.Coefficients.Count}";
                }

                AppendToLog($"Применен фильтр локомотивов: выбрано {result.SelectedLocomotives.Count} локомотивов");
                
                // Автоматически перезапускаем анализ если участок выбран
                if (!string.IsNullOrEmpty(SelectedSection))
                {
                    _ = AnalyzeAsync(null);
                }
            }
            else
            {
                AppendToLog("Фильтрация локомотивов отменена");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка диалога фильтра локомотивов");
            AppendToLog($"Ошибка фильтра: {ex.Message}");
        }
    }

    private void EditNorms()
    {
        try
        {
            // Показываем информационное сообщение о будущем функционале - аналог Python _on_edit_norms_clicked
            if (string.IsNullOrEmpty(SelectedSection))
            {
                AppendToLog("Предупреждение: Выберите участок для редактирования норм");
                return;
            }

            var info = new StringBuilder();
            info.AppendLine("РЕДАКТОР НОРМ РАСХОДА");
            info.AppendLine("=".PadRight(40, '='));
            info.AppendLine();
            info.AppendLine("Функция интерактивного редактора норм будет реализована");
            info.AppendLine("в следующих версиях приложения.");
            info.AppendLine();
            info.AppendLine("ТЕКУЩИЕ ВОЗМОЖНОСТИ:");
            info.AppendLine("• Просмотр норм на интерактивных графиках");
            info.AppendLine("• Загрузка норм из HTML файлов ЦОММ");  
            info.AppendLine("• Валидация структуры норм");
            info.AppendLine("• Детальная информация о нормах");
            info.AppendLine();
            info.AppendLine("ПЛАНИРУЕМЫЕ ВОЗМОЖНОСТИ:");
            info.AppendLine("• Создание новых норм через графический интерфейс");
            info.AppendLine("• Редактирование точек существующих норм");
            info.AppendLine("• Интерполяция и сглаживание кривых норм");
            info.AppendLine("• Экспорт отредактированных норм в HTML формат");
            info.AppendLine("• Сравнение различных вариантов норм");
            info.AppendLine();
            info.AppendLine("Сейчас нормы загружаются из HTML файлов и хранятся");
            info.AppendLine("в высокопроизводительном кэше для быстрого доступа.");

            ShowInfoDialog("Редактор норм - Информация", info.ToString());
            AppendToLog("Показана информация о редакторе норм");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка показа информации о редакторе норм");
            AppendToLog($"Ошибка редактора норм: {ex.Message}");
        }
    }

    private void OpenPlot()
    {
        try
        {
            if (!_loadedRoutes.Any())
            {
                AppendToLog("Предупреждение: Нет данных для построения графика");
                return;
            }

            if (string.IsNullOrEmpty(SelectedSection))
            {
                AppendToLog("Предупреждение: Выберите участок для анализа");
                return;
            }

            // Фильтруем маршруты для графика
            var filteredRoutes = _loadedRoutes
                .Where(r => r.SectionNames.Contains(SelectedSection))
                .Where(r => !SingleSectionOnly || r.SectionNames.Count == 1)
                .ToList();

            if (!filteredRoutes.Any())
            {
                AppendToLog("Предупреждение: Нет маршрутов для отображения");
                return;
            }

            // Подготавливаем данные норм для графика
            var normFunctions = new Dictionary<string, object>();
            foreach (var norm in _loadedNorms.Where(n => 
                filteredRoutes.Any(r => r.NormId == n.Id || r.NormId?.ToString() == n.Id)))
            {
                normFunctions[norm.Id] = new Dictionary<string, object>
                {
                    ["points"] = norm.Points.ToList(),
                    ["norm_type"] = norm.Type,
                    ["description"] = norm.Description
                };
            }

            // Создаем и показываем окно графика - аналог Python _open_plot_in_browser
            var plotViewModel = new PlotWindowViewModel(
                _loggerFactory.CreateLogger<PlotWindowViewModel>(),
                _visualizationService,
                _dataAnalysisService,
                SelectedSection,
                filteredRoutes,
                normFunctions,
                ExtractNormId(SelectedNorm),
                SingleSectionOnly
            );

            var plotWindow = new PlotWindow(plotViewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            plotWindow.Show();
            AppendToLog($"График открыт для участка '{SelectedSection}' ({filteredRoutes.Count} маршрутов)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка открытия графика");
            AppendToLog($"Ошибка открытия графика: {ex.Message}");
        }
    }

    private async Task ShowNormStorageInfoAsync(object? parameter)
    {
        // Реализация информации о хранилище - аналог Python _show_norm_storage_info
        AppendToLog("Загрузка информации о хранилище норм...");
    }

    private async Task ValidateNormsAsync(object? parameter)
    {
        // Реализация валидации норм - аналог Python _validate_norms
        AppendToLog("Валидация норм...");
    }

    private async Task ShowRoutesStatisticsAsync(object? parameter)
    {
        // Реализация статистики маршрутов - аналог Python _show_routes_statistics
        AppendToLog("Подготовка статистики маршрутов...");
    }

    private async Task ExportExcelAsync(object? parameter)
    {
        try
        {
            if (!_loadedRoutes.Any())
            {
                AppendToLog("Предупреждение: Нет данных для экспорта");
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Экспорт в Excel",
                Filter = "Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
                DefaultExt = "xlsx",
                FileName = $"Анализ_{SelectedSection}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                IsLoading = true;
                LoadingMessage = "Экспорт данных в Excel...";
                LoadingProgress = 0;

                // Фильтруем данные для экспорта - аналог Python export data preparation
                var exportRoutes = _loadedRoutes
                    .Where(r => string.IsNullOrEmpty(SelectedSection) || r.SectionNames.Contains(SelectedSection))
                    .Where(r => !SingleSectionOnly || r.SectionNames.Count == 1)
                    .ToList();

                LoadingProgress = 50;
                
                // Выполняем экспорт через сервис - аналог Python _export_to_excel
                var success = await _excelExportService.ExportRoutesToExcelAsync(dialog.FileName, exportRoutes);

                LoadingProgress = 100;

                if (success)
                {
                    AppendToLog($"Данные экспортированы в Excel: {System.IO.Path.GetFileName(dialog.FileName)}");
                    ShowInfoDialog("Экспорт завершен", 
                        $"Данные успешно экспортированы в Excel!\n\n" +
                        $"Файл: {dialog.FileName}\n" +
                        $"Маршрутов: {exportRoutes.Count}");
                }
                else
                {
                    AppendToLog("Ошибка: Не удалось экспортировать данные в Excel");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта в Excel");
            AppendToLog($"Ошибка экспорта Excel: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
            LoadingProgress = 0;
        }
    }

    private async Task ExportPlotAsync(object? parameter)
    {
        try
        {
            if (!_loadedRoutes.Any())
            {
                AppendToLog("Предупреждение: Нет данных для экспорта графика");
                return;
            }

            if (string.IsNullOrEmpty(SelectedSection))
            {
                AppendToLog("Предупреждение: Выберите участок для экспорта графика");
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Экспорт графика",
                Filter = "PNG изображения (*.png)|*.png|JPEG изображения (*.jpg)|*.jpg|PDF документы (*.pdf)|*.pdf",
                DefaultExt = "png",
                FileName = $"График_{SelectedSection}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (dialog.ShowDialog() == true)
            {
                IsLoading = true;
                LoadingMessage = "Экспорт графика...";
                LoadingProgress = 0;

                // Подготавливаем данные для графика
                var filteredRoutes = _loadedRoutes
                    .Where(r => r.SectionNames.Contains(SelectedSection))
                    .Where(r => !SingleSectionOnly || r.SectionNames.Count == 1)
                    .ToList();

                LoadingProgress = 30;

                // Подготавливаем данные норм
                var normFunctions = new Dictionary<string, object>();
                foreach (var norm in _loadedNorms.Where(n => 
                    filteredRoutes.Any(r => r.NormId == n.Id || r.NormId?.ToString() == n.Id)))
                {
                    normFunctions[norm.Id] = new Dictionary<string, object>
                    {
                        ["points"] = norm.Points.ToList(),
                        ["norm_type"] = norm.Type,
                        ["description"] = norm.Description
                    };
                }

                LoadingProgress = 60;

                // Экспортируем через VisualizationService - аналог Python _export_plot  
                var success = await _visualizationService.ExportPlotToImageAsync(
                    dialog.FileName,
                    SelectedSection,
                    filteredRoutes,
                    normFunctions,
                    ExtractNormId(SelectedNorm),
                    SingleSectionOnly
                );

                LoadingProgress = 100;

                if (success)
                {
                    AppendToLog($"График экспортирован: {System.IO.Path.GetFileName(dialog.FileName)}");
                    ShowInfoDialog("Экспорт завершен",
                        $"График успешно экспортирован!\n\n" +
                        $"Файл: {dialog.FileName}\n" +
                        $"Участок: {SelectedSection}\n" +
                        $"Маршрутов: {filteredRoutes.Count}");
                }
                else
                {
                    AppendToLog("Ошибка: Не удалось экспортировать график");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта графика");
            AppendToLog($"Ошибка экспорта графика: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
            LoadingProgress = 0;
        }
    }

    private void ClearLogs()
    {
        LogText = string.Empty;
        AppendToLog("Журнал операций очищен");
    }

    /// <summary>
    /// Показывает информационный диалог - аналог Python _show_info_window
    /// </summary>
    private void ShowInfoDialog(string title, string content)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Window
            {
                Title = title,
                Width = 700,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow,
                ResizeMode = ResizeMode.CanResize
            };

            var mainPanel = new StackPanel { Margin = new Thickness(16) };

            // Содержимое в скроллируемом текстовом поле
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var contentTextBox = new TextBox
            {
                Text = content,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12)
            };

            scrollViewer.Content = contentTextBox;
            mainPanel.Children.Add(scrollViewer);

            // Кнопка закрытия
            var closeButton = new Button
            {
                Content = "Закрыть",
                Margin = new Thickness(0, 16, 0, 0),
                Padding = new Thickness(20, 8),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            closeButton.Click += (s, e) => dialog.Close();
            mainPanel.Children.Add(closeButton);

            dialog.Content = mainPanel;
            dialog.ShowDialog();
        });
    }

    /// <summary>
    /// Показывает результаты валидации в табличном формате
    /// Аналог Python _show_validation_results
    /// </summary>
    private void ShowValidationResults(List<string> valid, List<string> invalid, List<string> warnings)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Window
            {
                Title = "Результаты валидации норм",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow
            };

            var tabControl = new TabControl { Margin = new Thickness(16) };

            // Создаем табы для каждой категории - аналог Python validation tabs
            var tabs = new[]
            {
                ("Валидные", valid, System.Windows.Media.Brushes.Green),
                ("Невалидные", invalid, System.Windows.Media.Brushes.Red),
                ("Предупреждения", warnings, System.Windows.Media.Brushes.Orange)
            };

            foreach (var (title, lines, color) in tabs)
            {
                var tabItem = new TabItem
                {
                    Header = $"{title} ({lines.Count})"
                };

                var textBox = new TextBox
                {
                    Text = string.Join("\n", lines),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 11,
                    Margin = new Thickness(8),
                    Padding = new Thickness(8)
                };

                var scrollViewer = new ScrollViewer
                {
                    Content = textBox,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                tabItem.Content = scrollViewer;
                tabControl.Items.Add(tabItem);
            }

            var mainPanel = new DockPanel();
            
            // Кнопка закрытия внизу
            var closeButton = new Button
            {
                Content = "Закрыть",
                Padding = new Thickness(20, 8),
                Margin = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            closeButton.Click += (s, e) => dialog.Close();
            DockPanel.SetDock(closeButton, Dock.Bottom);
            mainPanel.Children.Add(closeButton);

            // TabControl заполняет оставшееся место
            mainPanel.Children.Add(tabControl);

            dialog.Content = mainPanel;
            dialog.ShowDialog();
        });
    }

    /// <summary>
    /// Корректное завершение работы - аналог Python on_closing
    /// </summary>
    public async Task ShutdownAsync()
    {
        try
        {
            AppendToLog("Завершение работы приложения...");
            
            // Очистка ресурсов если необходимо
            _loadedRoutes.Clear();
            _loadedNorms.Clear();
            _locomotiveCoefficients.Clear();
            
            await Task.Delay(500); // Пауза для завершения операций
            
            _logger.LogInformation("Приложение корректно завершило работу");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при завершении работы");
        }
    }

    #endregion

    #region INotifyPropertyChanged Implementation

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

    #endregion
}