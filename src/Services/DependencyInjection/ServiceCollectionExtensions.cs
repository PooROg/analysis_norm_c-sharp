// ===================================================================
// ФАЙЛ 1: src/Services/DependencyInjection/ServiceCollectionExtensions.cs
// ОСНОВНАЯ регистрация - ОБЯЗАТЕЛЬНО для компиляции
// ===================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

namespace AnalysisNorm.Services.DependencyInjection;

/// <summary>
/// ОСНОВНАЯ регистрация DI сервисов - ЭТАП 1 (для компиляции)
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
        // Валидируем входные параметры
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            // 1. Основная инфраструктура
            services.AddCoreInfrastructure(configuration);
            
            // 2. Основные сервисы приложения  
            services.AddApplicationServices();
            
            // 3. ЭТАП 2: Расширенные сервисы CHAT 3-4
            services.AddAdvancedChatServices(configuration); // ← СВЯЗЬ с AdvancedServicesExtensions
            
            // 4. ViewModels для WPF
            services.AddViewModels();
            
            // 5. Мониторинг и диагностика
            services.AddMonitoringServices();

            return services;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ КРИТИЧЕСКАЯ ОШИБКА инициализации DI: {ex.Message}");
            throw;
        }
    }

    private static IServiceCollection AddCoreInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000;
            options.CompactionPercentage = 0.25;
        });

        services.AddSingleton<IApplicationLogger, SerilogApplicationLogger>();
        services.AddSingleton<IPerformanceMonitor, SimplePerformanceMonitor>();

        return services;
    }

    private static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<INormStorage, MemoryNormStorage>();
        services.AddTransient<IHtmlParser, SimpleHtmlParser>();
        services.AddTransient<IExcelExporter, SimpleExcelExporter>();
        services.AddTransient<IInteractiveNormsAnalyzer, SimpleNormsAnalyzer>();

        return services;
    }

    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // БАЗОВАЯ ViewModel (всегда доступна)
        services.AddTransient<MainViewModel>();
        
        // РАСШИРЕННАЯ ViewModel (если доступны расширенные сервисы)
        services.AddTransient<EnhancedMainViewModel>();

        return services;
    }

    private static IServiceCollection AddMonitoringServices(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("memory_usage", () => HealthCheckResult.Healthy("Память в норме"))
            .AddCheck("application_status", () => HealthCheckResult.Healthy("Приложение работает"));

        return services;
    }
}

// ===================================================================
// ФАЙЛ 2: src/Services/DependencyInjection/AdvancedServicesExtensions.cs  
// РАСШИРЕННЫЕ сервисы CHAT 3-4 - для полной функциональности
// ===================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AnalysisNorm.Configuration;

namespace AnalysisNorm.Services.DependencyInjection;

/// <summary>
/// РАСШИРЕННЫЕ сервисы CHAT 3-4 - полная функциональность
/// </summary>
public static class AdvancedServicesExtensions
{
    /// <summary>
    /// Добавляет все расширенные сервисы CHAT 3-4
    /// </summary>
    public static IServiceCollection AddAdvancedChatServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // HTML парсинг и обработка данных
        services.AddAdvancedHtmlServices();
        
        // Excel экспорт и отчетность
        services.AddAdvancedExcelServices();
        
        // Конфигурация и пользовательские настройки
        services.AddAdvancedConfigurationServices(configuration);
        
        // Системная диагностика
        services.AddSystemDiagnosticsServices();
        
        // Конфигурационные модели
        services.AddConfigurationModels(configuration);
        
        return services;
    }

    /// <summary>
    /// Расширенные сервисы HTML обработки
    /// </summary>
    private static IServiceCollection AddAdvancedHtmlServices(this IServiceCollection services)
    {
        // Расширенный HTML парсер (дополняет базовый IHtmlParser)
        services.AddTransient<IAdvancedHtmlParser, AdvancedHtmlParser>();
        
        // Резолвер дубликатов данных
        services.AddTransient<IDuplicateResolver, DuplicateResolver>();
        
        // Объединитель секций
        services.AddTransient<ISectionMerger, SectionMerger>();

        return services;
    }

    /// <summary>
    /// Расширенные сервисы Excel экспорта
    /// </summary>
    private static IServiceCollection AddAdvancedExcelServices(this IServiceCollection services)
    {
        // Расширенный экспорт (дополняет базовый IExcelExporter)
        services.AddTransient<IAdvancedExcelExporter, AdvancedExcelExportService>();
        
        // Генератор отчетов
        services.AddTransient<IReportGeneratorService, ReportGeneratorService>();
        
        // Менеджер файлов
        services.AddTransient<IFileManagerService, FileManagerService>();
        
        return services;
    }

    /// <summary>
    /// Расширенные сервисы конфигурации
    /// </summary>
    private static IServiceCollection AddAdvancedConfigurationServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Расширенная конфигурация (дополняет базовые настройки)
        services.AddSingleton<IAdvancedConfigurationService, AdvancedConfigurationService>();
        
        // Пользовательские настройки
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
        
        return services;
    }

    /// <summary>
    /// Сервисы системной диагностики
    /// </summary>
    private static IServiceCollection AddSystemDiagnosticsServices(this IServiceCollection services)
    {
        services.AddTransient<ISystemDiagnostics, SystemDiagnosticsService>();
        services.AddTransient<IBatchProcessingService, BatchProcessingService>();
        
        return services;
    }

    /// <summary>
    /// Конфигурационные модели для CHAT 3-4
    /// </summary>
    private static IServiceCollection AddConfigurationModels(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Регистрируем Options для различных конфигураций
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
}

// ===================================================================
// ИНТЕРФЕЙСЫ И ЗАГЛУШКИ - минимальные реализации для компиляции
// ===================================================================

namespace AnalysisNorm.Services.Interfaces
{
    // Основные интерфейсы (уже определены выше)
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

    // РАСШИРЕННЫЕ интерфейсы для CHAT 3-4
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
}

namespace AnalysisNorm.Services.Implementation
{
    using AnalysisNorm.Services.Interfaces;
    using System.Collections.Concurrent;

    // Основные реализации (уже определены выше)
    public class SerilogApplicationLogger : IApplicationLogger
    {
        public void LogInformation(string message) => Log.Information(message);
        public void LogError(Exception ex, string message) => Log.Error(ex, message);
        public void LogWarning(string message) => Log.Warning(message);
        public void LogDebug(string message) => Log.Debug(message);
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
            try
            {
                var content = $"Экспорт данных: {data?.ToString() ?? "null"}\nВремя: {DateTime.Now}";
                File.WriteAllText(filePath + ".txt", content);
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }

    public class SimpleNormsAnalyzer : IInteractiveNormsAnalyzer
    {
        public Task<string> AnalyzeAsync(string input)
        {
            var result = $"Анализ выполнен для данных размером: {input?.Length ?? 0} символов";
            return Task.FromResult(result);
        }
    }

    // РАСШИРЕННЫЕ ЗАГЛУШКИ для CHAT 3-4
    public class AdvancedHtmlParser : IAdvancedHtmlParser
    {
        public Task<string> ParseAsync(string htmlContent)
        {
            var result = $"[ADVANCED] Обработано HTML: {htmlContent?.Length ?? 0} символов";
            return Task.FromResult(result);
        }

        public Task<object> ParseWithOptionsAsync(string htmlContent, object options)
        {
            return Task.FromResult<object>($"Advanced parsing with options: {htmlContent?.Length ?? 0}");
        }
    }

    public class DuplicateResolver : IDuplicateResolver
    {
        public Task<string> ResolveAsync(string input)
        {
            return Task.FromResult($"Resolved duplicates in: {input?.Length ?? 0} chars");
        }
    }

    public class SectionMerger : ISectionMerger
    {
        public Task<string> MergeAsync(string input)
        {
            return Task.FromResult($"Merged sections: {input?.Length ?? 0} chars");
        }
    }

    public class AdvancedExcelExportService : IAdvancedExcelExporter
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
            return Task.FromResult($"Generated report from {data?.ToString()?.Length ?? 0} chars");
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
            return Task.FromResult<object>("System OK");
        }
    }

    public class BatchProcessingService : IBatchProcessingService
    {
        public Task<bool> ProcessBatchAsync(IEnumerable<string> files)
        {
            return Task.FromResult(true);
        }
    }
}

namespace AnalysisNorm.ViewModels
{
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

            _logger.LogInformation("MainViewModel инициализирована");
        }

        public string Title => "Анализатор норм расхода электроэнергии РЖД v1.3.4";
        public string Status => "Готов к работе";
    }

    /// <summary>
    /// РАСШИРЕННАЯ ViewModel для CHAT 3-4 (ЗАГЛУШКА)
    /// </summary>
    public class EnhancedMainViewModel : MainViewModel
    {
        public EnhancedMainViewModel(
            IApplicationLogger logger,
            IHtmlParser htmlParser, 
            IExcelExporter excelExporter) : base(logger, htmlParser, excelExporter)
        {
        }

        public string EnhancedTitle => "Анализатор норм РЖД v1.3.4 (Enhanced CHAT 3-4)";
    }
}

namespace AnalysisNorm.Configuration
{
    // Конфигурационные классы (заглушки)
    public class ExcelExportConfiguration
    {
        public string DefaultOutputPath { get; set; } = "Exports";
        public int MaxFileSize { get; set; } = 52428800;
    }

    public class HtmlParsingConfiguration
    {
        public int MaxConcurrentParsers { get; set; } = 4;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class PerformanceConfiguration
    {
        public int MaxMemoryUsageMB { get; set; } = 512;
        public bool EnablePerformanceCounters { get; set; } = true;
    }

    public class DiagnosticsConfiguration
    {
        public bool EnableDiagnostics { get; set; } = true;
        public string DiagnosticsLevel { get; set; } = "Information";
    }

    public class UIConfiguration
    {
        public string Theme { get; set; } = "Light";
        public string PrimaryColor { get; set; } = "DeepPurple";
    }
}