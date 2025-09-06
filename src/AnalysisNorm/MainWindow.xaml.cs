// СОЗДАТЬ ФАЙЛ: MainWindow.xaml.cs (в корне src/AnalysisNorm/)

using System.Windows;
using System.Windows.Threading;
using Serilog;

namespace AnalysisNorm;

/// <summary>
/// Простое главное окно для первого запуска CHAT 2
/// Показывает статус системы и готовность к разработке
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();
        
        // Простой таймер для обновления статистики
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += UpdateStats;
        _timer.Start();
        
        Loaded += OnLoaded;
        
        Log.Information("MainWindow инициализировано");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Title = $"Анализатор норм расхода электроэнергии РЖД v2.0 - Запущен {DateTime.Now:HH:mm:ss}";
        Log.Information("MainWindow загружено успешно");
    }

    private void UpdateStats(object? sender, EventArgs e)
    {
        try
        {
            // Обновляем простую статистику
            var memoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            MemoryText.Text = $"{memoryMB:F1}MB";
            TimeText.Text = DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка обновления статистики");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            _timer?.Stop();
            Log.Information("MainWindow закрыто");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при закрытии MainWindow");
        }
        
        base.OnClosed(e);
    }
}