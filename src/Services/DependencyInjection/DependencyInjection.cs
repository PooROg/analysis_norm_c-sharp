// Services/DependencyInjection/ServiceCollectionExtensions.cs (CHAT 3-4)
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
/// CHAT 3-4: Конфигурация Dependency Injection с новыми сервисами
/// Включает: AdvancedHtmlParser, ExcelExportService, ConfigurationService, DuplicateResolver, SectionMerger
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// ГЛАВНАЯ функция: Регистрация всех сервисов для CHAT 3-4
    /// </summary>
    public static IServiceCollection AddAnalysisNormServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Core Infrastructure
        services.AddCoreInfrastructure(configuration);
        
        // CHAT 1-2: Existing Services
        services.AddFoundationServices();
        
        // CHAT 3: Advanced HTML Parsing
        services.AddAdvancedHtmlParsing();
        
        // CHAT 4: Excel Export & Configuration
        services.AddExcelExportServices();
        services.AddConfigurationServices(configuration);
        
        // ViewModels and UI
        services.AddViewModels();
        
        // Validation and Health Checks
        services.AddValidationServices();
        
        return services;
    }

    /// <summary>
    /// Базовая инфраструктура (логирование, кэширование, производительность)
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
            options.SizeLimit = 1000; // Лимит кэша
            options.CompactionPercentage = 0.25; // Процент очистки при превышении лимита
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
        });

        // Application Logger
        services.AddSingleton<IApplicationLogger, SerilogApplicationLogger>();

        // Performance Monitoring
        services.AddSingleton<IPerformanceMonitor, AdvancedPerformanceMonitor>();

        return services;
    }

    /// <summary>
    /// CHAT 1-2: Базовые сервисы (хранилище, математика)
    /// </summary>
    private static IServiceCollection AddFoundationServices(this IServiceCollection services)
    {
        // Storage Services
        services.AddSingleton<INormStorage, MemoryNormStorage>();
        
        // Mathematics Infrastructure
        services.AddSingleton<InterpolationEngine>();
        services.AddSingleton<StatusClassifier>();
        
        // Basic HTML Parser (for compatibility)
        services.AddTransient<IHtmlParser, EnhancedHtmlParser>();
        
        // Interactive Norms Analyzer
        services.AddSingleton<IInteractiveNormsAnalyzer, InteractiveNormsAnalyzer>();

        return services;
    }

    /// <summary>
    /// CHAT 3: Продвинутый HTML парсинг с дедупликацией и объединением
    /// </summary>
    private static IServiceCollection AddAdvancedHtmlParsing(this IServiceCollection services)
    {
        // Advanced HTML Parser Components
        services.AddTransient<DuplicateResolver>();
        services.AddTransient<SectionMerger>();
        services.AddTransient<AdvancedHtmlParser>();
        
        // Advanced HTML Parser Interface
        services.AddTransient<IAdvancedHtmlParser>(provider =>
            provider.GetRequiredService<AdvancedHtmlParser>());

        // HTML Validation Service
        services.AddTransient<IHtmlValidationService, HtmlValidationService>();

        // Batch Processing Service
        services.AddTransient<IBatchProcessingService, BatchProcessingService>();

        return services;
    }

    /// <summary>
    /// CHAT 4: Excel экспорт и управление файлами
    /// </summary>
    private static IServiceCollection AddExcelExportServices(this IServiceCollection services)
    {
        // Excel Export Service
        services.AddTransient<IExcelExporter, ExcelExportService>();
        
        // File Management
        services.AddTransient<IFileManagerService, FileManagerService>();
        
        // Report Generation
        services.AddTransient<IReportGeneratorService, ReportGeneratorService>();

        return services;
    }

    /// <summary>
    /// CHAT 4: Сервисы конфигурации
    /// </summary>
    private static IServiceCollection AddConfigurationServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configuration Service
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        
        // User Preferences
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
    /// ViewModels регистрация
    /// </summary>
    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // Main ViewModel - Updated for CHAT 3-4
        services.AddTransient<EnhancedMainViewModel>();
        
        // Dialog ViewModels
        services.AddTransient<ConfigurationDialogViewModel>();
        services.AddTransient<DiagnosticsDialogViewModel>();
        services.AddTransient<AboutDialogViewModel>();

        return services;
    }

    /// <summary>
    /// Сервисы валидации и проверки состояния
    /// </summary>
    private static IServiceCollection AddValidationServices(this IServiceCollection services)
    {
        // System Diagnostics
        services.AddSingleton<ISystemDiagnostics, SystemDiagnosticsService>();
        
        // Health Checks
        services.AddHealthChecks()
            .AddCheck<NormStorageHealthCheck>("norm-storage")
            .AddCheck<PerformanceHealthCheck>("performance")
            .AddCheck<ConfigurationHealthCheck>("configuration");

        return services;
    }

    /// <summary>
    /// ДОПОЛНИТЕЛЬНАЯ функция: Регистрация сервисов для тестирования
    /// </summary>
    public static IServiceCollection AddTestingServices(this IServiceCollection services)
    {
        // Mock Services для юнит-тестов
        services.AddTransient<IMockDataGenerator, MockDataGenerator>();
        services.AddTransient<ITestDataProvider, TestDataProvider>();
        
        return services;
    }

    /// <summary>
    /// Конфигурация для разных профилей (Development, Production, Testing)
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
        
        return services;
    }

    /// <summary>
    /// Сервисы для среды разработки
    /// </summary>
    private static IServiceCollection AddDevelopmentServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Debug и диагностические сервисы
        services.AddTransient<IDebugService, DebugService>();
        services.AddTransient<IPerformanceProfiler, PerformanceProfiler>();
        
        // Mock данные
        services.AddTestingServices();
        
        return services;
    }

    /// <summary>
    /// Сервисы для продакшн среды
    /// </summary>
    private static IServiceCollection AddProductionServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Оптимизированные сервисы для продакшна
        services.AddSingleton<IErrorReportingService, ErrorReportingService>();
        services.AddTransient<IPerformanceOptimizer, PerformanceOptimizer>();
        
        return services;
    }
}

/// <summary>
/// НОВЫЕ интерфейсы сервисов для CHAT 3-4
/// </summary>

public interface IHtmlValidationService
{
    Task<HtmlValidationResult> ValidateAsync(string htmlContent);
    Task<bool> IsValidRouteHtmlAsync(string htmlContent);
    Task<bool> IsValidNormHtmlAsync(string htmlContent);
}

public interface IBatchProcessingService
{
    Task<BatchParsingResult> ProcessMultipleFilesAsync(
        IEnumerable<string> filePaths, 
        ParsingOptions options);
    Task<ProcessingResult<string>> GenerateBatchReportAsync(BatchParsingResult result);
}

public interface IFileManagerService
{
    Task<List<string>> GetRecentFilesAsync();
    Task AddRecentFileAsync(string filePath);
    Task<bool> ValidateFilePathAsync(string filePath);
    Task<string> GenerateUniqueFileNameAsync(string basePath, string extension);
}

public interface IReportGeneratorService
{
    Task<string> GenerateProcessingReportAsync(ProcessingStatistics statistics);
    Task<string> GenerateDiagnosticReportAsync(SystemDiagnosticResult diagnostics);
    Task<string> GeneratePerformanceReportAsync(PerformanceMetrics metrics);
}

public interface IMockDataGenerator
{
    List<Route> GenerateMockRoutes(int count);
    List<Norm> GenerateMockNorms(int count);
    Tu3Data GenerateMockTu3Data();
    List<Yu7Record> GenerateMockYu7Records(int count);
}

public interface ITestDataProvider
{
    Task<string> GetSampleHtmlAsync(string sampleType);
    Task<List<Route>> GetTestRoutesAsync();
    Task<ProcessingResult<IEnumerable<Route>>> GetTestParsingResultAsync();
}

/// <summary>
/// Реализация основных новых сервисов
/// </summary>

public class HtmlValidationService : IHtmlValidationService
{
    private readonly IApplicationLogger _logger;

    public HtmlValidationService(IApplicationLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HtmlValidationResult> ValidateAsync(string htmlContent)
    {
        await Task.Yield();
        
        var errors = new List<string>();
        var warnings = new List<string>();
        
        // Базовая валидация HTML
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            errors.Add("HTML контент пуст");
            return new HtmlValidationResult { IsValid = false, Errors = errors };
        }

        // Проверка на наличие таблиц
        if (!htmlContent.Contains("<table", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("HTML не содержит таблиц");
        }

        // Проверка кодировки
        if (htmlContent.Contains("�"))
        {
            warnings.Add("Возможны проблемы с кодировкой");
        }

        var structureInfo = new HtmlStructureInfo
        {
            FileSizeBytes = System.Text.Encoding.UTF8.GetByteCount(htmlContent),
            TotalTables = CountOccurrences(htmlContent, "<table"),
            HasTu3Data = htmlContent.Contains("ТУ3", StringComparison.OrdinalIgnoreCase),
            HasYu7Data = htmlContent.Contains("НЕТТО", StringComparison.OrdinalIgnoreCase) && 
                        htmlContent.Contains("БРУТТО", StringComparison.OrdinalIgnoreCase)
        };

        return new HtmlValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors,
            Warnings = warnings,
            StructureInfo = structureInfo
        };
    }

    public async Task<bool> IsValidRouteHtmlAsync(string htmlContent)
    {
        var result = await ValidateAsync(htmlContent);
        return result.IsValid && htmlContent.Contains("Маршрут", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> IsValidNormHtmlAsync(string htmlContent)
    {
        var result = await ValidateAsync(htmlContent);
        return result.IsValid && htmlContent.Contains("норм", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        return System.Text.RegularExpressions.Regex.Matches(text, pattern, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
    }
}

public class BatchProcessingService : IBatchProcessingService
{
    private readonly AdvancedHtmlParser _htmlParser;
    private readonly IApplicationLogger _logger;
    private readonly IPerformanceMonitor _performanceMonitor;

    public BatchProcessingService(
        AdvancedHtmlParser htmlParser,
        IApplicationLogger logger,
        IPerformanceMonitor performanceMonitor)
    {
        _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
    }

    public async Task<BatchParsingResult> ProcessMultipleFilesAsync(
        IEnumerable<string> filePaths, 
        ParsingOptions options)
    {
        var filePathsList = filePaths.ToList();
        var allRoutes = new List<Route>();
        var fileResults = new List<FileProcessingResult>();
        var totalProcessingTime = TimeSpan.Zero;
        
        _performanceMonitor.StartOperation("BatchProcessing");
        _logger.LogInformation("Начата пакетная обработка {FileCount} файлов", filePathsList.Count);

        var successfullyProcessed = 0;
        var failedToProcess = 0;

        foreach (var filePath in filePathsList)
        {
            try
            {
                var fileStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var fileInfo = new FileInfo(filePath);
                
                var htmlContent = await File.ReadAllTextAsync(filePath);
                var parseResult = await _htmlParser.ParseRoutesFromHtmlAsync(htmlContent, filePath);
                
                fileStopwatch.Stop();
                totalProcessingTime = totalProcessingTime.Add(fileStopwatch.Elapsed);

                if (parseResult.IsSuccess && parseResult.Data != null)
                {
                    var routes = parseResult.Data.ToList();
                    allRoutes.AddRange(routes);
                    successfullyProcessed++;

                    fileResults.Add(new FileProcessingResult
                    {
                        FileName = Path.GetFileName(filePath),
                        IsSuccess = true,
                        RoutesExtracted = routes.Count,
                        ProcessingTime = fileStopwatch.Elapsed,
                        FileSizeBytes = fileInfo.Length
                    });
                }
                else
                {
                    failedToProcess++;
                    fileResults.Add(new FileProcessingResult
                    {
                        FileName = Path.GetFileName(filePath),
                        IsSuccess = false,
                        ErrorMessage = parseResult.ErrorMessage,
                        ProcessingTime = fileStopwatch.Elapsed,
                        FileSizeBytes = fileInfo.Length
                    });
                }
            }
            catch (Exception ex)
            {
                failedToProcess++;
                _logger.LogError(ex, "Ошибка обработки файла {FilePath}", filePath);
                
                fileResults.Add(new FileProcessingResult
                {
                    FileName = Path.GetFileName(filePath),
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = TimeSpan.Zero,
                    FileSizeBytes = 0
                });
            }
        }

        _performanceMonitor.EndOperation("BatchProcessing");

        var statistics = new BatchParsingStatistics
        {
            AverageProcessingTimePerFile = successfullyProcessed > 0 
                ? (decimal)totalProcessingTime.TotalMilliseconds / successfullyProcessed 
                : 0,
            AverageRoutesPerFile = successfullyProcessed > 0 
                ? (decimal)allRoutes.Count / successfullyProcessed 
                : 0,
            TotalDataProcessed = fileResults.Sum(r => r.FileSizeBytes),
            SuccessRate = filePathsList.Count > 0 
                ? (decimal)successfullyProcessed / filePathsList.Count * 100 
                : 0,
            FastestFile = fileResults.Where(r => r.IsSuccess)
                .OrderBy(r => r.ProcessingTime)
                .FirstOrDefault()?.FileName ?? "",
            SlowestFile = fileResults.Where(r => r.IsSuccess)
                .OrderByDescending(r => r.ProcessingTime)
                .FirstOrDefault()?.FileName ?? ""
        };

        _logger.LogInformation("Пакетная обработка завершена: {Success}/{Total} файлов успешно", 
            successfullyProcessed, filePathsList.Count);

        return new BatchParsingResult
        {
            TotalFilesProcessed = filePathsList.Count,
            SuccessfullyProcessed = successfullyProcessed,
            FailedToProcess = failedToProcess,
            AllRoutes = allRoutes,
            TotalProcessingTime = totalProcessingTime,
            FileResults = fileResults,
            Statistics = statistics
        };
    }

    public async Task<ProcessingResult<string>> GenerateBatchReportAsync(BatchParsingResult result)
    {
        await Task.Yield();

        try
        {
            var report = new StringBuilder();
            report.AppendLine("ОТЧЕТ О ПАКЕТНОЙ ОБРАБОТКЕ");
            report.AppendLine("=".PadRight(50, '='));
            report.AppendLine();
            
            report.AppendLine($"Дата обработки: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            report.AppendLine($"Всего файлов: {result.TotalFilesProcessed}");
            report.AppendLine($"Успешно обработано: {result.SuccessfullyProcessed}");
            report.AppendLine($"Ошибок обработки: {result.FailedToProcess}");
            report.AppendLine($"Общее время обработки: {result.TotalProcessingTime:hh\\:mm\\:ss}");
            report.AppendLine($"Извлечено маршрутов: {result.AllRoutes.Count}");
            report.AppendLine();

            report.AppendLine("СТАТИСТИКА:");
            report.AppendLine($"Среднее время на файл: {result.Statistics.AverageProcessingTimePerFile:F1} мс");
            report.AppendLine($"Среднее маршрутов на файл: {result.Statistics.AverageRoutesPerFile:F1}");
            report.AppendLine($"Процент успешности: {result.Statistics.SuccessRate:F1}%");
            report.AppendLine($"Общий объем данных: {result.Statistics.TotalDataProcessed / 1024 / 1024:F1} МБ");
            report.AppendLine();

            if (!string.IsNullOrEmpty(result.Statistics.FastestFile))
            {
                report.AppendLine($"Самый быстрый файл: {result.Statistics.FastestFile}");
            }
            if (!string.IsNullOrEmpty(result.Statistics.SlowestFile))
            {
                report.AppendLine($"Самый медленный файл: {result.Statistics.SlowestFile}");
            }

            report.AppendLine();
            report.AppendLine("ДЕТАЛИ ПО ФАЙЛАМ:");
            report.AppendLine("-".PadRight(50, '-'));

            foreach (var fileResult in result.FileResults)
            {
                var status = fileResult.IsSuccess ? "✓" : "✗";
                report.AppendLine($"{status} {fileResult.FileName}");
                if (fileResult.IsSuccess)
                {
                    report.AppendLine($"   Маршрутов: {fileResult.RoutesExtracted}, Время: {fileResult.ProcessingTime.TotalMilliseconds:F0} мс");
                }
                else
                {
                    report.AppendLine($"   Ошибка: {fileResult.ErrorMessage}");
                }
            }

            return ProcessingResult<string>.Success(report.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации отчета о пакетной обработке");
            return ProcessingResult<string>.Failure($"Ошибка генерации отчета: {ex.Message}");
        }
    }
}

// Health Check Services для валидации состояния системы

public class NormStorageHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly INormStorage _normStorage;

    public NormStorageHealthCheck(INormStorage normStorage)
    {
        _normStorage = normStorage ?? throw new ArgumentNullException(nameof(normStorage));
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var storageInfo = await _normStorage.GetStorageInfoAsync();
            
            if (storageInfo.MemoryUsageBytes > 100 * 1024 * 1024) // 100 MB
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                    $"Высокое использование памяти: {storageInfo.MemoryUsageBytes / 1024 / 1024} MB");
            }

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                $"Норм в хранилище: {storageInfo.TotalNorms}");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Ошибка проверки хранилища норм", ex);
        }
    }
}

public class PerformanceHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IPerformanceMonitor _performanceMonitor;

    public PerformanceHealthCheck(IPerformanceMonitor performanceMonitor)
    {
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        try
        {
            var memoryUsage = GC.GetTotalMemory(false) / 1024 / 1024; // MB
            
            if (memoryUsage > 200) // 200 MB
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                    $"Высокое использование памяти: {memoryUsage} MB");
            }

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                $"Использование памяти: {memoryUsage} MB");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Ошибка проверки производительности", ex);
        }
    }
}

public class ConfigurationHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IConfigurationService _configurationService;

    public ConfigurationHealthCheck(IConfigurationService configurationService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        try
        {
            var diagnostics = _configurationService.GetDiagnostics();
            
            if (diagnostics.LoadedConfigurationsCount < 4) // Ожидаем минимум 4 конфигурации
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                    $"Загружено только {diagnostics.LoadedConfigurationsCount} конфигураций");
            }

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                $"Конфигураций загружено: {diagnostics.LoadedConfigurationsCount}");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Ошибка проверки конфигурации", ex);
        }
    }
}
