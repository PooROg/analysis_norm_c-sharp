// App.xaml.cs - ИСПРАВЛЕНА: убраны конвертеры (они теперь в отдельном файле)
using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using AnalysisNorm.Services.DependencyInjection;
using AnalysisNorm.ViewModels;
using AnalysisNorm.Infrastructure.Logging;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Configuration;

namespace AnalysisNorm;

/// <summary>
/// CHAT 3-4: Обновленное приложение с полной поддержкой DI и конфигурации
/// ИСПРАВЛЕНО: убраны конвертеры (они перенесены в Converters/ValueConverters.cs)
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private IApplicationLogger? _logger;

    /// <summary>
    /// Инициализация приложения при запуске
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Создаем хост приложения с полной конфигурацией DI
            _host = CreateHostBuilder(e.Args).Build();
            
            // Запускаем хост
            await _host.StartAsync();
            
            // Получаем логгер
            _logger = _host.Services.GetRequiredService<IApplicationLogger>();
            _logger.LogInformation("Приложение Analysis Norm запущено (CHAT 3-4)");

            // Выполняем инициализацию приложения
            await InitializeApplicationAsync();

            // Создаем и показываем главное окно
            await CreateAndShowMainWindowAsync();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            // Критическая ошибка запуска
            HandleStartupError(ex);
        }
    }

    /// <summary>
    /// Создание конфигурации хоста приложения
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppDomain.CurrentDomain.BaseDirectory)
            .ConfigureAppConfiguration((context, config) =>
            {
                var env = context.HostingEnvironment;
                
                // Основной файл конфигурации
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                
                // Файл конфигурации для среды
                config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                
                // НОВЫЕ конфигурационные файлы для CHAT 4
                config.AddJsonFile("Config/excel_export.json", optional: true, reloadOnChange: true);
                config.AddJsonFile("Config/html_parsing.json", optional: true, reloadOnChange: true);
                config.AddJsonFile("Config/performance.json", optional: true, reloadOnChange: true);
                config.AddJsonFile("Config/diagnostics.json", optional: true, reloadOnChange: true);
                
                // Переменные окружения
                config.AddEnvironmentVariables(prefix: "ANALYSISNORM_");
                
                // Аргументы командной строки
                if (args.Length > 0)
                {
                    config.AddCommandLine(args);
                }

                // Пользовательские секреты (только для разработки)
                if (env.IsDevelopment())
                {
                    config.AddUserSecrets<App>();
                }
            })
            .ConfigureServices((context, services) =>
            {
                // Регистрация всех сервисов Analysis Norm
                services.AddAnalysisNormServices(context.Configuration);
                
                // Конфигурация для конкретной среды
                services.ConfigureForEnvironment(context.HostingEnvironment, context.Configuration);
            })
            .UseSerilog(); // Используем Serilog как основной логгер
    }

    /// <summary>
    /// Инициализация приложения с проверками
    /// </summary>
    private async Task InitializeApplicationAsync()
    {
        if (_host?.Services == null) return;

        try
        {
            _logger?.LogInformation("Начинается инициализация приложения");

            // Проверяем и создаем необходимые директории
            await EnsureDirectoriesExistAsync();

            // Инициализируем конфигурации
            await InitializeConfigurationsAsync();

            // Выполняем проверки состояния системы
            await PerformStartupHealthChecksAsync();

            // Инициализируем сервисы
            await InitializeServicesAsync();

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
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Documents), "AnalysisNorm"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Documents), "AnalysisNorm", "Exports"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Documents), "AnalysisNorm", "Diagnostics"),
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
    /// Инициализация конфигураций
    /// </summary>
    private async Task InitializeConfigurationsAsync()
    {
        if (_host?.Services == null) return;

        var configService = _host.Services.GetRequiredService<IConfigurationService>();
        
        try
        {
            // Загружаем все основные конфигурации
            var excelConfig = configService.GetConfiguration<ExcelExportConfiguration>();
            var htmlConfig = configService.GetConfiguration<HtmlParsingConfiguration>();
            var perfConfig = configService.GetConfiguration<PerformanceConfiguration>();
            var diagConfig = configService.GetConfiguration<DiagnosticsConfiguration>();

            _logger?.LogInformation("Конфигурации инициализированы: Excel={ExcelValid}, HTML={HtmlValid}, Performance={PerfValid}, Diagnostics={DiagValid}",
                excelConfig != null, htmlConfig != null, perfConfig != null, diagConfig != null);

            // Выполняем валидацию конфигураций
            await ValidateConfigurationsAsync(configService);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка инициализации конфигураций");
        }
    }

    /// <summary>
    /// Валидация конфигураций
    /// </summary>
    private async Task ValidateConfigurationsAsync(IConfigurationService configService)
    {
        await Task.Yield();

        try
        {
            var diagnostics = configService.GetDiagnostics();
            
            if (diagnostics.LoadedConfigurationsCount < 4)
            {
                _logger?.LogWarning("Загружено только {Count} конфигураций из ожидаемых 4", 
                    diagnostics.LoadedConfigurationsCount);
            }

            _logger?.LogInformation("Валидация конфигурации завершена: конфигураций {Count}", 
                diagnostics.LoadedConfigurationsCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка валидации конфигураций");
        }
    }

    /// <summary>
    /// Проверки состояния системы при запуске
    /// </summary>
    private async Task PerformStartupHealthChecksAsync()
    {
        if (_host?.Services == null) return;

        try
        {
            var systemDiagnostics = _host.Services.GetRequiredService<ISystemDiagnostics>();
            var healthCheck = await systemDiagnostics.QuickHealthCheckAsync();

            if (healthCheck.IsHealthy)
            {
                _logger?.LogInformation("Проверка состояния системы пройдена успешно: {Status}", healthCheck.Status);
            }
            else
            {
                _logger?.LogWarning("Проблемы в проверке состояния системы: {Status}", healthCheck.Status);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Не удалось выполнить проверку состояния системы");
        }
    }

    /// <summary>
    /// Инициализация сервисов
    /// </summary>
    private async Task InitializeServicesAsync()
    {
        if (_host?.Services == null) return;

        try
        {
            // Прогреваем кэш сервисов
            var normStorage = _host.Services.GetRequiredService<INormStorage>();
            var performanceMonitor = _host.Services.GetRequiredService<IPerformanceMonitor>();

            // Запускаем мониторинг производительности
            performanceMonitor.StartOperation("Application_Startup");

            await Task.Delay(100); // Небольшая задержка для инициализации

            performanceMonitor.EndOperation("Application_Startup");

            _logger?.LogInformation("Сервисы инициализированы успешно");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка инициализации сервисов");
        }
    }

    /// <summary>
    /// Создание и отображение главного окна
    /// </summary>
    private async Task CreateAndShowMainWindowAsync()
    {
        if (_host?.Services == null) return;

        try
        {
            // Получаем главную ViewModel
            var mainViewModel = _host.Services.GetRequiredService<EnhancedMainViewModel>();
            
            // Создаем главное окно
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // Устанавливаем главное окно
            MainWindow = mainWindow;

            // Загружаем настройки окна
            await LoadWindowSettingsAsync(mainWindow);

            // Показываем окно
            mainWindow.Show();

            _logger?.LogInformation("Главное окно создано и отображено");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка создания главного окна");
            throw;
        }
    }

    /// <summary>
    /// Загрузка настроек окна
    /// </summary>
    private async Task LoadWindowSettingsAsync(Window window)
    {
        if (_host?.Services == null) return;

        try
        {
            var userPreferences = _host.Services.GetRequiredService<IUserPreferencesService>();
            var preferences = await userPreferences.LoadUserPreferencesAsync();

            // Применяем настройки позиции и размера окна
            if (preferences.MainWindowPosition.Width > 0 && preferences.MainWindowPosition.Height > 0)
            {
                window.Width = preferences.MainWindowPosition.Width;
                window.Height = preferences.MainWindowPosition.Height;
                
                if (preferences.MainWindowPosition.X >= 0 && preferences.MainWindowPosition.Y >= 0)
                {
                    window.Left = preferences.MainWindowPosition.X;
                    window.Top = preferences.MainWindowPosition.Y;
                }
                
                if (preferences.MainWindowPosition.IsMaximized)
                {
                    window.WindowState = WindowState.Maximized;
                }
            }

            _logger?.LogDebug("Настройки окна загружены");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Не удалось загрузить настройки окна");
        }
    }

    /// <summary>
    /// Сохранение настроек окна при закрытии
    /// </summary>
    private async Task SaveWindowSettingsAsync(Window window)
    {
        if (_host?.Services == null) return;

        try
        {
            var userPreferences = _host.Services.GetRequiredService<IUserPreferencesService>();
            var preferences = await userPreferences.LoadUserPreferencesAsync();

            // Обновляем настройки окна
            var updatedPosition = preferences.MainWindowPosition with
            {
                X = (int)window.Left,
                Y = (int)window.Top,
                Width = (int)window.Width,
                Height = (int)window.Height,
                IsMaximized = window.WindowState == WindowState.Maximized
            };

            var updatedPreferences = preferences with 
            { 
                MainWindowPosition = updatedPosition,
                LastModified = DateTime.UtcNow
            };

            await userPreferences.SaveUserPreferencesAsync(updatedPreferences);
            _logger?.LogDebug("Настройки окна сохранены");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Не удалось сохранить настройки окна");
        }
    }

    /// <summary>
    /// Обработка завершения работы приложения
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            _logger?.LogInformation("Начинается завершение работы приложения");

            // Сохраняем настройки окна
            if (MainWindow != null)
            {
                await SaveWindowSettingsAsync(MainWindow);
            }

            // Выполняем очистку ресурсов
            await CleanupResourcesAsync();

            // Останавливаем хост
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }

            _logger?.LogInformation("Приложение завершено успешно");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при завершении работы приложения");
        }
        finally
        {
            // Очищаем Serilog
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    /// <summary>
    /// Очистка ресурсов приложения
    /// </summary>
    private async Task CleanupResourcesAsync()
    {
        if (_host?.Services == null) return;

        try
        {
            // Останавливаем мониторинг производительности
            var performanceMonitor = _host.Services.GetService<IPerformanceMonitor>();
            performanceMonitor?.Dispose();

            // Очищаем кэш
            var memoryCache = _host.Services.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            if (memoryCache is IDisposable disposableCache)
            {
                disposableCache.Dispose();
            }

            // Принудительная сборка мусора
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await Task.Delay(100); // Небольшая задержка для завершения операций

            _logger?.LogDebug("Очистка ресурсов завершена");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ошибки при очистке ресурсов");
        }
    }

    /// <summary>
    /// Обработка критической ошибки запуска
    /// </summary>
    private void HandleStartupError(Exception ex)
    {
        try
        {
            // Логируем в файл если возможно
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "startup_error.log");
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

/// <summary>
/// Расширения для настройки среды
/// </summary>
public static class ServiceConfigurationExtensions
{
    public static IServiceCollection ConfigureForEnvironment(
        this IServiceCollection services, 
        IHostEnvironment environment, 
        IConfiguration configuration)
    {
        if (environment.IsDevelopment())
        {
            // Настройки для разработки
            services.Configure<DiagnosticsConfiguration>(options =>
            {
                options.EnableDetailedLogging = true;
                options.EnablePerformanceAlerts = true;
            });
        }
        else if (environment.IsProduction())
        {
            // Настройки для продакшена
            services.Configure<PerformanceConfiguration>(options =>
            {
                options.EnablePerformanceLogging = true;
                options.MaxMemoryUsageMB = 150; // Строже в продакшене
            });
        }

        return services;
    }
}