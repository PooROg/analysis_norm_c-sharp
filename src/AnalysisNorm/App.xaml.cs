// ===================================================================
// ФАЙЛ: src/AnalysisNorm/App.xaml.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
// Устраняет ошибки CS1061 и CS0234
// ===================================================================

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
// ИСПРАВЛЕНИЕ CS0234: Добавлена правильная using директива
using AnalysisNorm.Services.DependencyInjection;

namespace AnalysisNorm;

/// <summary>
/// ИСПРАВЛЕННОЕ приложение для .NET 9 с корректной инициализацией DI
/// Устраняет ошибки компиляции CS1061 и CS0234
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private Microsoft.Extensions.Logging.ILogger? _logger;

    /// <summary>
    /// Инициализация приложения при запуске
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // КРИТИЧЕСКИ ВАЖНО: инициализируем Serilog ДО создания хоста
            InitializeSerilog();

            // Создаем хост приложения с ИСПРАВЛЕННОЙ конфигурацией DI
            _host = CreateHostBuilder(e.Args).Build();
            
            // Запускаем хост
            await _host.StartAsync();
            
            // Получаем логгер Microsoft.Extensions.Logging
            _logger = _host.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>();
            _logger.LogInformation("✅ Приложение AnalysisNorm v1.3.4 запущено успешно");

            // Выполняем инициализацию приложения
            await InitializeApplicationAsync();

            // Создаем и показываем главное окно
            await CreateMainWindowAsync();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            // Критическая ошибка запуска
            HandleStartupError(ex);
        }
    }

    /// <summary>
    /// Инициализация Serilog ДО создания хоста
    /// </summary>
    private static void InitializeSerilog()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var logsDir = Path.Combine(baseDir, "Logs");
        
        // Создаем папку логов если не существует
        Directory.CreateDirectory(logsDir);

        // Конфигурируем Serilog с минимальными настройками
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "AnalysisNorm")
            .Enrich.WithProperty("Version", "1.3.4.0")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logsDir, "analysisnorm-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("🚀 Serilog инициализирован");
    }

    /// <summary>
    /// Создание хоста приложения с ИСПРАВЛЕННОЙ конфигурацией DI
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppDomain.CurrentDomain.BaseDirectory)
            .ConfigureAppConfiguration((context, config) =>
            {
                // Очищаем существующую конфигурацию
                config.Sources.Clear();
                
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // Основной файл конфигурации
                var appSettingsPath = Path.Combine(baseDir, "appsettings.json");
                config.AddJsonFile(appSettingsPath, optional: true, reloadOnChange: true);
                
                // Переменные окружения
                config.AddEnvironmentVariables("ANALYSISNORM_");
                
                // Аргументы командной строки
                if (args.Length > 0)
                {
                    config.AddCommandLine(args);
                }

                Log.Information("📋 Конфигурация загружена из: {Path}", appSettingsPath);
            })
            .ConfigureServices((context, services) =>
            {
                try
                {
                    // ИСПРАВЛЕНИЕ CS1061: Теперь метод доступен благодаря using директиве
                    services.AddAnalysisNormServices(context.Configuration);
                    
                    Log.Information("✅ Все сервисы зарегистрированы успешно");
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "❌ КРИТИЧЕСКАЯ ОШИБКА регистрации сервисов");
                    throw;
                }
            })
            .UseSerilog(); // Важно: интегрируем Serilog с Microsoft.Extensions.Logging
    }

    /// <summary>
    /// Инициализация логики приложения
    /// </summary>
    private async Task InitializeApplicationAsync()
    {
        try
        {
            // Проверяем доступность всех ключевых сервисов
            var logger = _host?.Services.GetRequiredService<Services.Interfaces.IApplicationLogger>();
            var performanceMonitor = _host?.Services.GetRequiredService<Services.Interfaces.IPerformanceMonitor>();
            var normStorage = _host?.Services.GetRequiredService<Services.Interfaces.INormStorage>();

            logger?.LogInformation("🔧 Инициализация основных сервисов завершена");
            
            // Выполняем предварительную проверку системы
            await PerformSystemHealthCheckAsync();
            
            _logger?.LogInformation("🎯 Приложение готово к работе");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Ошибка инициализации приложения");
            throw;
        }
    }

    /// <summary>
    /// Проверка работоспособности системы при запуске
    /// </summary>
    private async Task PerformSystemHealthCheckAsync()
    {
        try
        {
            var performanceMonitor = _host?.Services.GetRequiredService<Services.Interfaces.IPerformanceMonitor>();
            
            // Измеряем время инициализации
            performanceMonitor?.StartMeasurement("SystemHealthCheck");
            
            // Проверяем доступ к файловой системе
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var testDirs = new[] { "Logs", "Config", "Exports" };
            
            foreach (var dir in testDirs)
            {
                var dirPath = Path.Combine(baseDir, dir);
                Directory.CreateDirectory(dirPath);
            }
            
            // Проверяем память и производительность
            var memory = GC.GetTotalMemory(false);
            _logger?.LogInformation("💾 Использование памяти: {Memory:N0} байт", memory);
            
            performanceMonitor?.EndMeasurement("SystemHealthCheck");
            var checkTime = performanceMonitor?.GetLastMeasurement("SystemHealthCheck") ?? TimeSpan.Zero;
            
            _logger?.LogInformation("⚡ Проверка системы завершена за {Time:F2}мс", checkTime.TotalMilliseconds);
            
            await Task.Delay(10); // Имитация асинхронной операции
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "⚠️ Некритическая ошибка проверки системы");
        }
    }

    /// <summary>
    /// Создание и отображение главного окна
    /// </summary>
    private async Task CreateMainWindowAsync()
    {
        try
        {
            // Получаем ViewModel из DI контейнера
            var mainViewModel = _host?.Services.GetService<MainViewModel>();
            
            // Создаем главное окно
            var mainWindow = new MainWindow();
            
            // Устанавливаем DataContext если ViewModel доступна
            if (mainViewModel != null)
            {
                mainWindow.DataContext = mainViewModel;
                _logger?.LogInformation("🎨 MainViewModel привязана к главному окну");
            }
            
            // Показываем окно
            mainWindow.Show();
            _logger?.LogInformation("🪟 Главное окно отображено");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Ошибка создания главного окна");
            
            // Показываем базовое окно без ViewModel в случае ошибки
            var fallbackWindow = new MainWindow();
            fallbackWindow.Show();
        }
    }

    /// <summary>
    /// Корректное завершение приложения
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            _logger?.LogInformation("🔄 Завершение работы приложения...");
            
            // Останавливаем хост
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
            
            // Закрываем Serilog
            Log.CloseAndFlush();
        }
        catch (Exception ex)
        {
            // Логируем ошибку завершения в последний раз
            Log.Fatal(ex, "❌ КРИТИЧЕСКАЯ ОШИБКА при завершении приложения");
        }
        finally
        {
            base.OnExit(e);
        }
    }

    /// <summary>
    /// Обработка критических ошибок запуска
    /// </summary>
    private static void HandleStartupError(Exception ex)
    {
        // Логируем в Serilog если он инициализирован
        Log.Fatal(ex, "💥 КРИТИЧЕСКАЯ ОШИБКА запуска AnalysisNorm");

        // Показываем пользователю понятное сообщение об ошибке
        var message = $"Не удалось запустить AnalysisNorm v1.3.4\n\n" +
                     $"Ошибка: {ex.Message}\n\n" +
                     $"Рекомендуемые действия:\n" +
                     $"1. Перезапустить приложение\n" +
                     $"2. Проверить права доступа к папке приложения\n" +
                     $"3. Переустановить приложение\n\n" +
                     $"Если проблема не решается, обратитесь к администратору.";

        MessageBox.Show(message, "Ошибка запуска AnalysisNorm v1.3.4", 
            MessageBoxButton.OK, MessageBoxImage.Error);

        // Принудительно завершаем приложение
        Environment.Exit(1);
    }

    /// <summary>
    /// Получение сервиса из DI контейнера (для использования в коде)
    /// </summary>
    public static T? GetService<T>() where T : class
    {
        return (Current as App)?._host?.Services?.GetService<T>();
    }

    /// <summary>
    /// Получение обязательного сервиса из DI контейнера
    /// </summary>
    public static T GetRequiredService<T>() where T : class
    {
        return (Current as App)?._host?.Services?.GetRequiredService<T>() 
               ?? throw new InvalidOperationException($"❌ Сервис {typeof(T).Name} не зарегистрирован в DI контейнере");
    }
}