using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using AnalysisNorm.Data;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Services.Implementation;
using System.Text;

namespace AnalysisNorm.Services;

/// <summary>
/// Полная конфигурация dependency injection container с HTML Processing Engine
/// Централизованная настройка всех сервисов приложения включая новые HTML процессоры
/// 
/// ПРИМЕЧАНИЕ: ApplicationSettings теперь определен в Utils/utility_classes.cs
/// для устранения дублирования
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    /// Настраивает все сервисы приложения включая HTML Processing Engine
    /// Эквивалент архитектуры Python модулей, но с DI container
    /// ИСПРАВЛЕНО: Правильное название метода для соответствия App.xaml.cs
    /// </summary>
    public static IServiceCollection AddAnalysisNormServices(
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
        // ВАЖНО: Эти классы определены в Utils/utility_classes.cs
        services.AddSingleton<IFileEncodingDetector, FileEncodingDetector>();
        services.AddSingleton<ITextNormalizer, TextNormalizer>();

        // === CONFIGURATION BINDING ===
        // ВАЖНО: ApplicationSettings теперь импортируется из Utils/utility_classes.cs
        services.Configure<ApplicationSettings>(configuration.GetSection("ApplicationSettings"));
        services.Configure<LoggingSettings>(configuration.GetSection("Logging"));
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
        var context = scope.ServiceProvider.GetRequiredService<AnalysisNormDbContext>();

        Log.Information("Инициализация базы данных...");

        try
        {
            // Проверяем соединение с базой данных
            await context.Database.CanConnectAsync();

            // Применяем миграции если есть
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                Log.Information("Применение {MigrationCount} миграций", pendingMigrations.Count());
                await context.Database.MigrateAsync();
            }

            // Проверяем что все таблицы созданы
            await context.Database.EnsureCreatedAsync();

            Log.Information("База данных готова к работе");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка инициализации базы данных");
            throw;
        }
    }

    /// <summary>
    /// Проверяет здоровье всех компонентов системы
    /// Enterprise-уровень мониторинга с детальной диагностикой
    /// </summary>
    public static async Task<HealthCheckResult> PerformHealthCheckAsync(this IServiceProvider serviceProvider)
    {
        var result = new HealthCheckResult();

        try
        {
            using var scope = serviceProvider.CreateScope();

            // Проверка базы данных
            var dbContext = scope.ServiceProvider.GetRequiredService<AnalysisNormDbContext>();
            result.DatabaseHealthy = await dbContext.Database.CanConnectAsync();

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

#region Configuration Classes

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

#endregion

// ВАЖНО: ApplicationSettings удален отсюда и теперь определен 
// в Utils/utility_classes.cs для устранения дублирования кода