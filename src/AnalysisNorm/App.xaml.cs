// ЗАМЕНИТЬ App.xaml.cs НА ЭТУ ВЕРСИЮ (создает окно в коде):

using Serilog;
using System.IO;
using System.Windows;

namespace AnalysisNorm;

/// <summary>
/// App.xaml.cs с созданием MainWindow в коде
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Создаем папку logs если её нет
            Directory.CreateDirectory("logs");

            // Простая настройка логирования
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/analysis_norm_.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("=== Запуск Analysis Norm C# (CHAT 2) ===");
            Log.Information("Версия .NET: {Version}", Environment.Version);

            // Создаем главное окно ПРОГРАММНО
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            base.OnStartup(e);

            Log.Information("Приложение запущено успешно");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Критическая ошибка при запуске приложения");
            MessageBox.Show($"Критическая ошибка запуска:\n{ex.Message}\n\nДетали:\n{ex.StackTrace}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.Information("=== Завершение работы Analysis Norm C# ===");
            Log.CloseAndFlush();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при завершении: {ex.Message}", "Ошибка");
        }
        
        base.OnExit(e);
    }
}