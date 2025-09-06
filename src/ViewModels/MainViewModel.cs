using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Threading;
using AnalysisNorm.Services;
using AnalysisNorm.Models;

namespace AnalysisNorm.ViewModels;

/// <summary>
/// Главная ViewModel - ИСПРАВЛЕНО: правильные using и типы
/// </summary>
public partial class MainViewModel : ObservableObject
{
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

    public MainViewModel(
        IPerformanceMonitor performanceMonitor,
        INormStorage normStorage,
        IApplicationLogger logger)
    {
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
            new("123", "Нажатие", new List<DataPoint>
            {
                new(10.0m, 120.5m),
                new(15.0m, 95.2m),
                new(20.0m, 85.7m),
                new(25.0m, 78.3m)
            }),
            new("456", "Вес", new List<DataPoint>
            {
                new(1000m, 150.0m),
                new(1500m, 125.0m),
                new(2000m, 105.0m),
                new(2500m, 95.0m)
            })
        };
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
            Task.Run(() =>
            {
                var metrics = _performanceMonitor.GetCurrentMetrics();
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

    [RelayCommand]
    private void ShowAbout()
    {
        _logger.LogInformation("Показ информации о программе");
        
        var aboutText = $@"Анализатор норм расхода электроэнергии РЖД

Версия: 1.0.0
Платформа: .NET 9
UI Framework: WPF + Material Design

Текущий статус:
• Память: {PerformanceMetrics.MemoryUsageMB:F1} MB
• Активных операций: {PerformanceMetrics.ActiveOperations}
• Норм в хранилище: {PerformanceMetrics.TotalNorms}";

        MessageBox.Show(aboutText, "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task<string> GetDiagnosticInfoAsync()
    {
        var metrics = _performanceMonitor.GetCurrentMetrics();
        var storageInfo = await _normStorage.GetStorageInfoAsync();
        
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

Система:
• Строк кода: ~{metrics.TotalCodeLines}
• Версия .NET: {Environment.Version}";
    }

    public async Task OnWindowClosingAsync()
    {
        try
        {
            _logger.LogInformation("Завершение работы MainViewModel");
            _uiUpdateTimer?.Stop();
            await Task.Delay(10); // Graceful shutdown
            _logger.LogInformation("MainViewModel корректно завершен");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при завершении MainViewModel");
        }
    }
}