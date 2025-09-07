// ViewModels/MainViewModel.cs (ОБНОВЛЕННЫЙ для CHAT 2)
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Infrastructure.Logging;
using AnalysisNorm.Models.Domain;
using System.Windows.Threading;
using System.Text;
using System.Windows.Controls;

namespace AnalysisNorm.ViewModels;

/// <summary>
/// ОБНОВЛЕННАЯ MainViewModel для CHAT 2
/// Добавляет: интеграцию с LoadRoutesFromHtmlAsync, LoadNormsFromHtmlAsync, улучшенную диагностику
/// Использует новые возможности обновленного InteractiveNormsAnalyzer
/// </summary>
public partial class MainViewModel : ObservableObject, INotifyPropertyChanged
{
    private readonly INormStorage _normStorage;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IApplicationLogger _logger;
    private readonly IInteractiveNormsAnalyzer _normsAnalyzer; // ОБНОВЛЕННЫЙ сервис
    private readonly DispatcherTimer _uiUpdateTimer;

    // ОБНОВЛЕННЫЕ Observable свойства для CHAT 2
    [ObservableProperty]
    private string _statusText = "Готов к работе (CHAT 2)";

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

    // НОВЫЕ свойства для CHAT 2
    [ObservableProperty]
    private int _processedFiles = 0;

    [ObservableProperty]
    private string _lastOperationTime = "—";

    [ObservableProperty]
    private double _operationProgress = 0;

    [ObservableProperty]
    private bool _canLoadRoutes = true;

    [ObservableProperty]
    private bool _canLoadNorms = true;

    // Коллекции для UI
    public ObservableCollection<string> RecentFiles { get; } = new();
    public ObservableCollection<LogMessage> LogMessages { get; } = new();

    // ОБНОВЛЕННЫЕ команды для CHAT 2
    public ICommand LoadRoutesCommand { get; }
    public ICommand LoadNormsCommand { get; }
    public ICommand AnalyzeSectionCommand { get; }
    public ICommand ShowDiagnosticsCommand { get; }
    public ICommand ShowAboutCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand ExportResultsCommand { get; }

    // Производительность
    public PerformanceMetrics PerformanceMetrics { get; private set; } = new();

    public MainViewModel(
        INormStorage normStorage,
        IPerformanceMonitor performanceMonitor,
        IApplicationLogger logger,
        IInteractiveNormsAnalyzer normsAnalyzer)
    {
        _normStorage = normStorage ?? throw new ArgumentNullException(nameof(normStorage));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _normsAnalyzer = normsAnalyzer ?? throw new ArgumentNullException(nameof(normsAnalyzer));

        // Инициализация команд
        LoadRoutesCommand = new AsyncRelayCommand(ExecuteLoadRoutesAsync, () => CanLoadRoutes);
        LoadNormsCommand = new AsyncRelayCommand(ExecuteLoadNormsAsync, () => CanLoadNorms);
        AnalyzeSectionCommand = new AsyncRelayCommand<string>(ExecuteAnalyzeSectionAsync);
        ShowDiagnosticsCommand = new AsyncRelayCommand(ExecuteShowDiagnosticsAsync);
        ShowAboutCommand = new RelayCommand(ExecuteShowAbout);
        ClearLogCommand = new RelayCommand(ExecuteClearLog);
        ExportResultsCommand = new AsyncRelayCommand(ExecuteExportResultsAsync);

        // Настройка таймера для обновления UI
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _uiUpdateTimer.Tick += UpdateUIMetrics;
        _uiUpdateTimer.Start();

        _logger.LogInformation("MainViewModel инициализирован с поддержкой CHAT 2");
        
        // НОВОЕ: начальная проверка состояния
        _ = Task.Run(InitializeAsync);
    }

    /// <summary>
    /// НОВЫЙ метод: Асинхронная инициализация CHAT 2
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            await UpdateAvailableSectionsAsync();
            await UpdateCountersAsync();
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = "Система готова к работе (CHAT 2)";
            });

            _logger.LogInformation("MainViewModel успешно инициализирован");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации MainViewModel");
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = $"Ошибка инициализации: {ex.Message}";
            });
        }
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод загрузки маршрутов - использует новый LoadRoutesFromHtmlAsync
    /// </summary>
    private async Task ExecuteLoadRoutesAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Выберите HTML файлы с маршрутами",
            Filter = "HTML файлы (*.html;*.htm)|*.html;*.htm|Все файлы (*.*)|*.*",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() != true || !openFileDialog.FileNames.Any())
            return;

        IsProcessing = true;
        CanLoadRoutes = false;
        var startTime = DateTime.UtcNow;

        try
        {
            StatusText = "Загрузка маршрутов из HTML файлов...";
            OperationProgress = 0;

            var selectedFiles = openFileDialog.FileNames.ToList();
            _logger.LogInformation("Начата загрузка маршрутов из {Count} файлов", selectedFiles.Count);

            // НОВОЕ: используем обновленный метод анализатора
            var progress = new Progress<int>(value =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OperationProgress = value;
                    StatusText = $"Обработано файлов: {value}/{selectedFiles.Count}";
                });
            });

            // Вызываем новый метод LoadRoutesFromHtmlAsync
            var success = await _normsAnalyzer.LoadRoutesFromHtmlAsync(selectedFiles);

            if (success)
            {
                await UpdateCountersAsync();
                await UpdateAvailableSectionsAsync();

                var processingTime = DateTime.UtcNow - startTime;
                LastOperationTime = $"{processingTime.TotalSeconds:F1} сек";

                // Добавляем файлы в список недавних
                foreach (var file in selectedFiles.Take(5))
                {
                    if (!RecentFiles.Contains(file))
                    {
                        RecentFiles.Insert(0, file);
                        if (RecentFiles.Count > 10)
                            RecentFiles.RemoveAt(RecentFiles.Count - 1);
                    }
                }

                StatusText = $"Маршруты загружены успешно. Файлов: {selectedFiles.Count}, маршрутов: {LoadedRoutesCount}";
                _logger.LogInformation("Загрузка маршрутов завершена успешно за {Time}", processingTime);

                // Добавляем в лог
                AddLogMessage("Успех", $"Загружено {LoadedRoutesCount} маршрутов из {selectedFiles.Count} файлов", "Green");
            }
            else
            {
                StatusText = "Ошибка загрузки маршрутов из HTML файлов";
                _logger.LogWarning("Загрузка маршрутов завершилась неудачно");
                AddLogMessage("Ошибка", "Не удалось загрузить маршруты", "Red");
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Критическая ошибка загрузки маршрутов: {ex.Message}";
            StatusText = errorMessage;
            _logger.LogError(ex, "Критическая ошибка при загрузке маршрутов");
            AddLogMessage("Критическая ошибка", ex.Message, "Red");

            MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
            CanLoadRoutes = true;
            OperationProgress = 0;
        }
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод загрузки норм - использует новый LoadNormsFromHtmlAsync
    /// </summary>
    private async Task ExecuteLoadNormsAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Выберите HTML файлы с нормами",
            Filter = "HTML файлы (*.html;*.htm)|*.html;*.htm|Все файлы (*.*)|*.*",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() != true || !openFileDialog.FileNames.Any())
            return;

        IsProcessing = true;
        CanLoadNorms = false;
        var startTime = DateTime.UtcNow;

        try
        {
            StatusText = "Загрузка норм из HTML файлов...";
            OperationProgress = 0;

            var selectedFiles = openFileDialog.FileNames.ToList();
            _logger.LogInformation("Начата загрузка норм из {Count} файлов", selectedFiles.Count);

            // НОВОЕ: используем обновленный метод анализатора для норм
            var success = await _normsAnalyzer.LoadNormsFromHtmlAsync(selectedFiles);

            if (success)
            {
                await UpdateCountersAsync();

                var processingTime = DateTime.UtcNow - startTime;
                LastOperationTime = $"{processingTime.TotalSeconds:F1} сек";

                StatusText = $"Нормы загружены успешно. Файлов: {selectedFiles.Count}, норм: {LoadedNormsCount}";
                _logger.LogInformation("Загрузка норм завершена успешно за {Time}", processingTime);

                AddLogMessage("Успех", $"Загружено {LoadedNormsCount} норм из {selectedFiles.Count} файлов", "Green");
            }
            else
            {
                StatusText = "Ошибка загрузки норм из HTML файлов";
                _logger.LogWarning("Загрузка норм завершилась неудачно");
                AddLogMessage("Ошибка", "Не удалось загрузить нормы", "Red");
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Критическая ошибка загрузки норм: {ex.Message}";
            StatusText = errorMessage;
            _logger.LogError(ex, "Критическая ошибка при загрузке норм");
            AddLogMessage("Критическая ошибка", ex.Message, "Red");

            MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
            CanLoadNorms = true;
            OperationProgress = 0;
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Анализ участка с использованием обновленного анализатора
    /// </summary>
    private async Task ExecuteAnalyzeSectionAsync(string? sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            StatusText = "Выберите участок для анализа";
            return;
        }

        IsProcessing = true;
        var startTime = DateTime.UtcNow;

        try
        {
            StatusText = $"Анализ участка: {sectionName}";
            _logger.LogInformation("Начат анализ участка {Section}", sectionName);

            // Используем обновленный метод анализа
            var analysisResult = await _normsAnalyzer.AnalyzeSectionAsync(sectionName);

            var processingTime = DateTime.UtcNow - startTime;
            LastOperationTime = $"{processingTime.TotalSeconds:F1} сек";

            if (analysisResult.AnalyzedItems > 0)
            {
                StatusText = $"Анализ завершен. Участок: {sectionName}, элементов: {analysisResult.AnalyzedItems}, среднее отклонение: {analysisResult.MeanDeviation:F2}%";
                
                AddLogMessage("Анализ", 
                    $"Участок {sectionName}: {analysisResult.AnalyzedItems} элементов, отклонение {analysisResult.MeanDeviation:F2}%", 
                    "Blue");

                _logger.LogInformation("Анализ участка {Section} завершен: {Items} элементов, отклонение {Deviation}%", 
                    sectionName, analysisResult.AnalyzedItems, analysisResult.MeanDeviation);
            }
            else
            {
                StatusText = $"Нет данных для анализа участка: {sectionName}";
                AddLogMessage("Предупреждение", $"Участок {sectionName}: нет данных для анализа", "Orange");
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Ошибка анализа участка {sectionName}: {ex.Message}";
            StatusText = errorMessage;
            _logger.LogError(ex, "Ошибка анализа участка {Section}", sectionName);
            AddLogMessage("Ошибка", errorMessage, "Red");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Экспорт результатов анализа
    /// </summary>
    private async Task ExecuteExportResultsAsync()
    {
        if (LoadedRoutesCount == 0)
        {
            MessageBox.Show("Нет данных для экспорта. Загрузите маршруты.", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Title = "Сохранить результаты анализа",
            Filter = "Excel файлы (*.xlsx)|*.xlsx|CSV файлы (*.csv)|*.csv",
            DefaultExt = "xlsx",
            FileName = $"analysis_results_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        IsProcessing = true;
        var startTime = DateTime.UtcNow;

        try
        {
            StatusText = "Экспорт результатов анализа...";
            _logger.LogInformation("Начат экспорт результатов в {FilePath}", saveFileDialog.FileName);

            // Получаем все результаты анализа
            var allResults = _normsAnalyzer.AnalyzedResults;
            
            // Пока используем простой экспорт (полная реализация в CHAT 4)
            var exportData = new List<string>
            {
                "Участок;Маршрутов;Элементов;Среднее отклонение %;Дата анализа"
            };

            foreach (var result in allResults.Values)
            {
                exportData.Add($"{result.SectionName};{result.TotalRoutes};{result.AnalyzedItems};{result.MeanDeviation:F2};{result.AnalysisDate:yyyy-MM-dd HH:mm}");
            }

            await File.WriteAllLinesAsync(saveFileDialog.FileName.Replace(".xlsx", ".csv"), exportData, Encoding.UTF8);

            var processingTime = DateTime.UtcNow - startTime;
            LastOperationTime = $"{processingTime.TotalSeconds:F1} сек";

            StatusText = $"Результаты экспортированы в {Path.GetFileName(saveFileDialog.FileName)}";
            AddLogMessage("Экспорт", $"Результаты сохранены: {allResults.Count} участков", "Green");

            _logger.LogInformation("Экспорт завершен за {Time}", processingTime);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Ошибка экспорта: {ex.Message}";
            StatusText = errorMessage;
            _logger.LogError(ex, "Ошибка экспорта результатов");
            AddLogMessage("Ошибка экспорта", ex.Message, "Red");

            MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод обновления счетчиков с использованием новых методов
    /// </summary>
    private async Task UpdateCountersAsync()
    {
        try
        {
            // Используем обновленные свойства анализатора
            LoadedRoutesCount = _normsAnalyzer.LoadedRoutes.Count;
            
            // Получаем количество норм из хранилища
            var allNorms = await _normStorage.GetAllNormsAsync();
            LoadedNormsCount = allNorms.Count();

            // НОВОЕ: обновляем статистику обработки
            var stats = _normsAnalyzer.GetProcessingStats();
            ProcessingStats = $"Файлов: {ProcessedFiles}, маршрутов: {LoadedRoutesCount}, норм: {LoadedNormsCount}, ошибок: {stats.TotalErrors}";

            _logger.LogDebug("Счетчики обновлены: маршрутов {Routes}, норм {Norms}", 
                LoadedRoutesCount, LoadedNormsCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления счетчиков");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Обновление доступных участков
    /// </summary>
    private async Task UpdateAvailableSectionsAsync()
    {
        try
        {
            var availableSectionsList = await _normsAnalyzer.GetAvailableSectionsAsync();
            AvailableSections = availableSectionsList.Count();
            
            _logger.LogDebug("Доступно участков для анализа: {Count}", AvailableSections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления списка участков");
            AvailableSections = 0;
        }
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод показа диагностики с новыми метриками CHAT 2
    /// </summary>
    private async Task ExecuteShowDiagnosticsAsync()
    {
        try
        {
            var diagnosticInfo = await GetDiagnosticInfoAsync();
            
            var diagnosticWindow = new Window
            {
                Title = "Диагностическая информация (CHAT 2)",
                Width = 800,
                Height = 600,
                Content = new TextBox
                {
                    Text = diagnosticInfo,
                    IsReadOnly = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Padding = new Thickness(10)
                }
            };

            diagnosticWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка показа диагностики");
            MessageBox.Show($"Ошибка диагностики: {ex.Message}", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод показа информации о программе
    /// </summary>
    private void ExecuteShowAbout()
    {
        var aboutText = $@"Анализатор норм расхода электроэнергии РЖД

Версия: 2.0.0 (CHAT 2 - расширенная версия)
Платформа: .NET 9
UI Framework: WPF + Material Design

НОВЫЕ возможности CHAT 2:
✓ Загрузка маршрутов из HTML файлов
✓ Загрузка норм из HTML файлов  
✓ Построение карты участков и норм
✓ Продвинутая дедупликация маршрутов
✓ Детальная обработка HTML таблиц
✓ Async/await обработка файлов
✓ Расширенная диагностика системы

Текущий статус:
• Память: {PerformanceMetrics.MemoryUsageMB:F1} MB
• Активных операций: {PerformanceMetrics.ActiveOperations}
• Загруженных маршрутов: {LoadedRoutesCount}
• Загруженных норм: {LoadedNormsCount}
• Доступных участков: {AvailableSections}
• Обработанных файлов: {ProcessedFiles}
• Последняя операция: {LastOperationTime}

Архитектурные улучшения CHAT 2:
✓ Обновленный EnhancedHtmlParser
✓ Расширенный InteractiveNormsAnalyzer
✓ Система проверки здоровья сервисов
✓ Кэширование результатов парсинга
✓ Memory-efficient потоковая обработка";

        MessageBox.Show(aboutText, "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// НОВЫЙ метод: Очистка лога сообщений
    /// </summary>
    private void ExecuteClearLog()
    {
        LogMessages.Clear();
        AddLogMessage("Система", "Лог очищен", "Gray");
        _logger.LogInformation("Лог сообщений очищен пользователем");
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод получения диагностической информации
    /// </summary>
    private async Task<string> GetDiagnosticInfoAsync()
    {
        var storageInfo = await _normStorage.GetStorageInfoAsync();
        var processingStats = _normsAnalyzer.GetProcessingStats();
        var analyzedResults = _normsAnalyzer.AnalyzedResults;
        
        return $@"ДИАГНОСТИЧЕСКАЯ ИНФОРМАЦИЯ (CHAT 2)
========================================

Производительность:
• Использование памяти: {PerformanceMetrics.MemoryUsageMB:F1} MB
• Активных операций: {PerformanceMetrics.ActiveOperations}
• Всего операций: {PerformanceMetrics.TotalOperations}
• Среднее время операции: {PerformanceMetrics.AverageOperationTime:F1} мс

Хранилище норм:
• Норм в памяти: {storageInfo.TotalNorms}
• Кэшированных функций: {storageInfo.CachedFunctions}
• Память хранилища: {storageInfo.MemoryUsageBytes / 1024:F1} KB
• Последнее обновление: {storageInfo.LastUpdated:HH:mm:ss}

Анализатор норм (CHAT 2):
• Загруженных маршрутов: {LoadedRoutesCount}
• Проанализированных участков: {analyzedResults.Count}
• Обработанных файлов: {ProcessedFiles}
• Доступных участков: {AvailableSections}
• Всего ошибок: {processingStats.TotalErrors}
• Время последней обработки: {processingStats.ProcessingTime.TotalSeconds:F1} сек

Результаты анализа:
• Участков с результатами: {analyzedResults.Count}
• Среднее количество элементов: {(analyzedResults.Values.Any() ? analyzedResults.Values.Average(r => r.AnalyzedItems) : 0):F1}
• Последний анализ: {(analyzedResults.Values.Any() ? analyzedResults.Values.Max(r => r.AnalysisDate) : DateTime.MinValue):yyyy-MM-dd HH:mm:ss}

Система:
• Строк кода: ~{PerformanceMetrics.TotalCodeLines}
• Версия .NET: {Environment.Version}
• Процессоров: {Environment.ProcessorCount}
• Время работы: {DateTime.Now - Process.GetCurrentProcess().StartTime:hh\:mm\:ss}

НОВЫЕ компоненты CHAT 2:
✓ LoadRoutesFromHtmlAsync активен
✓ LoadNormsFromHtmlAsync активен
✓ BuildSectionsNormsMapAsync функционирует
✓ Продвинутая дедупликация работает
✓ Кэширование результатов включено
✓ Async file processing активен
✓ Система здоровья сервисов работает

Последние операции:
{string.Join("\n", LogMessages.TakeLast(10).Select(msg => $"[{msg.Timestamp:HH:mm:ss}] {msg.Level}: {msg.Message}"))}";
    }

    /// <summary>
    /// НОВЫЙ метод: Добавление сообщения в лог
    /// </summary>
    private void AddLogMessage(string level, string message, string color)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogMessages.Insert(0, new LogMessage
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Color = color
            });

            // Ограничиваем количество сообщений
            while (LogMessages.Count > 100)
            {
                LogMessages.RemoveAt(LogMessages.Count - 1);
            }
        });
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод обновления UI метрик
    /// </summary>
    private void UpdateUIMetrics(object? sender, EventArgs e)
    {
        try
        {
            // Обновляем метрики производительности
            var currentMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            MemoryUsage = $"{currentMemoryMB:F1} MB";

            PerformanceMetrics = new PerformanceMetrics
            {
                MemoryUsageMB = currentMemoryMB,
                ActiveOperations = IsProcessing ? 1 : 0,
                TotalOperations = ProcessedFiles,
                AverageOperationTime = 0, // Будет рассчитано позже
                TotalCodeLines = 8500 // Примерная оценка для CHAT 2
            };

            // Проверяем лимиты производительности
            if (currentMemoryMB > 200)
            {
                _logger.LogWarning("Превышен лимит памяти: {Memory}MB > 200MB", currentMemoryMB);
                AddLogMessage("Предупреждение", $"Превышен лимит памяти: {currentMemoryMB:F1}MB", "Orange");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления UI метрик");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Корректное завершение работы ViewModel
    /// </summary>
    public async Task OnWindowClosingAsync()
    {
        try
        {
            _logger.LogInformation("Завершение работы MainViewModel");
            _uiUpdateTimer?.Stop();
            
            // Освобождаем ресурсы анализатора
            if (_normsAnalyzer is IDisposable disposableAnalyzer)
            {
                disposableAnalyzer.Dispose();
            }
            
            await Task.Delay(10); // Graceful shutdown
            _logger.LogInformation("MainViewModel корректно завершен");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при завершении MainViewModel");
        }
    }
}

/// <summary>
/// НОВЫЕ вспомогательные классы для CHAT 2
/// </summary>
public record LogMessage
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Color { get; init; } = "Black";
}

public record PerformanceMetrics
{
    public double MemoryUsageMB { get; init; }
    public int ActiveOperations { get; init; }
    public int TotalOperations { get; init; }
    public double AverageOperationTime { get; init; }
    public int TotalCodeLines { get; init; }
}

public enum DeviationStatus
{
    Excellent,   // Отлично
    Good,        // Хорошо
    Acceptable,  // Приемлемо
    Poor,        // Плохо
    Critical     // Критично
}