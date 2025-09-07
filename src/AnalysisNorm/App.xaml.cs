// App.xaml.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ для .NET 9
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using AnalysisNorm.Services.DependencyInjection;

namespace AnalysisNorm;

/// <summary>
/// ИСПРАВЛЕННОЕ приложение для .NET 9 с корректной инициализацией DI
/// Устраняет ошибку FileNotFoundException
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
                    // ИСПРАВЛЕННАЯ регистрация сервисов
                    services.AddAnalysisNormServices(context.Configuration);
                    
                    Log.Information("✅ Все сервисы зарегистрированы успешно");
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "❌ КРИТИЧЕСКАЯ ОШИБКА регистрации сервисов");
                    throw;
                }
            })
            .UseSerilog(); // Используем Serilog как основной провайдер логирования
    }

    /// <summary>
    /// Завершение работы приложения
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            _logger?.LogInformation("🔄 Завершение работы приложения");

            // Останавливаем хост
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }

            // Очистка ресурсов
            await CleanupResourcesAsync();

            _logger?.LogInformation("✅ Приложение завершено корректно");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Ошибка при завершении приложения");
        }
        finally
        {
            // Закрываем Serilog
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    /// <summary>
    /// Инициализация приложения
    /// </summary>
    private async Task InitializeApplicationAsync()
    {
        try
        {
            _logger?.LogInformation("🔧 Начало инициализации приложения");

            // Проверка и создание директорий
            await EnsureDirectoriesExistAsync();

            // Базовые проверки системы
            await PerformHealthChecksAsync();

            _logger?.LogInformation("✅ Инициализация приложения завершена успешно");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Ошибка инициализации приложения");
            throw;
        }
    }

    /// <summary>
    /// Проверка и создание необходимых директорий
    /// </summary>
    private async Task EnsureDirectoriesExistAsync()
    {
        await Task.Run(() =>
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var directories = new[]
            {
                Path.Combine(baseDir, "Logs"),
                Path.Combine(baseDir, "Config"),
                Path.Combine(baseDir, "Exports"),
                Path.Combine(baseDir, "SampleData"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AnalysisNorm"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AnalysisNorm", "Exports"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnalysisNorm"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnalysisNorm", "Logs")
            };

            foreach (var directory in directories)
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        _logger?.LogDebug("📁 Создана директория: {Directory}", directory);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ Не удалось создать директорию: {Directory}", directory);
                }
            }
        });
    }

    /// <summary>
    /// Базовые проверки работоспособности системы
    /// </summary>
    private async Task PerformHealthChecksAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                // Проверка доступной памяти
                var memoryBefore = GC.GetTotalMemory(false);
                _logger?.LogDebug("💾 Доступная память: {Memory} байт", memoryBefore);

                // Проверка версии .NET
                var runtimeVersion = Environment.Version;
                _logger?.LogDebug("🔧 Версия .NET: {Version}", runtimeVersion);

                // Проверка рабочей директории
                var workingDir = Environment.CurrentDirectory;
                _logger?.LogDebug("📂 Рабочая директория: {Directory}", workingDir);

                _logger?.LogInformation("✅ Базовые проверки системы завершены успешно");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ Предупреждение при проверке состояния системы");
            }
        });
    }

    /// <summary>
    /// Создание и отображение главного окна
    /// </summary>
    private async Task CreateMainWindowAsync()
    {
        // Создание окна должно происходить в UI потоке
        await Dispatcher.InvokeAsync(() =>
        {
            try
            {
                // Создаем главное окно
                var mainWindow = new MainWindow();

                // Устанавливаем главное окно
                MainWindow = mainWindow;

                // Показываем окно
                mainWindow.Show();

                _logger?.LogInformation("🪟 Главное окно создано и отображено");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Ошибка создания главного окна");
                throw;
            }
        });
    }

    /// <summary>
    /// Очистка ресурсов при завершении
    /// </summary>
    private async Task CleanupResourcesAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                _logger?.LogInformation("🧹 Начало очистки ресурсов");

                // Принудительная сборка мусора
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                _logger?.LogInformation("✅ Очистка ресурсов завершена");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Ошибка при очистке ресурсов");
            }
        });
    }

    /// <summary>
    /// Обработка критических ошибок запуска
    /// </summary>
    private void HandleStartupError(Exception ex)
    {
        // Логирование критической ошибки в файл
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AnalysisNorm", "Logs", "startup_errors.log"
            );
            
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            
            var errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ❌ КРИТИЧЕСКАЯ ОШИБКА ЗАПУСКА:\n{ex}\n\n";
            File.AppendAllText(logPath, errorMessage);
        }
        catch
        {
            // Игнорируем ошибки логирования
        }

        // Показываем пользователю сообщение об ошибке
        var message = $"❌ Критическая ошибка при запуске приложения:\n\n{ex.Message}\n\n" +
                     $"Версия сборки: 1.3.4.0\n" +
                     $"Детали ошибки сохранены в файл логов.\n\n" +
                     $"Попробуйте:\n" +
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