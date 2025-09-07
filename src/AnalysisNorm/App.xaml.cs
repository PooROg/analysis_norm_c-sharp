// App.xaml.cs - МИНИМАЛЬНАЯ ВЕРСИЯ Serilog без проблемных зависимостей
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AnalysisNorm;

/// <summary>
/// ЭТАП 2: Приложение с минимальной настройкой Serilog
/// Убраны все проблемные зависимости и методы
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
            // Создаем хост приложения с минимальной конфигурацией Serilog
            _host = CreateHostBuilder(e.Args).Build();
            
            // Запускаем хост
            await _host.StartAsync();
            
            // Получаем логгер Microsoft.Extensions.Logging
            _logger = _host.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>();
            _logger.LogInformation("Приложение Analysis Norm запущено (Этап 2 - минимальный Serilog)");

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
    /// Создание хоста приложения с минимальной конфигурацией Serilog
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppDomain.CurrentDomain.BaseDirectory)
            .ConfigureAppConfiguration((context, config) =>
            {
                var env = context.HostingEnvironment;
                
                // Основной файл конфигурации
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                
                // Файл конфигурации для среды (если существует)
                config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                
                // Переменные окружения
                config.AddEnvironmentVariables();
                
                // Аргументы командной строки
                if (args.Length > 0)
                {
                    config.AddCommandLine(args);
                }
            })
            .ConfigureServices((context, services) =>
            {
                // Базовая конфигурация сервисов
                ConfigureServices(services, context.Configuration);
            })
            .UseSerilog((hostingContext, loggerConfiguration) =>
            {
                // МИНИМАЛЬНАЯ настройка Serilog без проблемных методов
                loggerConfiguration
                    .WriteTo.Console()
                    .WriteTo.File("Logs/application-.log", rollingInterval: RollingInterval.Day);

                Console.WriteLine("Serilog настроен с минимальной конфигурацией");
            });
    }

    /// <summary>
    /// Конфигурация сервисов DI контейнера
    /// </summary>
    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Базовые сервисы Microsoft.Extensions
        services.AddMemoryCache();
        services.AddLogging();
        
        // TODO: Здесь будут добавлены кастомные сервисы на следующих этапах
        // services.AddAnalysisNormServices(); // Будет добавлено на этапе 3
    }

    /// <summary>
    /// Завершение приложения
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            await CleanupResourcesAsync();
            
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
            // Логируем ошибку очистки, но не блокируем выход
            _logger?.LogError(ex, "Ошибка при завершении приложения");
        }
        finally
        {
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
            _logger?.LogInformation("Начало инициализации приложения");

            // Проверка и создание директорий
            await EnsureDirectoriesExistAsync();

            // Базовые проверки системы
            await PerformHealthChecksAsync();

            _logger?.LogInformation("Инициализация приложения завершена успешно");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка инициализации приложения");
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
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AnalysisNorm", "Diagnostics"),
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
                        _logger?.LogDebug("Создана директория: {Directory}", directory);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Не удалось создать директорию: {Directory}", directory);
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
                _logger?.LogDebug("Доступная память: {Memory} байт", memoryBefore);

                // Базовые проверки среды выполнения
                var currentDomain = AppDomain.CurrentDomain;
                _logger?.LogDebug("Домен приложения: {Domain}", currentDomain.FriendlyName);

                // Проверка версии .NET
                var runtimeVersion = Environment.Version;
                _logger?.LogDebug("Версия .NET: {Version}", runtimeVersion);

                _logger?.LogInformation("Базовые проверки системы завершены успешно");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Предупреждение при проверке состояния системы");
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

                _logger?.LogInformation("Главное окно создано и отображено");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка создания главного окна");
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
                _logger?.LogInformation("Начало очистки ресурсов");

                // Принудительная сборка мусора
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                _logger?.LogInformation("Очистка ресурсов завершена");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при очистке ресурсов");
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
            
            var errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] КРИТИЧЕСКАЯ ОШИБКА ЗАПУСКА: {ex}\n";
            File.AppendAllText(logPath, errorMessage);
        }
        catch
        {
            // Игнорируем ошибки логирования
        }

        // Показываем пользователю сообщение об ошибке
        var message = $"Критическая ошибка при запуске приложения:\n\n{ex.Message}\n\n" +
                     $"Детали ошибки сохранены в файл логов.\n" +
                     $"Обратитесь к администратору или перезапустите приложение.";

        MessageBox.Show(message, "Ошибка запуска Analysis Norm", 
            MessageBoxButton.OK, MessageBoxImage.Error);

        // Принудительно завершаем приложение
        Environment.Exit(1);
    }

    /// <summary>
    /// Получение сервиса из DI контейнера
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
               ?? throw new InvalidOperationException($"Сервис {typeof(T).Name} не зарегистрирован");
    }
}