using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using AnalysisNorm.Data;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Services.Implementation;

namespace AnalysisNorm.Services;

/// <summary>
/// Конфигурация dependency injection container
/// Централизованная настройка всех сервисов приложения
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    /// Настраивает все сервисы приложения
    /// Эквивалент архитектуры Python модулей, но с DI container
    /// </summary>
    public static IServiceCollection ConfigureServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // === DATABASE CONFIGURATION ===
        services.AddDbContext<AnalysisNormDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=analysis_norm.db;Cache=Shared;";
            
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(300); // 5 минут для длительных операций
            });

            // Включаем детальное логирование в режиме разработки
            options.EnableSensitiveDataLogging(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development");
            options.EnableDetailedErrors(true);
        });

        // === CORE SERVICES (аналоги Python модулей) ===
        
        // HTML Processing (аналог html_route_processor.py + html_norm_processor.py)
        services.AddScoped<IHtmlRouteProcessorService, HtmlRouteProcessorService>();
        services.AddScoped<IHtmlNormProcessorService, HtmlNormProcessorService>();
        
        // Data Analysis (аналог analyzer.py + data_analyzer.py)
        services.AddScoped<IDataAnalysisService, DataAnalysisService>();
        services.AddScoped<INormInterpolationService, NormInterpolationService>();
        
        // Locomotive Management (аналог coefficients.py + filter.py)
        services.AddScoped<ILocomotiveCoefficientService, LocomotiveCoefficientService>();
        services.AddScoped<ILocomotiveFilterService, LocomotiveFilterService>();
        
        // Caching and Storage (аналог norm_storage.py с улучшениями)
        services.AddScoped<INormStorageService, NormStorageService>();
        services.AddScoped<IAnalysisCacheService, AnalysisCacheService>();
        
        // Export Services (аналог export функций)
        services.AddScoped<IExcelExportService, ExcelExportService>();
        
        // Visualization Data (подготовка данных для OxyPlot)
        services.AddScoped<IVisualizationDataService, VisualizationDataService>();

        // === CONFIGURATION ===
        services.Configure<ApplicationSettings>(configuration.GetSection("ApplicationSettings"));
        services.Configure<LoggingSettings>(configuration.GetSection("Logging"));

        // === UTILITIES ===
        services.AddSingleton<IFileEncodingDetector, FileEncodingDetector>();
        services.AddSingleton<ITextNormalizer, TextNormalizer>();

        return services;
    }

    /// <summary>
    /// Инициализирует базу данных и выполняет миграции
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AnalysisNormDbContext>();
        
        try 
        {
            // Создаем базу данных и применяем миграции
            await dbContext.Database.EnsureCreatedAsync();
            
            // Очищаем старый кэш (старше 30 дней)
            await dbContext.CleanupInterpolationCacheAsync(TimeSpan.FromDays(30));
            
            Log.Information("База данных инициализирована успешно");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Критическая ошибка инициализации базы данных");
            throw;
        }
    }

    /// <summary>
    /// Настраивает Serilog logging
    /// Соответствует logging.basicConfig из Python main.py
    /// </summary>
    public static IHostBuilder ConfigureLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
        {
            var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);

            var logFileName = Path.Combine(logsDirectory, 
                $"analyzer_{DateTime.Now:yyyyMMdd}.log");

            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "AnalysisNorm")
                .WriteTo.Console(outputTemplate: 
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(logFileName, 
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: 
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug();

            // В режиме разработки добавляем более детальное логирование
            if (context.HostingEnvironment.IsDevelopment())
            {
                configuration.MinimumLevel.Debug();
            }
        });
    }
}

/// <summary>
/// Настройки приложения (аналог APP_CONFIG из Python config.py)
/// </summary>
public class ApplicationSettings
{
    public string DataDirectory { get; set; } = "data";
    public string TempDirectory { get; set; } = "temp";
    public string ExportsDirectory { get; set; } = "exports";
    public string LogsDirectory { get; set; } = "logs";
    
    // Настройки обработки (из Python config.py)
    public string[] SupportedEncodings { get; set; } = ["cp1251", "utf-8", "utf-8-sig"];
    public int MaxTempFiles { get; set; } = 10;
    public double DefaultTolerancePercent { get; set; } = 5.0;
    public double MinWorkThreshold { get; set; } = 200.0;
    
    // UI настройки
    public int DefaultWindowWidth { get; set; } = 1400;
    public int DefaultWindowHeight { get; set; } = 900;
    
    /// <summary>
    /// Создает необходимые директории
    /// </summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(ExportsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}

/// <summary>
/// Настройки логирования
/// </summary>
public class LoggingSettings
{
    public string MinimumLevel { get; set; } = "Information";
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;
    public bool EnableDetailedErrors { get; set; } = true;
    public int RetainedFileCountLimit { get; set; } = 30;
}