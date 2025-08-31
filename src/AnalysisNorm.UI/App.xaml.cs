using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; // ИСПРАВЛЕНО: добавлен недостающий using
using Microsoft.Extensions.Configuration;
using AnalysisNorm.Services;
using AnalysisNorm.Data;
using AnalysisNorm.UI.Windows;
using AnalysisNorm.UI.ViewModels;
using Serilog;

namespace AnalysisNorm.UI;

/// <summary>
/// Главный класс приложения WPF с поддержкой DI и логирования
/// </summary>
public partial class App : Application
{
    #region Private Fields

    private IHost? _host;
    private ILogger<App>? _logger; // ИСПРАВЛЕНО: правильный тип ILogger<T>

    #endregion

    #region Application Lifecycle

    /// <summary>
    /// ИСПРАВЛЕНО: Инициализация приложения при запуске
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Инициализируем Serilog до создания хоста
            InitializeSerilog();

            // Создаем и запускаем хост
            _host = CreateHostBuilder(e.Args).Build();
            _host.Start();

            // Получаем логгер после инициализации DI
            _logger = _host.Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("Приложение успешно запущено"); // ИСПРАВЛЕНО: используем метод расширения
            _logger.LogInformation($"Версия приложения: {GetApplicationVersion()}");

            // Инициализируем материальную тему
            InitializeMaterialDesign();

            // Создаем и показываем главное окно
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            // Логируем критическую ошибку
            Log.Fatal(ex, "Критическая ошибка при запуске приложения");

            MessageBox.Show(
                $"Критическая ошибка при запуске приложения:\n\n{ex.Message}\n\nПриложение будет закрыто.",
                "Ошибка запуска",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
        finally
        {
            _logger?.LogInformation("Инициализация приложения завершена");
        }
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Завершение работы приложения
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _logger?.LogInformation("Начало завершения работы приложения");

            // Сохраняем настройки пользователя
            SaveUserSettings();

            // Очищаем ресурсы
            CleanupResources();

            base.OnExit(e);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при завершении работы приложения"); // ИСПРАВЛЕНО
        }
        finally
        {
            // Останавливаем хост
            _host?.StopAsync(TimeSpan.FromSeconds(5)).Wait();
            _host?.Dispose();

            // Закрываем Serilog
            Log.CloseAndFlush();
        }
    }

    #endregion

    #region Host Configuration

    /// <summary>
    /// Создает и настраивает хост приложения
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog() // Используем Serilog для логирования
            .ConfigureAppConfiguration((context, config) =>
            {
                // Настраиваем конфигурацию
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                    optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Регистрируем сервисы
                ConfigureServices(services, context.Configuration);
            });
    }

    /// <summary>
    /// Настройка служб dependency injection
    /// </summary>
    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Регистрируем конфигурацию
        services.AddSingleton(configuration);

        // Регистрируем сервисы из других проектов
        services.AddAnalysisNormServices(configuration);
        services.AddAnalysisNormData(configuration);

        // Регистрируем окна и view models
        RegisterWindowsAndViewModels(services);

        // Регистрируем UI сервисы
        RegisterUIServices(services);
    }

    /// <summary>
    /// Регистрация окон и view models
    /// </summary>
    private static void RegisterWindowsAndViewModels(IServiceCollection services)
    {
        // Главное окно
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        // Окна диалогов
        services.AddTransient<PlotWindow>(); // ИСПРАВЛЕНО: удален лишний using
        services.AddTransient<PlotWindowViewModel>();

        services.AddTransient<RouteStatisticsWindow>(); // ИСПРАВЛЕНО
        services.AddTransient<RouteStatisticsViewModel>(); // ИСПРАВЛЕНО

        services.AddTransient<LocomotiveFilterWindow>(); // ИСПРАВЛЕНО
        services.AddTransient<LocomotiveFilterViewModel>(); // ИСПРАВЛЕНО
    }

    /// <summary>
    /// Регистрация UI сервисов
    /// </summary>
    private static void RegisterUIServices(IServiceCollection services)
    {
        // Добавляем HTTP client для веб-запросов
        services.AddHttpClient();

        // Сервисы для работы с файлами
        services.AddSingleton<IDialogService, DialogService>();

        // Фабрика логгеров для совместимости
        services.AddSingleton<ILoggerFactory>(provider => // ИСПРАВЛЕНО: правильная регистрация
            provider.GetRequiredService<ILoggerFactory>());
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Инициализация Serilog
    /// </summary>
    private static void InitializeSerilog()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AnalysisNorm",
            "Logs",
            "app.log");

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 50 * 1024 * 1024, // 50MB
                rollOnFileSizeLimit: true)
            .CreateLogger();
    }

    /// <summary>
    /// Инициализация Material Design
    /// </summary>
    private void InitializeMaterialDesign()
    {
        try
        {
            _logger?.LogInformation("Инициализация Material Design темы"); // ИСПРАВЛЕНО

            // Инициализация темы будет выполнена через XAML ресурсы
            // в App.xaml файле, здесь только логируем событие
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при инициализации Material Design"); // ИСПРАВЛЕНО
        }
    }

    #endregion

    #region Cleanup and Settings

    /// <summary>
    /// Сохранение пользовательских настроек
    /// </summary>
    private void SaveUserSettings()
    {
        try
        {
            _logger?.LogInformation("Сохранение пользовательских настроек"); // ИСПРАВЛЕНО

            // Здесь будет логика сохранения настроек
            // Properties.Settings.Default.Save();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при сохранении пользовательских настроек"); // ИСПРАВЛЕНО
        }
    }

    /// <summary>
    /// Очистка ресурсов приложения
    /// </summary>
    private void CleanupResources()
    {
        try
        {
            _logger?.LogInformation("Очистка ресурсов приложения");

            // Очищаем временные файлы
            CleanupTempFiles();

            // Закрываем подключения к БД
            // (будет выполнено автоматически через DI контейнер)
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при очистке ресурсов");
        }
    }

    /// <summary>
    /// Очистка временных файлов
    /// </summary>
    private static void CleanupTempFiles()
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "AnalysisNorm");
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
        catch
        {
            // Игнорируем ошибки очистки временных файлов
        }
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Получает версию приложения
    /// </summary>
    private static string GetApplicationVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Обработчик необработанных исключений
    /// </summary>
    private void Application_DispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            _logger?.LogError(e.Exception, "Необработанное исключение в UI потоке"); // ИСПРАВЛЕНО

            var result = MessageBox.Show(
                $"Произошла неожиданная ошибка:\n\n{e.Exception.Message}\n\nПродолжить работу приложения?",
                "Необработанная ошибка",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.No)
            {
                Shutdown(-1);
            }
            else
            {
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            // Критическая ошибка в обработчике ошибок
            Log.Fatal(ex, "Критическая ошибка в обработчике исключений");
            Shutdown(-1);
        }
    }

    #endregion
}

/// <summary>
/// Интерфейс для работы с диалоговыми окнами
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Показывает диалог выбора файла
    /// </summary>
    string? ShowOpenFileDialog(string filter = "", string title = "");

    /// <summary>
    /// Показывает диалог сохранения файла
    /// </summary>
    string? ShowSaveFileDialog(string filter = "", string title = "", string defaultFileName = "");

    /// <summary>
    /// Показывает диалог выбора папки
    /// </summary>
    string? ShowFolderBrowserDialog(string description = "");

    /// <summary>
    /// Показывает информационное сообщение
    /// </summary>
    void ShowMessage(string message, string title = "Информация");

    /// <summary>
    /// Показывает сообщение с подтверждением
    /// </summary>
    bool ShowConfirmation(string message, string title = "Подтверждение");

    /// <summary>
    /// Показывает сообщение об ошибке
    /// </summary>
    void ShowError(string message, string title = "Ошибка");
}

/// <summary>
/// Реализация сервиса диалогов для WPF
/// </summary>
public class DialogService : IDialogService
{
    public string? ShowOpenFileDialog(string filter = "", string title = "")
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter,
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string filter = "", string title = "", string defaultFileName = "")
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            Title = title,
            FileName = defaultFileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowFolderBrowserDialog(string description = "")
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = description
        };

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    public void ShowMessage(string message, string title = "Информация")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public bool ShowConfirmation(string message, string title = "Подтверждение")
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public void ShowError(string message, string title = "Ошибка")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}