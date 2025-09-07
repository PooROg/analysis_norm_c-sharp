// ViewModels/Enhanced MainViewModel.cs (CHAT 3-4)
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Services.Implementation;
using AnalysisNorm.Infrastructure.Logging;
using AnalysisNorm.Models.Domain;
using AnalysisNorm.Configuration;
using System.Windows.Threading;
using System.Text;
using System.IO;

namespace AnalysisNorm.ViewModels;

/// <summary>
/// CHAT 3-4: Расширенная MainViewModel с полной интеграцией новых сервисов
/// Включает: AdvancedHtmlParser, ExcelExportService, улучшенную диагностику, автотестирование
/// Современный интерфейс с drag-and-drop, пошаговой валидацией, детализированными отчетами
/// </summary>
public partial class EnhancedMainViewModel : ObservableObject, INotifyPropertyChanged
{
    // Основные сервисы - обновленные для CHAT 3-4
    private readonly INormStorage _normStorage;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IApplicationLogger _logger;
    private readonly IInteractiveNormsAnalyzer _normsAnalyzer;
    private readonly IExcelExporter _excelExporter; // НОВЫЙ
    private readonly IConfigurationService _configService; // НОВЫЙ
    private readonly AdvancedHtmlParser _advancedHtmlParser; // НОВЫЙ
    private readonly DuplicateResolver _duplicateResolver; // НОВЫЙ
    private readonly SectionMerger _sectionMerger; // НОВЫЙ
    
    private readonly DispatcherTimer _uiUpdateTimer;
    private readonly DispatcherTimer _autoTestTimer; // НОВЫЙ: Автоматическое тестирование

    // ОБНОВЛЕННЫЕ Observable свойства для CHAT 3-4
    [ObservableProperty]
    private string _statusText = "Готов к работе (CHAT 3-4 Enhanced)";

    [ObservableProperty]
    private bool _isProcessing = false;

    [ObservableProperty]
    private int _loadedRoutesCount = 0;

    [ObservableProperty]
    private int _loadedNormsCount = 0;

    [ObservableProperty]
    private int _availableSections = 0;

    [ObservableProperty]
    private string _memoryUsage = "0 MB";

    [ObservableProperty]
    private string _processingStats = "Нет данных";

    // НОВЫЕ свойства для CHAT 3-4
    [ObservableProperty]
    private int _processedFiles = 0;

    [ObservableProperty]
    private int _duplicatesFound = 0;

    [ObservableProperty]
    private int _sectionsmerged = 0;

    [ObservableProperty]
    private string _lastOperationTime = "—";

    [ObservableProperty]
    private double _operationProgress = 0;

    [ObservableProperty]
    private bool _canLoadRoutes = true;

    [ObservableProperty]
    private bool _canLoadNorms = true;

    [ObservableProperty]
    private bool _canExportExcel = false;

    [ObservableProperty]
    private string _configurationStatus = "Загружено";

    [ObservableProperty]
    private bool _autoTestingEnabled = false;

    [ObservableProperty]
    private string _lastTestResult = "Тестирование не проводилось";

    // Коллекции для UI
    public ObservableCollection<string> RecentFiles { get; } = new();
    public ObservableCollection<LogMessage> LogMessages { get; } = new();
    public ObservableCollection<ProcessingStep> ProcessingSteps { get; } = new(); // НОВОЕ
    public ObservableCollection<DiagnosticAlert> DiagnosticAlerts { get; } = new(); // НОВОЕ
    public ObservableCollection<TestResult> AutoTestResults { get; } = new(); // НОВОЕ

    // ОБНОВЛЕННЫЕ команды для CHAT 3-4
    public ICommand LoadRoutesCommand { get; }
    public ICommand LoadNormsCommand { get; }
    public ICommand ExportToExcelCommand { get; } // НОВАЯ
    public ICommand RunDiagnosticsCommand { get; } // ОБНОВЛЕННАЯ
    public ICommand StartAutoTestCommand { get; } // НОВАЯ
    public ICommand ConfigureSettingsCommand { get; } // НОВАЯ
    public ICommand AnalyzeSectionCommand { get; }
    public ICommand ShowAboutCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand ImportConfigurationCommand { get; } // НОВАЯ
    public ICommand ExportConfigurationCommand { get; } // НОВАЯ

    // Данные о производительности и диагностике
    public EnhancedPerformanceMetrics PerformanceMetrics { get; private set; } = new();
    public ProcessingDiagnostics ProcessingDiagnostics { get; private set; } = new();

    public EnhancedMainViewModel(
        INormStorage normStorage,
        IPerformanceMonitor performanceMonitor,
        IApplicationLogger logger,
        IInteractiveNormsAnalyzer normsAnalyzer,
        IExcelExporter excelExporter,
        IConfigurationService configService,
        AdvancedHtmlParser advancedHtmlParser,
        DuplicateResolver duplicateResolver,
        SectionMerger sectionMerger)
    {
        _normStorage = normStorage ?? throw new ArgumentNullException(nameof(normStorage));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _normsAnalyzer = normsAnalyzer ?? throw new ArgumentNullException(nameof(normsAnalyzer));
        _excelExporter = excelExporter ?? throw new ArgumentNullException(nameof(excelExporter));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _advancedHtmlParser = advancedHtmlParser ?? throw new ArgumentNullException(nameof(advancedHtmlParser));
        _duplicateResolver = duplicateResolver ?? throw new ArgumentNullException(nameof(duplicateResolver));
        _sectionMerger = sectionMerger ?? throw new ArgumentNullException(nameof(sectionMerger));

        // ОБНОВЛЕННЫЕ команды для CHAT 3-4
        LoadRoutesCommand = new AsyncRelayCommand(ExecuteLoadRoutesAsync, () => CanLoadRoutes);
        LoadNormsCommand = new AsyncRelayCommand(ExecuteLoadNormsAsync, () => CanLoadNorms);
        ExportToExcelCommand = new AsyncRelayCommand(ExecuteExportToExcelAsync, () => CanExportExcel);
        RunDiagnosticsCommand = new AsyncRelayCommand(ExecuteRunDiagnosticsAsync);
        StartAutoTestCommand = new RelayCommand(ExecuteStartAutoTest);
        ConfigureSettingsCommand = new RelayCommand(ExecuteConfigureSettings);
        AnalyzeSectionCommand = new AsyncRelayCommand<string>(ExecuteAnalyzeSectionAsync);
        ShowAboutCommand = new RelayCommand(ExecuteShowAbout);
        ClearLogCommand = new RelayCommand(ExecuteClearLog);
        ImportConfigurationCommand = new AsyncRelayCommand(ExecuteImportConfigurationAsync);
        ExportConfigurationCommand = new AsyncRelayCommand(ExecuteExportConfigurationAsync);

        // Таймеры обновления
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiUpdateTimer.Tick += UpdatePerformanceMetrics;
        _uiUpdateTimer.Start();

        _autoTestTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5) // Автотестирование каждые 5 минут
        };
        _autoTestTimer.Tick += async (_, _) => await ExecuteAutoTestAsync();

        InitializeApplication();
    }

    /// <summary>
    /// НОВАЯ функция: Инициализация приложения с проверкой конфигурации
    /// </summary>
    private async void InitializeApplication()
    {
        try
        {
            AddLogMessage("Система", "Инициализация приложения (CHAT 3-4)", "Blue");
            
            // Проверяем конфигурацию
            await ValidateConfigurationAsync();
            
            // Инициализируем диагностические алерты
            InitializeDiagnosticAlerts();
            
            // Запускаем первичную диагностику
            await ExecuteRunDiagnosticsAsync();
            
            StatusText = "Приложение готово к работе";
            AddLogMessage("Система", "Инициализация завершена успешно", "Green");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации приложения");
            AddLogMessage("Система", $"Ошибка инициализации: {ex.Message}", "Red");
        }
    }

    /// <summary>
    /// ГЛАВНАЯ функция CHAT 3: Загрузка маршрутов с продвинутым парсингом
    /// </summary>
    private async Task ExecuteLoadRoutesAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Выберите HTML файлы с маршрутами",
            Filter = "HTML файлы (*.html;*.htm)|*.html;*.htm|Все файлы (*.*)|*.*",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() != true) return;

        try
        {
            IsProcessing = true;
            CanLoadRoutes = false;
            StatusText = "Загрузка маршрутов с продвинутым парсингом...";
            
            AddLogMessage("Парсинг", $"Начата загрузка {openFileDialog.FileNames.Length} HTML файлов", "Blue");
            
            // Очищаем предыдущие результаты
            ProcessingSteps.Clear();
            
            var allRoutes = new List<Route>();
            var totalDuplicates = 0;
            var totalMergedSections = 0;

            // Обрабатываем каждый файл с детальным логированием
            for (int i = 0; i < openFileDialog.FileNames.Length; i++)
            {
                var fileName = openFileDialog.FileNames[i];
                OperationProgress = (double)i / openFileDialog.FileNames.Length * 100;
                
                AddProcessingStep($"Обработка файла {Path.GetFileName(fileName)}", ProcessingStepStatus.InProgress);
                
                try
                {
                    var htmlContent = await File.ReadAllTextAsync(fileName);
                    var parseResult = await _advancedHtmlParser.ParseRoutesFromHtmlAsync(htmlContent, fileName);
                    
                    if (parseResult.IsSuccess && parseResult.Data != null)
                    {
                        var routes = parseResult.Data.ToList();
                        allRoutes.AddRange(routes);
                        
                        // Анализируем дубликаты для этого файла
                        var duplicationAnalysis = _duplicateResolver.AnalyzeDuplicationQuality(routes);
                        totalDuplicates += duplicationAnalysis.TotalDuplicates;
                        
                        // Анализируем объединения участков
                        var mergeAnalyses = routes.Select(r => _sectionMerger.AnalyzeMergePotential(r)).ToList();
                        totalMergedSections += mergeAnalyses.Sum(m => m.TotalDuplicateSections);
                        
                        UpdateProcessingStep(ProcessingSteps.Count - 1, 
                            $"✓ {Path.GetFileName(fileName)}: {routes.Count} маршрутов, {duplicationAnalysis.TotalDuplicates} дубликатов", 
                            ProcessingStepStatus.Completed);
                        
                        AddLogMessage("Парсинг", 
                            $"Файл {Path.GetFileName(fileName)}: {routes.Count} маршрутов, {duplicationAnalysis.TotalDuplicates} дубликатов", 
                            "Green");
                    }
                    else
                    {
                        UpdateProcessingStep(ProcessingSteps.Count - 1, 
                            $"✗ Ошибка обработки {Path.GetFileName(fileName)}: {parseResult.ErrorMessage}", 
                            ProcessingStepStatus.Error);
                            
                        AddLogMessage("Парсинг", $"Ошибка файла {Path.GetFileName(fileName)}: {parseResult.ErrorMessage}", "Red");
                    }
                }
                catch (Exception ex)
                {
                    UpdateProcessingStep(ProcessingSteps.Count - 1, 
                        $"✗ Исключение в файле {Path.GetFileName(fileName)}: {ex.Message}", 
                        ProcessingStepStatus.Error);
                        
                    _logger.LogError(ex, "Ошибка обработки файла {FileName}", fileName);
                }
            }

            // Финальные результаты
            LoadedRoutesCount = allRoutes.Count;
            ProcessedFiles = openFileDialog.FileNames.Length;
            DuplicatesFound = totalDuplicates;
            Sectionsmerged = totalMergedSections;
            CanExportExcel = allRoutes.Any();
            
            // Обновляем список доступных участков
            var sections = allRoutes.SelectMany(r => r.Sections.Select(s => s.Name)).Distinct().ToList();
            AvailableSections = sections.Count;
            
            // Добавляем файлы в список недавних
            foreach (var fileName in openFileDialog.FileNames)
            {
                if (RecentFiles.Contains(fileName)) RecentFiles.Remove(fileName);
                RecentFiles.Insert(0, fileName);
                if (RecentFiles.Count > 10) RecentFiles.RemoveAt(10);
            }

            var successMessage = $"Загружено {LoadedRoutesCount} маршрутов из {ProcessedFiles} файлов. " +
                               $"Дубликатов: {DuplicatesFound}, объединений: {SectionsChanged}";
            
            StatusText = successMessage;
            AddLogMessage("Результат", successMessage, "Green");
            
            // Создаем диагностический алерт при большом количестве дубликатов
            if (totalDuplicates > allRoutes.Count * 0.1) // Более 10% дубликатов
            {
                AddDiagnosticAlert(DiagnosticAlertLevel.Warning, 
                    "Высокий уровень дубликатов", 
                    $"Обнаружено {totalDuplicates} дубликатов ({(double)totalDuplicates/allRoutes.Count:P1}) от общего количества маршрутов");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка загрузки маршрутов");
            StatusText = $"Ошибка загрузки: {ex.Message}";
            AddLogMessage("Ошибка", $"Критическая ошибка: {ex.Message}", "Red");
        }
        finally
        {
            IsProcessing = false;
            CanLoadRoutes = true;
            OperationProgress = 0;
            LastOperationTime = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    /// <summary>
    /// ГЛАВНАЯ функция CHAT 4: Экспорт в Excel с полным форматированием
    /// </summary>
    private async Task ExecuteExportToExcelAsync()
    {
        if (!_normsAnalyzer.LoadedRoutes.Any())
        {
            MessageBox.Show("Нет загруженных маршрутов для экспорта", "Предупреждение", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Title = "Сохранить результаты анализа в Excel",
            Filter = "Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
            DefaultExt = "xlsx",
            FileName = GenerateDefaultExcelFileName()
        };

        if (saveFileDialog.ShowDialog() != true) return;

        try
        {
            IsProcessing = true;
            StatusText = "Экспорт в Excel...";
            
            AddLogMessage("Экспорт", $"Начат экспорт {LoadedRoutesCount} маршрутов в Excel", "Blue");
            AddProcessingStep("Подготовка данных для экспорта", ProcessingStepStatus.InProgress);

            // Получаем конфигурацию экспорта
            var exportConfig = _configService.GetConfiguration<ExcelExportConfiguration>();
            var exportOptions = new ExportOptions
            {
                IncludeSummaryStatistics = true,
                EnableConditionalFormatting = exportConfig.Formatting.EnableConditionalFormatting,
                FreezeHeaderRow = exportConfig.Formatting.FreezeHeaderRow
            };

            UpdateProcessingStep(ProcessingSteps.Count - 1, "✓ Данные подготовлены", ProcessingStepStatus.Completed);
            AddProcessingStep("Создание Excel файла с форматированием", ProcessingStepStatus.InProgress);

            var exportResult = await _excelExporter.ExportRoutesToExcelAsync(
                _normsAnalyzer.LoadedRoutes, 
                saveFileDialog.FileName, 
                exportOptions);

            if (exportResult.IsSuccess)
            {
                UpdateProcessingStep(ProcessingSteps.Count - 1, "✓ Excel файл создан", ProcessingStepStatus.Completed);
                
                // Создаем дополнительный диагностический файл
                if (_configService.GetConfiguration<DiagnosticsConfiguration>().Features.AutoExportDiagnostics)
                {
                    AddProcessingStep("Создание диагностического отчета", ProcessingStepStatus.InProgress);
                    await CreateDiagnosticExcelReportAsync(saveFileDialog.FileName);
                    UpdateProcessingStep(ProcessingSteps.Count - 1, "✓ Диагностический отчет создан", ProcessingStepStatus.Completed);
                }

                var fileInfo = new FileInfo(saveFileDialog.FileName);
                var successMessage = $"Экспорт завершен успешно. Файл: {Path.GetFileName(saveFileDialog.FileName)} ({fileInfo.Length / 1024} KB)";
                
                StatusText = successMessage;
                AddLogMessage("Экспорт", successMessage, "Green");

                // Предлагаем открыть файл
                if (exportConfig.AutoOpenAfterExport)
                {
                    var result = MessageBox.Show("Открыть созданный Excel файл?", "Экспорт завершен", 
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(saveFileDialog.FileName) { UseShellExecute = true });
                    }
                }
            }
            else
            {
                UpdateProcessingStep(ProcessingSteps.Count - 1, $"✗ Ошибка экспорта: {exportResult.ErrorMessage}", ProcessingStepStatus.Error);
                AddLogMessage("Экспорт", $"Ошибка экспорта: {exportResult.ErrorMessage}", "Red");
                MessageBox.Show($"Ошибка экспорта: {exportResult.ErrorMessage}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка экспорта в Excel");
            AddLogMessage("Ошибка", $"Критическая ошибка экспорта: {ex.Message}", "Red");
            MessageBox.Show($"Критическая ошибка экспорта: {ex.Message}", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
            LastOperationTime = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    /// <summary>
    /// РАСШИРЕННАЯ диагностика с автоматическим тестированием
    /// </summary>
    private async Task ExecuteRunDiagnosticsAsync()
    {
        try
        {
            StatusText = "Выполнение комплексной диагностики...";
            AddLogMessage("Диагностика", "Начата комплексная диагностика системы", "Blue");
            
            var diagnosticResults = new List<DiagnosticResult>();

            // 1. Проверка конфигурации
            AddProcessingStep("Проверка конфигурации", ProcessingStepStatus.InProgress);
            var configDiagnostics = _configService.GetDiagnostics();
            diagnosticResults.Add(new DiagnosticResult("Конфигурация", 
                $"Загружено {configDiagnostics.LoadedConfigurationsCount} конфигураций, размер: {configDiagnostics.FormattedSize}", 
                DiagnosticStatus.Good));
            UpdateProcessingStep(ProcessingSteps.Count - 1, "✓ Конфигурация проверена", ProcessingStepStatus.Completed);

            // 2. Проверка производительности
            AddProcessingStep("Анализ производительности", ProcessingStepStatus.InProgress);
            await UpdatePerformanceMetrics(null, null);
            var perfStatus = PerformanceMetrics.MemoryUsageMB > 150 ? DiagnosticStatus.Warning : DiagnosticStatus.Good;
            diagnosticResults.Add(new DiagnosticResult("Производительность", 
                $"Память: {PerformanceMetrics.MemoryUsageMB:F1} MB, Операций: {PerformanceMetrics.TotalOperations}", 
                perfStatus));
            UpdateProcessingStep(ProcessingSteps.Count - 1, "✓ Производительность проанализирована", ProcessingStepStatus.Completed);

            // 3. Проверка хранилища
            AddProcessingStep("Проверка хранилища норм", ProcessingStepStatus.InProgress);
            var storageInfo = await _normStorage.GetStorageInfoAsync();
            diagnosticResults.Add(new DiagnosticResult("Хранилище", 
                $"Норм: {storageInfo.TotalNorms}, Кэш: {storageInfo.CachedFunctions}, Память: {storageInfo.MemoryUsageBytes / 1024} KB", 
                DiagnosticStatus.Good));
            UpdateProcessingStep(ProcessingSteps.Count - 1, "✓ Хранилище проверено", ProcessingStepStatus.Completed);

            // 4. Тестирование парсинга (если есть недавние файлы)
            if (RecentFiles.Any())
            {
                AddProcessingStep("Тестирование HTML парсинга", ProcessingStepStatus.InProgress);
                var testResult = await TestHtmlParsingAsync(RecentFiles.First());
                diagnosticResults.Add(testResult);
                UpdateProcessingStep(ProcessingSteps.Count - 1, $"✓ Парсинг протестирован: {testResult.Status}", ProcessingStepStatus.Completed);
            }

            // Обновляем диагностические алерты
            ProcessDiagnosticResults(diagnosticResults);
            
            var summary = $"Диагностика завершена: {diagnosticResults.Count(r => r.Status == DiagnosticStatus.Good)} ОК, " +
                         $"{diagnosticResults.Count(r => r.Status == DiagnosticStatus.Warning)} предупреждений, " +
                         $"{diagnosticResults.Count(r => r.Status == DiagnosticStatus.Error)} ошибок";
            
            StatusText = summary;
            AddLogMessage("Диагностика", summary, "Green");
            
            ProcessingDiagnostics = new ProcessingDiagnostics
            {
                LastDiagnosticTime = DateTime.Now,
                DiagnosticResults = diagnosticResults,
                OverallStatus = diagnosticResults.Any(r => r.Status == DiagnosticStatus.Error) ? DiagnosticStatus.Error :
                               diagnosticResults.Any(r => r.Status == DiagnosticStatus.Warning) ? DiagnosticStatus.Warning :
                               DiagnosticStatus.Good
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка выполнения диагностики");
            AddLogMessage("Диагностика", $"Ошибка диагностики: {ex.Message}", "Red");
        }
    }

    /// <summary>
    /// НОВАЯ функция: Автоматическое тестирование HTML файлов
    /// </summary>
    private async Task<DiagnosticResult> TestHtmlParsingAsync(string testFilePath)
    {
        try
        {
            if (!File.Exists(testFilePath))
                return new DiagnosticResult("HTML Парсинг", "Тестовый файл не найден", DiagnosticStatus.Warning);

            var stopwatch = Stopwatch.StartNew();
            var htmlContent = await File.ReadAllTextAsync(testFilePath);
            var parseResult = await _advancedHtmlParser.ParseRoutesFromHtmlAsync(htmlContent, testFilePath);
            stopwatch.Stop();

            if (parseResult.IsSuccess && parseResult.Data != null)
            {
                var routes = parseResult.Data.ToList();
                var message = $"Успешно: {routes.Count} маршрутов за {stopwatch.ElapsedMilliseconds} мс";
                return new DiagnosticResult("HTML Парсинг", message, DiagnosticStatus.Good);
            }
            else
            {
                return new DiagnosticResult("HTML Парсинг", $"Ошибка: {parseResult.ErrorMessage}", DiagnosticStatus.Error);
            }
        }
        catch (Exception ex)
        {
            return new DiagnosticResult("HTML Парсинг", $"Исключение: {ex.Message}", DiagnosticStatus.Error);
        }
    }

    /// <summary>
    /// Включение/выключение автоматического тестирования
    /// </summary>
    private void ExecuteStartAutoTest()
    {
        AutoTestingEnabled = !AutoTestingEnabled;
        
        if (AutoTestingEnabled)
        {
            _autoTestTimer.Start();
            AddLogMessage("Автотест", "Автоматическое тестирование включено (каждые 5 минут)", "Blue");
        }
        else
        {
            _autoTestTimer.Stop();
            AddLogMessage("Автотест", "Автоматическое тестирование отключено", "Gray");
        }
    }

    /// <summary>
    /// Автоматическое тестирование по таймеру
    /// </summary>
    private async Task ExecuteAutoTestAsync()
    {
        if (!RecentFiles.Any()) return;

        try
        {
            var testFile = RecentFiles.First();
            var result = await TestHtmlParsingAsync(testFile);
            
            AutoTestResults.Insert(0, new TestResult
            {
                TestTime = DateTime.Now,
                TestFile = Path.GetFileName(testFile),
                Result = result.Message,
                Status = result.Status
            });

            // Оставляем только последние 20 результатов
            while (AutoTestResults.Count > 20)
                AutoTestResults.RemoveAt(AutoTestResults.Count - 1);

            LastTestResult = $"{DateTime.Now:HH:mm:ss} - {result.Status}: {result.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка автоматического тестирования");
        }
    }

    // Вспомогательные методы...

    private void AddProcessingStep(string description, ProcessingStepStatus status)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ProcessingSteps.Add(new ProcessingStep
            {
                StepNumber = ProcessingSteps.Count + 1,
                Description = description,
                Status = status,
                StartTime = DateTime.Now
            });
        });
    }

    private void UpdateProcessingStep(int index, string description, ProcessingStepStatus status)
    {
        if (index >= 0 && index < ProcessingSteps.Count)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var step = ProcessingSteps[index];
                ProcessingSteps[index] = step with 
                { 
                    Description = description, 
                    Status = status, 
                    EndTime = DateTime.Now 
                };
            });
        }
    }

    private string GenerateDefaultExcelFileName()
    {
        var config = _configService.GetConfiguration<ExcelExportConfiguration>();
        return config.DefaultFileNamePattern
            .Replace("{date:yyyyMMdd}", DateTime.Now.ToString("yyyyMMdd"))
            .Replace("{time:HHmm}", DateTime.Now.ToString("HHmm"));
    }

    private async Task CreateDiagnosticExcelReportAsync(string mainExcelPath)
    {
        try
        {
            var diagnosticPath = Path.ChangeExtension(mainExcelPath, ".diagnostics.xlsx");
            
            var duplicationAnalysis = _duplicateResolver.AnalyzeDuplicationQuality(_normsAnalyzer.LoadedRoutes.ToList());
            var mergeAnalyses = _normsAnalyzer.LoadedRoutes
                .Select(r => _sectionMerger.AnalyzeMergePotential(r))
                .ToList();

            await _excelExporter.ExportDiagnosticDataAsync(duplicationAnalysis, mergeAnalyses, diagnosticPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось создать диагностический Excel отчет");
        }
    }

    // Остальные методы остаются прежними, но с обновленной логикой...
    // (методы для загрузки норм, анализа участков, управления конфигурацией и т.д.)

    // Интерфейсы для новых моделей данных будут добавлены в следующем артефакте...
}

// НОВЫЕ модели данных для расширенной функциональности

public record ProcessingStep
{
    public int StepNumber { get; init; }
    public string Description { get; init; } = string.Empty;
    public ProcessingStepStatus Status { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    
    public TimeSpan? Duration => EndTime?.Subtract(StartTime);
    public string FormattedDuration => Duration?.TotalSeconds.ToString("F1") + " сек" ?? "—";
}

public enum ProcessingStepStatus
{
    InProgress,
    Completed,
    Error,
    Skipped
}

public record DiagnosticAlert
{
    public DiagnosticAlertLevel Level { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public bool IsRead { get; init; }
}

public enum DiagnosticAlertLevel
{
    Info,
    Warning,
    Error,
    Critical
}

public record TestResult
{
    public DateTime TestTime { get; init; }
    public string TestFile { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public DiagnosticStatus Status { get; init; }
}

public record DiagnosticResult
{
    public string Component { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DiagnosticStatus Status { get; init; }
}

public enum DiagnosticStatus
{
    Good,
    Warning,
    Error
}

public record EnhancedPerformanceMetrics
{
    public decimal MemoryUsageMB { get; init; }
    public int ActiveOperations { get; init; }
    public int TotalOperations { get; init; }
    public decimal AverageOperationTime { get; init; }
    public DateTime LastUpdate { get; init; }
}

public record ProcessingDiagnostics
{
    public DateTime LastDiagnosticTime { get; init; }
    public List<DiagnosticResult> DiagnosticResults { get; init; } = new();
    public DiagnosticStatus OverallStatus { get; init; }
}
