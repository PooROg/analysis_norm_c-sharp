// ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Threading;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.ViewModels;

/// <summary>
/// Главная ViewModel - ОБНОВЛЕНО для интеграции с новой архитектурой
/// Добавлена поддержка IInteractiveNormsAnalyzer и расширенных метрик
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IInteractiveNormsAnalyzer _normsAnalyzer;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly INormStorage _normStorage;
    private readonly IApplicationLogger _logger;
    private readonly DispatcherTimer _uiUpdateTimer;

    [ObservableProperty]
    private string _statusMessage = "Готов к работе";

    [ObservableProperty]
    private DateTime _currentTime = DateTime.Now;

    [ObservableProperty]
    private PerformanceMetrics _performanceMetrics = new();

    [ObservableProperty]
    private bool _isInitialized;

    // НОВЫЕ свойства для интеграции с анализатором
    [ObservableProperty]
    private int _loadedRoutesCount;

    [ObservableProperty]
    private int _loadedNormsCount;

    [ObservableProperty]
    private string _availableSections = "Участки не загружены";

    [ObservableProperty]
    private bool _hasDataForAnalysis;

    public MainViewModel(
        IInteractiveNormsAnalyzer normsAnalyzer,
        IPerformanceMonitor performanceMonitor,
        INormStorage normStorage,
        IApplicationLogger logger)
    {
        _normsAnalyzer = normsAnalyzer ?? throw new ArgumentNullException(nameof(normsAnalyzer));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        _normStorage = normStorage ?? throw new ArgumentNullException(nameof(normStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _uiUpdateTimer.Tick += OnUiUpdateTimer;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            using var operation = _performanceMonitor.StartOperation("ViewModel_Initialization");
            
            StatusMessage = "Инициализация приложения...";
            _logger.LogInformation("Начало инициализации MainViewModel");

            await InitializeNormStorageAsync();
            await UpdateDataStatisticsAsync();
            StartPerformanceMonitoring();

            StatusMessage = "Приложение готово к работе";
            IsInitialized = true;
            
            _logger.LogInformation("MainViewModel успешно инициализирован");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации MainViewModel");
            StatusMessage = $"Ошибка инициализации: {ex.Message}";
            
            MessageBox.Show(
                $"Ошибка при инициализации:\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task InitializeNormStorageAsync()
    {
        // ОБНОВЛЕНО: Создаем тестовые данные совместимые с новой архитектурой
        var testNorms = CreateTestNorms();
        
        foreach (var norm in testNorms)
        {
            await _normStorage.SaveNormAsync(norm);
        }

        _logger.LogInformation("Хранилище норм инициализировано с {Count} тестовыми нормами", testNorms.Count);
    }

    private static List<Norm> CreateTestNorms()
    {
        return new List<Norm>
        {
            new("123_Нажатие", "Нажатие", new List<DataPoint>
            {
                new(10.0m, 120.5m),
                new(15.0m, 95.2m),
                new(20.0m, 85.7m),
                new(25.0m, 78.3m),
                new(30.0m, 72.1m)
            }, new NormMetadata("Тестовая норма нажатия", DateTime.UtcNow, "Система", "1.0")),
            
            new("456_Вес", "Вес", new List<DataPoint>
            {
                new(1000m, 150.0m),
                new(1500m, 125.0m),
                new(2000m, 105.0m),
                new(2500m, 95.0m),
                new(3000m, 88.5m)
            }, new NormMetadata("Тестовая норма по весу", DateTime.UtcNow, "Система", "1.0")),
            
            new("789_Комплексная", "Комплексная", new List<DataPoint>
            {
                new(5.0m, 200.0m),
                new(10.0m, 180.0m),
                new(15.0m, 160.0m),
                new(20.0m, 140.0m),
                new(25.0m, 125.0m),
                new(30.0m, 112.0m)
            }, new NormMetadata("Комплексная тестовая норма", DateTime.UtcNow, "Система", "1.0"))
        };
    }

    // НОВЫЙ метод для обновления статистики данных
    private async Task UpdateDataStatisticsAsync()
    {
        try
        {
            // Обновляем количество загруженных маршрутов
            LoadedRoutesCount = _normsAnalyzer.LoadedRoutes.Count;

            // Обновляем количество норм в хранилище
            var storageInfo = await _normStorage.GetStorageInfoAsync();
            LoadedNormsCount = storageInfo.TotalNorms;

            // Обновляем список доступных участков
            var sections = await _normsAnalyzer.GetAvailableSectionsAsync();
            var sectionsList = sections.Take(5).ToList(); // Показываем только первые 5
            
            if (sectionsList.Any())
            {
                AvailableSections = string.Join(", ", sectionsList) + 
                    (sections.Count() > 5 ? $" и еще {sections.Count() - 5}" : "");
                HasDataForAnalysis = true;
            }
            else
            {
                AvailableSections = "Участки не загружены";
                HasDataForAnalysis = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка обновления статистики данных");
        }
    }

    private void StartPerformanceMonitoring()
    {
        _uiUpdateTimer.Start();
        _logger.LogInformation("Мониторинг производительности запущен");
    }

    private void OnUiUpdateTimer(object? sender, EventArgs e)
    {
        try
        {
            // ИСПРАВЛЕНО: выполняем в background thread
            Task.Run(async () =>
            {
                var metrics = _performanceMonitor.GetCurrentMetrics();
                await UpdateDataStatisticsAsync(); // НОВОЕ: обновляем статистику данных
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PerformanceMetrics = metrics;
                    CurrentTime = DateTime.Now;
                    CheckPerformanceThresholds();
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении метрик производительности");
        }
    }

    private void CheckPerformanceThresholds()
    {
        if (PerformanceMetrics.MemoryUsageMB > 150)
        {
            if (!StatusMessage.Contains("памяти"))
            {
                StatusMessage = $"Внимание: высокое использование памяти ({PerformanceMetrics.MemoryUsageMB:F1} MB)";
                _logger.LogWarning("Высокое использование памяти: {MemoryMB} MB", PerformanceMetrics.MemoryUsageMB);
            }
        }
        else if (HasDataForAnalysis)
        {
            StatusMessage = $"Готов к анализу. Маршрутов: {LoadedRoutesCount}, норм: {LoadedNormsCount}";
        }
    }

    [RelayCommand]
    private async Task ShowDiagnosticsAsync()
    {
        try
        {
            using var operation = _performanceMonitor.StartOperation("Show_Diagnostics");
            
            var diagnosticInfo = await GetDiagnosticInfoAsync();
            
            MessageBox.Show(
                diagnosticInfo,
                "Диагностическая информация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при показе диагностики");
            MessageBox.Show(
                $"Ошибка при получении диагностики:\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    // НОВАЯ команда для тестового анализа
    [RelayCommand]
    private async Task RunTestAnalysisAsync()
    {
        try
        {
            using var operation = _performanceMonitor.StartOperation("Test_Analysis");
            StatusMessage = "Выполняется тестовый анализ...";
            
            // Получаем первый доступный участок для тестирования
            var sections = await _normsAnalyzer.GetAvailableSectionsAsync();
            var firstSection = sections.FirstOrDefault();
            
            if (string.IsNullOrEmpty(firstSection))
            {
                MessageBox.Show("Нет данных для анализа. Загрузите маршруты.", "Информация", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = await _normsAnalyzer.AnalyzeSectionAsync(firstSection);
            
            var message = $@"Тестовый анализ участка ""{firstSection}"" завершен:

Маршрутов проанализировано: {result.TotalRoutes}
Элементов анализа: {result.AnalysisItems.Count}
Время обработки: {result.ProcessingTime.TotalMilliseconds:F0} мс
Приемлемых результатов: {result.AcceptablePercentage:F1}%

Статистика отклонений:
• Среднее отклонение: {result.Statistics.MeanDeviation:F2}%
• Стандартное отклонение: {result.Statistics.StandardDeviation:F2}%
• Стабильность: {result.Statistics.StabilityAssessment}";

            MessageBox.Show(message, "Результат тестового анализа", 
                MessageBoxButton.OK, MessageBoxImage.Information);
                
            StatusMessage = $"Тестовый анализ завершен за {result.ProcessingTime.TotalMilliseconds:F0} мс";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при тестовом анализе");
            MessageBox.Show($"Ошибка анализа:\n{ex.Message}", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Ошибка при выполнении анализа";
        }
    }

    [RelayCommand]
    private void ShowAbout()
    {
        _logger.LogInformation("Показ информации о программе");
        
        var aboutText = $@"Анализатор норм расхода электроэнергии РЖД

Версия: 1.0.0 (CHAT 1 - дополненная версия)
Платформа: .NET 9
UI Framework: WPF + Material Design

Текущий статус:
• Память: {PerformanceMetrics.MemoryUsageMB:F1} MB
• Активных операций: {PerformanceMetrics.ActiveOperations}
• Загруженных маршрутов: {LoadedRoutesCount}
• Загруженных норм: {LoadedNormsCount}
• Доступных участков: {AvailableSections}

Новые возможности:
✓ Гиперболическая интерполяция норм
✓ Усиленный HTML парсер
✓ Классификация статусов отклонений
✓ Главный анализатор норм
✓ Расширенная диагностика";

        MessageBox.Show(aboutText, "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task<string> GetDiagnosticInfoAsync()
    {
        var metrics = _performanceMonitor.GetCurrentMetrics();
        var storageInfo = await _normStorage.GetStorageInfoAsync();
        var processingStats = _normsAnalyzer.GetProcessingStats();
        
        return $@"ДИАГНОСТИЧЕСКАЯ ИНФОРМАЦИЯ
==========================

Производительность:
• Использование памяти: {metrics.MemoryUsageMB:F1} MB
• Активных операций: {metrics.ActiveOperations}
• Всего операций: {metrics.TotalOperations}
• Среднее время операции: {metrics.AverageOperationTime:F1} мс

Хранилище норм:
• Норм в памяти: {storageInfo.TotalNorms}
• Кэшированных функций: {storageInfo.CachedFunctions}
• Память хранилища: {storageInfo.MemoryUsageBytes / 1024:F1} KB
• Последнее обновление: {storageInfo.LastUpdated:HH:mm:ss}

Анализатор норм:
• Загруженных маршрутов: {LoadedRoutesCount}
• Проанализированных участков: {_normsAnalyzer.AnalyzedResults.Count}
• Скорость обработки: {processingStats.ProcessingRate:F1} маршрутов/сек
• Процент успешности: {processingStats.SuccessRate:F1}%

Система:
• Строк кода: ~{metrics.TotalCodeLines}
• Версия .NET: {Environment.Version}
• Процессоров: {Environment.ProcessorCount}
• Время работы: {DateTime.Now - Process.GetCurrentProcess().StartTime:hh\:mm\:ss}

Новая архитектура (CHAT 1+):
✓ IInteractiveNormsAnalyzer реализован
✓ Гиперболическая интерполяция активна
✓ EnhancedHtmlParser готов к работе
✓ StatusClassifier функционирует";
    }

    public async Task OnWindowClosingAsync()
    {
        try
        {
            _logger.LogInformation("Завершение работы MainViewModel");
            _uiUpdateTimer?.Stop();
            
            // НОВОЕ: корректное освобождение ресурсов анализатора
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