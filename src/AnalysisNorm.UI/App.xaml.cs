using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Text;
using System.Windows;
using AnalysisNorm.Services;
using AnalysisNorm.UI.ViewModels;
using AnalysisNorm.Data;
using Microsoft.EntityFrameworkCore;

namespace AnalysisNorm.UI;

/// <summary>
/// WPF Application entry point с полной Dependency Injection конфигурацией
/// Аналог Python main.py с современной архитектурой .NET и MVVM паттерном
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private ILogger? _logger;

    /// <summary>
    /// Инициализация приложения - аналог Python main() функции
    /// Настраивает DI контейнер, логирование и конфигурацию
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Настраиваем кодировку для корректной работы с русскими файлами - аналог Python encoding setup
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            // Создаем и запускаем хост с DI контейнером
            _host = CreateHostBuilder(e.Args).Build();
            await _host.StartAsync();

            // Получаем logger после инициализации DI
            _logger = _host.Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("=== АНАЛИЗАТОР НОРМ РАСХОДА ЭЛЕКТРОЭНЕРГИИ РЖД ===");
            _logger.LogInformation("Приложение запущено успешно");

            // Инициализируем базу данных
            await InitializeDatabaseAsync();

            // Создаем и отображаем главное окно
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            // Критическая ошибка при запуске
            var message = $"Критическая ошибка при запуске приложения:\n\n{ex.Message}\n\nДетали:\n{ex}";
            MessageBox.Show(message, "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            
            Current.Shutdown(1);
        }
    }

    /// <summary>
    /// Корректное завершение работы приложения - аналог Python cleanup
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            _logger?.LogInformation("Завершение работы приложения...");
            
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
            
            Log.CloseAndFlush();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при завершении: {ex}");
        }
        finally
        {
            base.OnExit(e);
        }
    }

    /// <summary>
    /// Создает и настраивает хост приложения с DI контейнером
    /// Аналог Python dependency setup, но с современной архитектурой .NET
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog((context, configuration) =>
            {
                // Конфигурация Serilog - аналог Python logging setup
                configuration
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        path: Path.Combine("logs", "analysis_norm-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                    .Enrich.FromLogContext();
            })
            .ConfigureAppConfiguration((context, config) =>
            {
                // Конфигурация приложения - аналог Python config.py
                config.SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                      .AddEnvironmentVariables()
                      .AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Регистрация всех сервисов - аналог Python modules import
                ConfigureServices(services, context.Configuration);
            })
            .UseConsoleLifetime();

    /// <summary>
    /// Настраивает DI контейнер со всеми сервисами
    /// Полный аналог Python import statements и объектов
    /// </summary>
    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // === БАЗОВЫЕ СЕРВИСЫ ===
        
        // Entity Framework и база данных - аналог Python database setup
        services.AddDbContext<AnalysisNormDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                                  ?? "Data Source=analysis_norm.db";
            options.UseSqlite(connectionString);
        });

        // Регистрация всех бизнес-сервисов из ServiceConfiguration
        services.AddAnalysisNormServices(configuration);

        // === WPF КОМПОНЕНТЫ ===
        
        // Регистрация основных окон и ViewModels
        services.AddTransient<MainWindow>();
        services.AddTransient<MainWindowViewModel>();
        
        // Регистрация окон графиков и аналитики
        services.AddTransient<PlotWindow>();
        services.AddTransient<PlotWindowViewModel>();
        services.AddTransient<RouteStatisticsWindow>();
        services.AddTransient<RouteStatisticsViewModel>();
        services.AddTransient<LocomotiveFilterWindow>();
        services.AddTransient<LocomotiveFilterViewModel>();
        
        // Регистрация UserControls
        services.AddTransient<Controls.FileSectionControl>();
        services.AddTransient<Controls.ControlSectionControl>();
        services.AddTransient<Controls.VisualizationSectionControl>();

        // === ДОПОЛНИТЕЛЬНЫЕ СЕРВИСЫ ===
        
        // LoggerFactory для создания логгеров в ViewModel'ах
        services.AddSingleton<ILoggerFactory>(provider => provider.GetService<ILoggerFactory>()!);
        
        // HTTP клиент для будущих веб-запросов (если понадобится)
        services.AddHttpClient();
        
        // Конфигурация как сервис
        services.AddSingleton(configuration);
        
        // Фабрики и хелперы
        services.AddSingleton<IServiceProvider>(provider => provider);
        
        // Логирование
        services.AddLogging();
    }

    /// <summary>
    /// Инициализирует базу данных при первом запуске
    /// Аналог Python database setup и migration
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        try
        {
            using var scope = _host!.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AnalysisNormDbContext>();
            
            _logger?.LogInformation("Инициализация базы данных...");
            
            // Создаем базу данных если её нет
            await dbContext.Database.EnsureCreatedAsync();
            
            // Выполняем миграции если есть
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger?.LogInformation("Применение миграций базы данных...");
                await dbContext.Database.MigrateAsync();
            }
            
            _logger?.LogInformation("База данных готова к работе");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка инициализации базы данных");
            throw new InvalidOperationException("Не удалось инициализировать базу данных", ex);
        }
    }

    /// <summary>
    /// Глобальный обработчик необработанных исключений
    /// Аналог Python global exception handler
    /// </summary>
    private void Application_DispatcherUnhandledException(object sender, 
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            _logger?.LogError(e.Exception, "Необработанное исключение в UI потоке");
            
            var message = $"Произошла неожиданная ошибка:\n\n{e.Exception.Message}\n\n" +
                         "Приложение может работать нестабильно. Рекомендуется перезапустить программу.";
            
            MessageBox.Show(message, "Неожиданная ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            
            // Помечаем исключение как обработанное, чтобы приложение не закрылось
            e.Handled = true;
        }
        catch
        {
            // Если даже обработчик ошибок дал сбой, просто завершаем приложение
            Current.Shutdown(1);
        }
    }
}