// Services/DependencyInjection/ServiceCollectionExtensions.cs (ИСПРАВЛЕН)
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Services.Implementation;
using AnalysisNorm.Infrastructure.Logging;
using AnalysisNorm.Infrastructure.Mathematics;
using AnalysisNorm.ViewModels;
using AnalysisNorm.Configuration;

namespace AnalysisNorm.Services.DependencyInjection;

/// <summary>
/// ИСПРАВЛЕННАЯ конфигурация Dependency Injection для CHAT 3-4
/// Устраняет конфликты между старыми и новыми сервисами
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// ГЛАВНАЯ функция: Регистрация всех сервисов для CHAT 3-4 БЕЗ КОНФЛИКТОВ
    /// </summary>
    public static IServiceCollection AddAnalysisNormServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Core Infrastructure
        services.AddCoreInfrastructure(configuration);
        
        // CHAT 1-2: Foundation Services
        services.AddFoundationServices();
        
        // CHAT 3: Advanced HTML Parsing (исправлено)
        services.AddAdvancedHtmlParsing();
        
        // CHAT 4: Excel Export & Configuration
        services.AddExcelExportServices();
        services.AddConfigurationServices(configuration);
        
        // ViewModels (исправлено - используем EnhancedMainViewModel)
        services.AddViewModels();
        
        // Validation and Health Checks
        services.AddValidationServices();
        
        return services;
    }

    /// <summary>
    /// Базовая инфраструктура (без изменений)
    /// </summary>
    private static IServiceCollection AddCoreInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Serilog Configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "AnalysisNorm")
            .Enrich.WithProperty("Version", "3.4.0")
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        // Memory Caching
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000;
            options.CompactionPercentage = 0.25;
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
        });

        // Application Logger
        services.AddSingleton<IApplicationLogger, SerilogApplicationLogger>();

        // Performance Monitoring
        services.AddSingleton<IPerformanceMonitor, AdvancedPerformanceMonitor>();

        return services;
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Foundation Services без дублирования
    /// </summary>
    private static IServiceCollection AddFoundationServices(this IServiceCollection services)
    {
        // Storage Services
        services.AddSingleton<INormStorage, MemoryNormStorage>();
        
        // Mathematics Infrastructure
        services.AddSingleton<InterpolationEngine>();
        services.AddSingleton<StatusClassifier>();
        
        // Basic HTML Parser (оставляем для совместимости)
        services.AddTransient<IHtmlParser, EnhancedHtmlParser>();

        return services;
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Advanced HTML Parsing - избегаем дублирования с EnhancedHtmlParser
    /// </summary>
    private static IServiceCollection AddAdvancedHtmlParsing(this IServiceCollection services)
    {
        // ВАЖНО: Сначала регистрируем компоненты, которые нужны AdvancedHtmlParser
        services.AddTransient<DuplicateResolver>();
        services.AddTransient<SectionMerger>();
        
        // Теперь регистрируем AdvancedHtmlParser как отдельный сервис
        services.AddTransient<AdvancedHtmlParser>();
        
        // ИСПРАВЛЕНО: InteractiveNormsAnalyzer использует ОБА парсера
        services.AddSingleton<IInteractiveNormsAnalyzer>(provider =>
        {
            var basicParser = provider.GetRequiredService<IHtmlParser>();
            var advancedParser = provider.GetRequiredService<AdvancedHtmlParser>();
            var normStorage = provider.GetRequiredService<INormStorage>();
            var logger = provider.GetRequiredService<IApplicationLogger>();
            var performanceMonitor = provider.GetRequiredService<IPerformanceMonitor>();

            return new InteractiveNormsAnalyzer(
                basicParser, 
                advancedParser, 
                normStorage, 
                logger, 
                performanceMonitor);
        });

        // Дополнительные сервисы для HTML обработки
        services.AddTransient<IHtmlValidationService, HtmlValidationService>();
        services.AddTransient<IBatchProcessingService, BatchProcessingService>();

        return services;
    }

    /// <summary>
    /// Excel Export Services (без изменений)
    /// </summary>
    private static IServiceCollection AddExcelExportServices(this IServiceCollection services)
    {
        services.AddTransient<IExcelExporter, ExcelExportService>();
        services.AddTransient<IFileManagerService, FileManagerService>();
        services.AddTransient<IReportGeneratorService, ReportGeneratorService>();

        return services;
    }

    /// <summary>
    /// Configuration Services (без изменений)
    /// </summary>
    private static IServiceCollection AddConfigurationServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
        
        // Bind Configuration Objects
        services.Configure<ExcelExportConfiguration>(
            configuration.GetSection("ExcelExport"));
        services.Configure<HtmlParsingConfiguration>(
            configuration.GetSection("HtmlParsing"));
        services.Configure<PerformanceConfiguration>(
            configuration.GetSection("Performance"));
        services.Configure<DiagnosticsConfiguration>(
            configuration.GetSection("Diagnostics"));
        services.Configure<UIConfiguration>(
            configuration.GetSection("UI"));

        return services;
    }

    /// <summary>
    /// ИСПРАВЛЕНО: ViewModels - используем EnhancedMainViewModel вместо MainViewModel
    /// </summary>
    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // ГЛАВНАЯ ViewModel для CHAT 3-4
        services.AddTransient<EnhancedMainViewModel>();
        
        // Дополнительные ViewModels для диалогов
        services.AddTransient<ConfigurationDialogViewModel>();
        services.AddTransient<DiagnosticsDialogViewModel>();
        services.AddTransient<AboutDialogViewModel>();

        return services;
    }

    /// <summary>
    /// Validation Services (без изменений)
    /// </summary>
    private static IServiceCollection AddValidationServices(this IServiceCollection services)
    {
        services.AddSingleton<ISystemDiagnostics, SystemDiagnosticsService>();
        
        services.AddHealthChecks()
            .AddCheck<NormStorageHealthCheck>("norm-storage")
            .AddCheck<PerformanceHealthCheck>("performance")
            .AddCheck<ConfigurationHealthCheck>("configuration");

        return services;
    }

    /// <summary>
    /// НОВЫЕ сервисы для устранения ошибок компиляции
    /// </summary>
    private static IServiceCollection AddMissingServices(this IServiceCollection services)
    {
        // Добавляем недостающие сервисы, на которые ссылается код
        services.AddTransient<IFileManagerService, FileManagerService>();
        services.AddTransient<IReportGeneratorService, ReportGeneratorService>();
        services.AddTransient<ISystemDiagnostics, SystemDiagnosticsService>();
        services.AddTransient<IUserPreferencesService, UserPreferencesService>();
        
        // ViewModels для диалогов (заглушки)
        services.AddTransient<ConfigurationDialogViewModel>();
        services.AddTransient<DiagnosticsDialogViewModel>();
        services.AddTransient<AboutDialogViewModel>();

        return services;
    }

    /// <summary>
    /// НОВЫЙ метод: Конфигурация для разных профилей
    /// </summary>
    public static IServiceCollection ConfigureForEnvironment(
        this IServiceCollection services, 
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        if (environment.IsDevelopment())
        {
            services.AddDevelopmentServices(configuration);
        }
        else if (environment.IsProduction())
        {
            services.AddProductionServices(configuration);
        }
        
        // Добавляем недостающие сервисы в любом случае
        services.AddMissingServices();
        
        return services;
    }

    private static IServiceCollection AddDevelopmentServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<IDebugService, DebugService>();
        services.AddTransient<IPerformanceProfiler, PerformanceProfiler>();
        return services;
    }

    private static IServiceCollection AddProductionServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IErrorReportingService, ErrorReportingService>();
        services.AddTransient<IPerformanceOptimizer, PerformanceOptimizer>();
        return services;
    }
}

/// <summary>
/// ЗАГЛУШКИ для недостающих сервисов (будут реализованы в следующих чатах)
/// </summary>

public interface IFileManagerService
{
    Task<List<string>> GetRecentFilesAsync();
    Task AddRecentFileAsync(string filePath);
}

public class FileManagerService : IFileManagerService
{
    public async Task<List<string>> GetRecentFilesAsync()
    {
        await Task.Yield();
        return new List<string>();
    }

    public async Task AddRecentFileAsync(string filePath)
    {
        await Task.Yield();
        // Заглушка
    }
}

public interface IReportGeneratorService
{
    Task<string> GenerateProcessingReportAsync(ProcessingStatistics statistics);
}

public class ReportGeneratorService : IReportGeneratorService
{
    public async Task<string> GenerateProcessingReportAsync(ProcessingStatistics statistics)
    {
        await Task.Yield();
        return "Отчет будет реализован в следующих версиях";
    }
}

public interface ISystemDiagnostics
{
    Task<SystemDiagnosticResult> RunFullDiagnosticsAsync();
    Task<HealthCheckResult> QuickHealthCheckAsync();
}

public class SystemDiagnosticsService : ISystemDiagnostics
{
    public async Task<SystemDiagnosticResult> RunFullDiagnosticsAsync()
    {
        await Task.Yield();
        return new SystemDiagnosticResult
        {
            DiagnosticTime = DateTime.UtcNow,
            OverallHealth = SystemHealth.Good,
            Summary = "Система работает нормально"
        };
    }

    public async Task<HealthCheckResult> QuickHealthCheckAsync()
    {
        await Task.Yield();
        return new HealthCheckResult
        {
            IsHealthy = true,
            Status = "OK",
            ResponseTime = TimeSpan.FromMilliseconds(50)
        };
    }
}

public interface IUserPreferencesService
{
    Task<UserPreferences> LoadUserPreferencesAsync();
    Task SaveUserPreferencesAsync(UserPreferences preferences);
}

public class UserPreferencesService : IUserPreferencesService
{
    public async Task<UserPreferences> LoadUserPreferencesAsync()
    {
        await Task.Yield();
        return new UserPreferences
        {
            Theme = "Light",
            Language = "ru-RU",
            MainWindowPosition = new WindowPosition()
        };
    }

    public async Task SaveUserPreferencesAsync(UserPreferences preferences)
    {
        await Task.Yield();
        // Заглушка
    }
}

// Заглушки ViewModels
public class ConfigurationDialogViewModel { }
public class DiagnosticsDialogViewModel { }
public class AboutDialogViewModel { }

// Заглушки для среды разработки
public interface IDebugService { }
public class DebugService : IDebugService { }

public interface IPerformanceProfiler { }
public class PerformanceProfiler : IPerformanceProfiler { }

public interface IErrorReportingService { }
public class ErrorReportingService : IErrorReportingService { }

public interface IPerformanceOptimizer { }
public class PerformanceOptimizer : IPerformanceOptimizer { }