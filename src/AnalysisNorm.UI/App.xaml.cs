using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Text;
using System.Windows;
using AnalysisNorm.Services;
using AnalysisNorm.UI.Views;
using AnalysisNorm.UI.ViewModels;

namespace AnalysisNorm.UI;

/// <summary>
/// WPF Application entry point with Dependency Injection
/// Соответствует main.py из Python версии, но с современной архитектурой .NET
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>
    /// Инициализация приложения (аналог setup из Python main.py)
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        try 
        {
            // Регистрируем кодировки для cp1251 (как в Python)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Создаем host с DI container
            _host = CreateHost();
            await _host.StartAsync();

            // Инициализируем базу данных
            await _host.Services.InitializeDatabaseAsync();

            // Создаем и показываем главное окно
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            Log.Information("=== АНАЛИЗАТОР НОРМ РАСХОДА ЭЛЕКТРОЭНЕРГИИ (C# версия) ===");
            Log.Information("Версия .NET: {Version}", Environment.Version);
            Log.Information("Рабочая директория: {Directory}", Environment.CurrentDirectory);
            Log.Information("Приложение запущено успешно");

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Критическая ошибка запуска приложения");
            MessageBox.Show(
                $"Критическая ошибка запуска:\n\n{ex.Message}", 
                "Анализатор норм расхода", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Корректное завершение приложения
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        try 
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            
            Log.Information("Приложение завершено корректно");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при завершении приложения");
        }
        finally
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    /// <summary>
    /// Обработчик необработанных исключений
    /// </summary>
    private void Application_DispatcherUnhandledException(object sender, 
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Необработанное исключение в UI потоке");
        
        MessageBox.Show(
            $"Произошла неожиданная ошибка:\n\n{e.Exception.Message}\n\nПодробности записаны в лог файл.",
            "Ошибка", 
            MessageBoxButton.OK, 
            MessageBoxImage.Error);
        
        e.Handled = true;
    }

    /// <summary>
    /// Создает и настраивает host с DI контейнером
    /// </summary>
    private IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                config.SetBasePath(basePath);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                
                // Добавляем конфигурацию для development окружения
                var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
                if (environment == "Development")
                {
                    config.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
                }
            })
            .ConfigureServices((context, services) =>
            {
                // Настраиваем все сервисы приложения
                services.ConfigureServices(context.Configuration);
                
                // Регистрируем WPF окна и ViewModels
                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<ChartWindow>();
                services.AddTransient<LocomotiveFilterWindow>();
                services.AddTransient<NormInfoWindow>();
                
                // Создаем необходимые директории
                var appSettings = context.Configuration
                    .GetSection("ApplicationSettings")
                    .Get<ApplicationSettings>() ?? new ApplicationSettings();
                appSettings.EnsureDirectories();
            })
            .ConfigureLogging()
            .UseConsoleLifetime()
            .Build();
    }
}

/// <summary>
/// Программный entry point (аналог if __name__ == "__main__" в Python)
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new App();
        
        // Подписываемся на глобальные исключения
        app.DispatcherUnhandledException += app.Application_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Log.Fatal((Exception)e.ExceptionObject, "Необработанное исключение в домене приложения");
        };

        app.Run();
    }
}