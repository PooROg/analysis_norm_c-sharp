using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using AnalysisNorm.Data;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Services.Implementation;
using System.Text;

namespace AnalysisNorm.Services;

/// <summary>
/// Полная конфигурация dependency injection container с HTML Processing Engine
/// Централизованная настройка всех сервисов приложения включая новые HTML процессоры
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    /// Настраивает все сервисы приложения включая HTML Processing Engine
    /// Эквивалент архитектуры Python модулей, но с DI container
    /// </summary>
    public static IServiceCollection ConfigureServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Регистрируем кодировки для cp1251 поддержки (как в Python read_text)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // === DATABASE CONFIGURATION ===
        services.AddDbContext<AnalysisNormDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=analysis_norm.db;Cache=Shared;Journal Mode=WAL;Synchronous=NORMAL;Foreign Keys=true;";
            
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(300); // 5 минут для длительных операций
            });

            // Включаем детальное логирование в режиме разработки
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            options.EnableSensitiveDataLogging(environment == "Development");
            options.EnableDetailedErrors(true);
        });

        // === HTML PROCESSING ENGINE (новые компоненты) ===
        
        // HTML Processing Services (точные аналоги Python HTMLRouteProcessor + HTMLNormProcessor)
        services.AddScoped<IHtmlRouteProcessorService, HtmlRouteProcessorService>();
        services.AddScoped<IHtmlNormProcessorService, HtmlNormProcessorService>();
        
        // === CORE ANALYSIS SERVICES (аналоги Python модулей) ===
        
        // Data Analysis (аналог analyzer.py + data_analyzer.py)
        services.AddScoped<IDataAnalysisService, DataAnalysisService>();
        services.AddScoped<INormInterpolationService, NormInterpolationService>();
        
        // Locomotive Management (аналог coefficients.py + filter.py)
        services.AddScoped<ILocomotiveCoefficientService, LocomotiveCoefficientService>();
        services.AddScoped<ILocomotiveFilterService, LocomotiveFilterService>();
        
        // Caching and Storage (аналог norm_storage.py с улучшениями)
        services.AddScoped<INormStorageService, NormStorageService>();
        services.AddScoped<IAnalysisCacheService, AnalysisCacheService>();
        
        // Export Services (аналог export функций с улучшениями)
        services.AddScoped<IExcelExportService, ExcelExportService>();
        
        // Visualization Data (подготовка данных для OxyPlot аналог visualization.py)
        services.AddScoped<IVisualizationDataService, VisualizationDataService>();

        // === UTILITY SERVICES (точные аналоги Python utils) ===
        
        // File Processing Utilities (аналог read_text из Python)
        services.AddSingleton<IFileEncodingDetector, FileEncodingDetector>();
        services.AddSingleton<ITextNormalizer, TextNormalizer>();

        // === CONFIGURATION BINDING ===
        services.Configure<ApplicationSettings>(configuration.GetSection("ApplicationSettings"));
        services.Configure<LoggingSettings>(configuration.GetSection("Logging"));

        // === PERFORMANCE CONFIGURATION ===
        services.Configure<PerformanceSettings>(configuration.GetSection("Performance"));

        // === HTTP CLIENT для будущих веб-интеграций ===
        services.AddHttpClient("AnalysisNormClient", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Add("User-Agent", "AnalysisNorm/2.0");
        });

        return services;
    }

    /// <summary>
    /// Инициализирует базу данных и выполняет миграции
    /// Расширенная версия с проверкой HTML Processing Engine
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AnalysisNormDbContext>();
        
        try 
        {
            Log.Information("Начинаем инициализацию базы данных...");
            
            // Создаем базу данных и применяем миграции
            await dbContext.Database.EnsureCreatedAsync();
            
            // Очищаем старый кэш (старше 30 дней)
            await dbContext.CleanupInterpolationCacheAsync(TimeSpan.FromDays(30));
            
            // Проверяем работоспособность HTML Processing сервисов
            await ValidateHtmlProcessingServicesAsync(scope.ServiceProvider);
            
            Log.Information("База данных инициализирована успешно");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Критическая ошибка инициализации базы данных");
            throw;
        }
    }

    /// <summary>
    /// Проверяет работоспособность HTML Processing сервисов
    /// </summary>
    private static async Task ValidateHtmlProcessingServicesAsync(IServiceProvider serviceProvider)
    {
        try
        {
            Log.Debug("Проверяем HTML Processing Engine...");
            
            // Проверяем регистрацию HTML процессоров
            var routeProcessor = serviceProvider.GetService<IHtmlRouteProcessorService>();
            var normProcessor = serviceProvider.GetService<IHtmlNormProcessorService>();
            
            if (routeProcessor == null)
                throw new InvalidOperationException("HTML Route Processor не зарегистрирован");
            
            if (normProcessor == null)
                throw new InvalidOperationException("HTML Norm Processor не зарегистрирован");

            // Проверяем utility сервисы
            var encodingDetector = serviceProvider.GetService<IFileEncodingDetector>();
            var textNormalizer = serviceProvider.GetService<ITextNormalizer>();
            
            if (encodingDetector == null)
                throw new InvalidOperationException("File Encoding Detector не зарегистрирован");
            
            if (textNormalizer == null)
                throw new InvalidOperationException("Text Normalizer не зарегистрирован");

            // Тестируем базовую функциональность
            var testText = "  Тест\xa0нормализации  ";
            var normalizedText = textNormalizer.NormalizeText(testText);
            
            if (normalizedText != "Тест нормализации")
                throw new InvalidOperationException("Text Normalizer работает некорректно");

            Log.Debug("HTML Processing Engine проверен успешно");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка проверки HTML Processing Engine");
            throw;
        }
    }

    /// <summary>
    /// Настраивает Serilog logging с поддержкой HTML Processing Engine
    /// Расширенная версия с дополнительными категориями логирования
    /// </summary>
    public static void ConfigureLogging(this IHostBuilder builder, IConfiguration configuration)
    {
        builder.UseSerilog((context, loggerConfig) =>
        {
            var loggingSettings = configuration.GetSection("Logging").Get<LoggingSettings>() 
                ?? new LoggingSettings();

            loggerConfig
                .MinimumLevel.Is(Enum.Parse<Serilog.Events.LogEventLevel>(loggingSettings.MinimumLevel))
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "AnalysisNorm")
                .Enrich.WithProperty("Version", "2.0.0")
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentUserName();

            // Console logging для разработки
            if (loggingSettings.EnableConsoleLogging)
            {
                loggerConfig.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
            }

            // File logging для production
            if (loggingSettings.EnableFileLogging)
            {
                var logsDirectory = configuration.GetSection("ApplicationSettings:LogsDirectory").Value ?? "logs";
                Directory.CreateDirectory(logsDirectory);
                
                loggerConfig.WriteTo.File(
                    path: Path.Combine(logsDirectory, "analysis-norm-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: loggingSettings.RetainedFileCountLimit,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");

                // Отдельный файл для HTML Processing Engine
                loggerConfig.WriteTo.Logger(l => l
                    .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("SourceContext") && 
                        e.Properties["SourceContext"].ToString().Contains("Html"))
                    .WriteTo.File(
                        path: Path.Combine(logsDirectory, "html-processing-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7));
            }

            // Debug logging для разработки
            if (context.HostingEnvironment.IsDevelopment() || loggingSettings.EnableDetailedErrors)
            {
                loggerConfig.WriteTo.Debug();
                loggerConfig.MinimumLevel.Debug();
            }
        });
    }

    /// <summary>
    /// Выполняет проверку работоспособности всех сервисов
    /// </summary>
    public static async Task<HealthCheckResult> PerformHealthCheckAsync(this IServiceProvider serviceProvider)
    {
        var result = new HealthCheckResult();
        
        try
        {
            using var scope = serviceProvider.CreateScope();
            
            // Проверка базы данных
            var dbContext = scope.ServiceProvider.GetRequiredService<AnalysisNormDbContext>();
            var dbStats = await dbContext.GetDatabaseStatisticsAsync();
            result.DatabaseHealthy = true;
            result.Details["DatabaseStats"] = dbStats;

            // Проверка HTML Processing сервисов
            var routeProcessor = scope.ServiceProvider.GetRequiredService<IHtmlRouteProcessorService>();
            var normProcessor = scope.ServiceProvider.GetRequiredService<IHtmlNormProcessorService>();
            result.HtmlProcessingHealthy = true;

            // Проверка кэширования
            var cacheService = scope.ServiceProvider.GetRequiredService<IAnalysisCacheService>();
            result.CacheHealthy = true;

            // Проверка utility сервисов
            var textNormalizer = scope.ServiceProvider.GetRequiredService<ITextNormalizer>();
            var testResult = textNormalizer.NormalizeText("  тест  ");
            result.UtilitiesHealthy = testResult == "тест";

            result.OverallHealthy = result.DatabaseHealthy && result.HtmlProcessingHealthy && 
                                   result.CacheHealthy && result.UtilitiesHealthy;
            
            Log.Information("Health check completed: {OverallHealth}", result.OverallHealthy ? "Healthy" : "Unhealthy");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Health check failed");
            result.OverallHealthy = false;
            result.Details["Error"] = ex.Message;
        }

        return result;
    }
}

/// <summary>
/// Настройки приложения расширенные для HTML Processing Engine
/// </summary>
public class ApplicationSettings
{
    public string DataDirectory { get; set; } = "data";
    public string TempDirectory { get; set; } = "temp";
    public string ExportsDirectory { get; set; } = "exports";
    public string LogsDirectory { get; set; } = "logs";
    
    // Настройки обработки HTML (аналог Python config.py)
    public string[] SupportedEncodings { get; set; } = new[] { "cp1251", "utf-8", "utf-8-sig" };
    public int MaxTempFiles { get; set; } = 10;
    public double DefaultTolerancePercent { get; set; } = 5.0;
    public double MinWorkThreshold { get; set; } = 200.0;
    
    // HTML Processing настройки (новые)
    public int HtmlProcessingTimeoutSeconds { get; set; } = 30;
    public int MaxConcurrentFiles { get; set; } = 4;
    public bool EnableHtmlValidation { get; set; } = true;
    public bool PreserveOriginalHtml { get; set; } = false;
    
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
        
        Log.Debug("Созданы директории: {DataDir}, {TempDir}, {ExportsDir}, {LogsDir}",
            DataDirectory, TempDirectory, ExportsDirectory, LogsDirectory);
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

/// <summary>
/// Настройки производительности
/// </summary>
public class PerformanceSettings
{
    public bool EnableInterpolationCaching { get; set; } = true;
    public int CacheExpirationDays { get; set; } = 30;
    public int MaxConcurrentProcessing { get; set; } = 4;
    public int DatabasePoolSize { get; set; } = 10;
    public bool EnableAsyncProcessing { get; set; } = true;
    public int MaxMemoryUsageMB { get; set; } = 2048;
}

/// <summary>
/// Результат проверки здоровья системы
/// </summary>
public class HealthCheckResult
{
    public bool OverallHealthy { get; set; }
    public bool DatabaseHealthy { get; set; }
    public bool HtmlProcessingHealthy { get; set; }
    public bool CacheHealthy { get; set; }
    public bool UtilitiesHealthy { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
    public DateTime CheckTime { get; set; } = DateTime.UtcNow;
}