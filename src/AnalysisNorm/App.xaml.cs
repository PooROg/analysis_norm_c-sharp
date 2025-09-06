// App.xaml.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Windows;
using AnalysisNorm.Services.Extensions;

namespace AnalysisNorm;

/// <summary>
/// Главный класс приложения с настройкой DI контейнера и логирования
/// ОБНОВЛЕНО: Интеграция новых сервисов, улучшенная обработка ошибок
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Настройка логирования как можно раньше
            ConfigureLogging();
            
            Log.Information("=== Запуск анализатора норм расхода электроэнергии ===");
            Log.Information("Версия .NET: {DotNetVersion}", Environment.Version);
            Log.Information("Версия ОС: {OSVersion}", Environment.OSVersion);
            
            // Создание и настройка хоста
            _host = CreateHostBuilder(e.Args).Build();
            
            // Валидация сервисов
            _host.Services.ValidateServiceRegistration();
            Log.Information("Валидация DI контейнера успешно завершена");
            
            // Запуск хоста
            await _host.StartAsync();
            
            // Получение главного окна из DI
            var mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
            
            // Установка главного окна
            MainWindow = mainWindow;
            MainWindow.Show();
            
            Log.Information("Приложение успешно запущено");
            
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Критическая ошибка при запуске приложения");
            
            MessageBox.Show(
                $"Критическая ошибка при запуске:\n{ex.Message}\n\nПроверьте логи для получения подробной информации.",
                "Ошибка запуска",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
                
            Environment.Exit(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.Information("Завершение работы приложения");
            
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
            
            Log.Information("Приложение корректно завершено");
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
    /// Настройка логирования Serilog
    /// </summary>
    private static void ConfigureLogging()
    {
        // Создаем директорию для логов
        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AnalysisNorm", "Logs");
        Directory.CreateDirectory(logsDirectory);

        var logFilePath = Path.Combine(logsDirectory, "analyzer-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentUserName()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// Создание и настройка хоста с DI контейнером
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                // Настройка конфигурации
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // ОБНОВЛЕНО: Регистрация всех новых сервисов
                services.AddAnalysisNormServices();
                services.ConfigureAnalysisOptions();
                
                Log.Information("DI контейнер сконфигурирован с новыми сервисами");
            })
            .UseSerilog();

    /// <summary>
    /// Глобальная обработка необработанных исключений
    /// </summary>
    private void Application_DispatcherUnhandledException(object sender, 
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Необработанное исключение в UI потоке");
        
        var errorMessage = $"Произошла неожиданная ошибка:\n{e.Exception.Message}\n\nДетали записаны в лог файл.";
        
        MessageBox.Show(errorMessage, "Неожиданная ошибка", 
            MessageBoxButton.OK, MessageBoxImage.Error);
        
        // Помечаем исключение как обработанное, чтобы приложение не завершилось
        e.Handled = true;
    }

    /// <summary>
    /// Обработка исключений в других потоках
    /// </summary>
    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Log.Fatal(exception, "Критическое необработанное исключение в приложении. Завершающееся: {IsTerminating}", 
            e.IsTerminating);
            
        if (e.IsTerminating)
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Конструктор приложения
    /// </summary>
    public App()
    {
        // Регистрируем обработчики глобальных исключений
        DispatcherUnhandledException += Application_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }
}