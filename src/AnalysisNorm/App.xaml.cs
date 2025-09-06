using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using System.IO;
using System.Windows;

namespace AnalysisNorm;

/// <summary>
/// Главный класс приложения с настройкой DI контейнера и логирования
/// ИСПРАВЛЕНО: правильные using statements и существующие типы
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            ConfigureSerilog();
            
            Log.Information("=== ЗАПУСК АНАЛИЗАТОРА НОРМ РАСХОДА ЭЛЕКТРОЭНЕРГИИ ===");

            _host = CreateHostBuilder().Build();
            await _host.StartAsync();
            
            var mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
            mainWindow.Show();
            
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Критическая ошибка при запуске приложения");
            MessageBox.Show($"Критическая ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
            Log.CloseAndFlush();
        }
        finally
        {
            base.OnExit(e);
        }
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Core services
                services.AddMemoryCache();
                services.AddSingleton<Services.INormStorage, Services.MemoryNormStorage>();
                services.AddSingleton<Services.IPerformanceMonitor, Services.SimplePerformanceMonitor>();
                services.AddSingleton<Services.IApplicationLogger, Services.SerilogLogger>();
                services.AddTransient<Services.IFileService, Services.FileService>();
                
                // ViewModels
                services.AddTransient<ViewModels.MainViewModel>();
                
                // Views
                services.AddTransient<Views.MainWindow>();
            });
    }

    private static void ConfigureSerilog()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, "analyzer-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();
    }
}