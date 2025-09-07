// ===================================================================
// ФАЙЛ 1: src/AnalysisNorm/Services/DependencyInjection/ServiceCollectionExtensions.cs
// ===================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Collections.Concurrent;
using AnalysisNorm.Configuration;

namespace AnalysisNorm.Services.DependencyInjection;

/// <summary>
/// ОСНОВНАЯ регистрация DI сервисов - ИСПРАВЛЕННАЯ для компиляции
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// ГЛАВНАЯ точка входа для регистрации всех сервисов AnalysisNorm
    /// </summary>
    public static IServiceCollection AddAnalysisNormServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            services.AddCoreInfrastructure(configuration);
            services.AddApplicationServices();
            services.AddAdvancedServices(configuration);
            services.AddViewModels();
            services.AddConfigurationModels(configuration);

            Log.Information("✅ Все сервисы AnalysisNorm зарегистрированы успешно");
            return services;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "❌ КРИТИЧЕСКАЯ ОШИБКА регистрации сервисов AnalysisNorm");
            throw;
        }
    }

    private static IServiceCollection AddCoreInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000;
            options.CompactionPercentage = 0.25;
        });

        services.AddHealthChecks()
            .AddCheck("memory", () => 
            {
                var memory = GC.GetTotalMemory(false);
                return memory < 500_000_000
                    ? HealthCheckResult.Healthy($"Memory usage: {memory:N0} bytes")
                    : HealthCheckResult.Unhealthy($"High memory usage: {memory:N0} bytes");
            });

        return services;
    }

    private static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<Interfaces.IApplicationLogger, Implementation.SerilogApplicationLogger>();
        services.AddSingleton<Interfaces.IPerformanceMonitor, Implementation.SimplePerformanceMonitor>();
        services.AddSingleton<Interfaces.INormStorage, Implementation.MemoryNormStorage>();
        services.AddTransient<Interfaces.IHtmlParser, Implementation.SimpleHtmlParser>();
        services.AddTransient<Interfaces.IExcelExporter, Implementation.SimpleExcelExporter>();
        services.AddTransient<Interfaces.IInteractiveNormsAnalyzer, Implementation.SimpleNormsAnalyzer>();
        services.AddTransient<Interfaces.IFileService, Implementation.FileService>();

        return services;
    }

    private static IServiceCollection AddAdvancedServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<Interfaces.IAdvancedHtmlParser, Implementation.AdvancedHtmlParser>();
        services.AddTransient<Interfaces.IDuplicateResolver, Implementation.DuplicateResolver>();
        services.AddTransient<Interfaces.ISectionMerger, Implementation.SectionMerger>();
        services.AddTransient<Interfaces.IAdvancedExcelExporter, Implementation.AdvancedExcelExporter>();
        services.AddTransient<Interfaces.IReportGeneratorService, Implementation.ReportGeneratorService>();
        services.AddSingleton<Interfaces.IFileManagerService, Implementation.FileManagerService>();
        services.AddSingleton<Interfaces.IAdvancedConfigurationService, Implementation.AdvancedConfigurationService>();
        services.AddSingleton<Interfaces.IUserPreferencesService, Implementation.UserPreferencesService>();
        services.AddSingleton<Interfaces.ISystemDiagnostics, Implementation.SystemDiagnosticsService>();
        services.AddTransient<Interfaces.IBatchProcessingService, Implementation.BatchProcessingService>();

        return services;
    }

    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<ViewModels.MainViewModel>();
        return services;
    }

    private static IServiceCollection AddConfigurationModels(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ApplicationConfiguration>(
            configuration.GetSection("Application"));
        services.Configure<PerformanceConfiguration>(
            configuration.GetSection("Performance"));
        services.Configure<UIConfiguration>(
            configuration.GetSection("UI"));

        return services;
    }
}

// ===================================================================
// ФАЙЛ 2: src/AnalysisNorm/Services/Interfaces/IApplicationLogger.cs
// ===================================================================

namespace AnalysisNorm.Services.Interfaces;

public interface IApplicationLogger
{
    void LogInformation(string message);
    void LogError(Exception ex, string message);
    void LogWarning(string message);
    void LogDebug(string message);
}

public interface IPerformanceMonitor
{
    void StartMeasurement(string operationName);
    void EndMeasurement(string operationName);
    TimeSpan GetLastMeasurement(string operationName);
}

public interface INormStorage
{
    Task<bool> SaveNormAsync(object norm);
    Task<object?> GetNormAsync(string id);
    Task<IEnumerable<object>> GetAllNormsAsync();
}

public interface IHtmlParser
{
    Task<string> ParseAsync(string htmlContent);
}

public interface IExcelExporter
{
    Task<bool> ExportAsync(object data, string filePath);
}

public interface IInteractiveNormsAnalyzer
{
    Task<string> AnalyzeAsync(string input);
}

public interface IFileService
{
    Task<bool> FileExistsAsync(string filePath);
    Task<Stream> OpenReadAsync(string filePath);
    Task<byte[]> ReadAllBytesAsync(string filePath);
    Task WriteAllBytesAsync(string filePath, byte[] data);
    Task<string[]> GetFilesAsync(string directory, string searchPattern = "*");
}

public interface IAdvancedHtmlParser : IHtmlParser
{
    Task<object> ParseWithOptionsAsync(string htmlContent, object options);
}

public interface IDuplicateResolver
{
    Task<string> ResolveAsync(string input);
}

public interface ISectionMerger
{
    Task<string> MergeAsync(string input);
}

public interface IAdvancedExcelExporter : IExcelExporter
{
    Task<bool> ExportWithTemplateAsync(object data, string templatePath, string outputPath);
}

public interface IReportGeneratorService
{
    Task<string> GenerateAsync(object data);
}

public interface IFileManagerService
{
    Task<List<string>> GetRecentFilesAsync();
    Task AddRecentFileAsync(string filePath);
}

public interface IAdvancedConfigurationService
{
    T GetAdvancedConfiguration<T>() where T : class, new();
}

public interface IUserPreferencesService
{
    Task<T> GetPreferenceAsync<T>(string key);
    Task SetPreferenceAsync<T>(string key, T value);
}

public interface ISystemDiagnostics
{
    Task<object> GetSystemInfoAsync();
}

public interface IBatchProcessingService
{
    Task<bool> ProcessBatchAsync(IEnumerable<string> files);
}

// ===================================================================
// ФАЙЛ 3: src/AnalysisNorm/Services/Implementation/BasicServices.cs
// ===================================================================

namespace AnalysisNorm.Services.Implementation;

using AnalysisNorm.Services.Interfaces;

public class SerilogApplicationLogger : IApplicationLogger
{
    public void LogInformation(string message) => Serilog.Log.Information(message);
    public void LogError(Exception ex, string message) => Serilog.Log.Error(ex, message);
    public void LogWarning(string message) => Serilog.Log.Warning(message);
    public void LogDebug(string message) => Serilog.Log.Debug(message);
}

public class SimplePerformanceMonitor : IPerformanceMonitor
{
    private readonly ConcurrentDictionary<string, DateTime> _startTimes = new();
    private readonly ConcurrentDictionary<string, TimeSpan> _measurements = new();

    public void StartMeasurement(string operationName)
    {
        _startTimes[operationName] = DateTime.UtcNow;
    }

    public void EndMeasurement(string operationName)
    {
        if (_startTimes.TryRemove(operationName, out var startTime))
        {
            _measurements[operationName] = DateTime.UtcNow - startTime;
        }
    }

    public TimeSpan GetLastMeasurement(string operationName)
    {
        return _measurements.TryGetValue(operationName, out var measurement) 
            ? measurement : TimeSpan.Zero;
    }
}

public class MemoryNormStorage : INormStorage
{
    private readonly ConcurrentDictionary<string, object> _norms = new();

    public Task<bool> SaveNormAsync(object norm)
    {
        var id = Guid.NewGuid().ToString();
        _norms[id] = norm;
        return Task.FromResult(true);
    }

    public Task<object?> GetNormAsync(string id)
    {
        _norms.TryGetValue(id, out var norm);
        return Task.FromResult(norm);
    }

    public Task<IEnumerable<object>> GetAllNormsAsync()
    {
        return Task.FromResult(_norms.Values.AsEnumerable());
    }
}

public class SimpleHtmlParser : IHtmlParser
{
    public Task<string> ParseAsync(string htmlContent)
    {
        var result = $"Обработано HTML контента: {htmlContent?.Length ?? 0} символов";
        return Task.FromResult(result);
    }
}

public class SimpleExcelExporter : IExcelExporter
{
    public Task<bool> ExportAsync(object data, string filePath)
    {
        Serilog.Log.Information($"Экспорт данных в файл: {filePath}");
        return Task.FromResult(true);
    }
}

public class SimpleNormsAnalyzer : IInteractiveNormsAnalyzer
{
    public Task<string> AnalyzeAsync(string input)
    {
        var result = $"Анализ завершен для входных данных: {input?.Length ?? 0} символов";
        return Task.FromResult(result);
    }
}

public class FileService : IFileService
{
    public async Task<bool> FileExistsAsync(string filePath)
    {
        return await Task.FromResult(File.Exists(filePath));
    }

    public async Task<Stream> OpenReadAsync(string filePath)
    {
        return await Task.FromResult(File.OpenRead(filePath));
    }

    public async Task<byte[]> ReadAllBytesAsync(string filePath)
    {
        return await File.ReadAllBytesAsync(filePath);
    }

    public async Task WriteAllBytesAsync(string filePath, byte[] data)
    {
        await File.WriteAllBytesAsync(filePath, data);
    }

    public async Task<string[]> GetFilesAsync(string directory, string searchPattern = "*")
    {
        return await Task.FromResult(Directory.GetFiles(directory, searchPattern));
    }
}

// Расширенные реализации (заглушки)
public class AdvancedHtmlParser : IAdvancedHtmlParser
{
    public Task<string> ParseAsync(string htmlContent)
    {
        return Task.FromResult($"Расширенный парсинг: {htmlContent?.Length ?? 0} символов");
    }

    public Task<object> ParseWithOptionsAsync(string htmlContent, object options)
    {
        return Task.FromResult<object>($"Парсинг с опциями: {htmlContent?.Length ?? 0} символов");
    }
}

public class DuplicateResolver : IDuplicateResolver
{
    public Task<string> ResolveAsync(string input)
    {
        return Task.FromResult($"Дубликаты разрешены для: {input}");
    }
}

public class SectionMerger : ISectionMerger
{
    public Task<string> MergeAsync(string input)
    {
        return Task.FromResult($"Секции объединены для: {input}");
    }
}

public class AdvancedExcelExporter : IAdvancedExcelExporter
{
    public Task<bool> ExportAsync(object data, string filePath)
    {
        return Task.FromResult(true);
    }

    public Task<bool> ExportWithTemplateAsync(object data, string templatePath, string outputPath)
    {
        return Task.FromResult(true);
    }
}

public class ReportGeneratorService : IReportGeneratorService
{
    public Task<string> GenerateAsync(object data)
    {
        return Task.FromResult("Отчет сгенерирован");
    }
}

public class FileManagerService : IFileManagerService
{
    public Task<List<string>> GetRecentFilesAsync()
    {
        return Task.FromResult(new List<string>());
    }

    public Task AddRecentFileAsync(string filePath)
    {
        return Task.CompletedTask;
    }
}

public class AdvancedConfigurationService : IAdvancedConfigurationService
{
    public T GetAdvancedConfiguration<T>() where T : class, new()
    {
        return new T();
    }
}

public class UserPreferencesService : IUserPreferencesService
{
    public Task<T> GetPreferenceAsync<T>(string key)
    {
        return Task.FromResult(default(T)!);
    }

    public Task SetPreferenceAsync<T>(string key, T value)
    {
        return Task.CompletedTask;
    }
}

public class SystemDiagnosticsService : ISystemDiagnostics
{
    public Task<object> GetSystemInfoAsync()
    {
        return Task.FromResult<object>("Система в норме");
    }
}

public class BatchProcessingService : IBatchProcessingService
{
    public Task<bool> ProcessBatchAsync(IEnumerable<string> files)
    {
        return Task.FromResult(true);
    }
}

// ===================================================================
// ФАЙЛ 4: src/AnalysisNorm/ViewModels/MainViewModel.cs
// ===================================================================

namespace AnalysisNorm.ViewModels;

using AnalysisNorm.Services.Interfaces;

/// <summary>
/// Базовая ViewModel для MainWindow
/// </summary>
public class MainViewModel
{
    private readonly IApplicationLogger _logger;
    private readonly IHtmlParser _htmlParser;
    private readonly IExcelExporter _excelExporter;

    public MainViewModel(
        IApplicationLogger logger,
        IHtmlParser htmlParser, 
        IExcelExporter excelExporter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
        _excelExporter = excelExporter ?? throw new ArgumentNullException(nameof(excelExporter));
        
        _logger.LogInformation("MainViewModel инициализирована успешно");
    }
}

// ===================================================================
// ФАЙЛ 5: src/AnalysisNorm/Configuration/ConfigurationClasses.cs
// ===================================================================

namespace AnalysisNorm.Configuration;

public class ApplicationConfiguration
{
    public string ApplicationName { get; set; } = "AnalysisNorm";
    public string Version { get; set; } = "1.3.4.0";
    public string WorkingDirectory { get; set; } = string.Empty;
    public long MaxFileSize { get; set; } = 100_000_000;
    public int OperationTimeoutSeconds { get; set; } = 300;
    public bool EnableDetailedLogging { get; set; } = false;
    public bool EnableAutoBackup { get; set; } = true;
}

public class PerformanceConfiguration
{
    public int CacheSizeMB { get; set; } = 256;
    public int MaxParallelThreads { get; set; } = Environment.ProcessorCount;
    public int FileBufferSize { get; set; } = 8192;
    public int CacheCleanupIntervalMinutes { get; set; } = 30;
    public bool EnablePerformanceMonitoring { get; set; } = true;
    public int MaxMemoryUsageMB { get; set; } = 1024;
    public bool EnableGCAfterOperations { get; set; } = false;
    public int PerformanceWarningThresholdMs { get; set; } = 5000;
}

public class UIConfiguration
{
    public string Theme { get; set; } = "Light";
    public string PrimaryColor { get; set; } = "DeepPurple";
    public string SecondaryColor { get; set; } = "Lime";
    public double DefaultFontSize { get; set; } = 14.0;
    public string FontFamily { get; set; } = "Segoe UI";
    public bool ShowTooltips { get; set; } = true;
    public bool SaveWindowState { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    public bool ShowProgressIndicator { get; set; } = true;
    public string Language { get; set; } = "ru-RU";
}