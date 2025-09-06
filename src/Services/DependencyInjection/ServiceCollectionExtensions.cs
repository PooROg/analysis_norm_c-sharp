// Services/DependencyInjection/ServiceCollectionExtensions.cs - Основная регистрация сервисов
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Services.Implementation;
using AnalysisNorm.Infrastructure.Logging;
using AnalysisNorm.ViewModels;

namespace AnalysisNorm.Services.DependencyInjection;

/// <summary>
/// Основные расширения для регистрации сервисов Analysis Norm
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет все сервисы Analysis Norm в DI контейнер
    /// </summary>
    public static IServiceCollection AddAnalysisNormServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Основные сервисы
        services.AddCoreServices();
        
        // HTML парсинг
        services.AddHtmlParsingServices();
        
        // Excel экспорт 
        services.AddExcelExportServices();
        
        // Конфигурация
        services.AddConfigurationServices(configuration);
        
        // ViewModels
        services.AddViewModels();
        
        // Расширенные сервисы CHAT 3-4 (заглушки)
        services.AddAdvancedServices();

        return services;
    }

    /// <summary>
    /// Основные сервисы
    /// </summary>
    private static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Логирование
        services.AddSingleton<IApplicationLogger, SerilogApplicationLogger>();
        
        // Мониторинг производительности  
        services.AddSingleton<IPerformanceMonitor, AdvancedPerformanceMonitor>();
        
        // Хранилище норм
        services.AddSingleton<INormStorage, MemoryNormStorage>();

        return services;
    }

    /// <summary>
    /// Сервисы HTML парсинга
    /// </summary>
    private static IServiceCollection AddHtmlParsingServices(this IServiceCollection services)
    {
        // Базовый парсер
        services.AddTransient<IHtmlParser, StubHtmlParser>();
        
        // Интерактивный анализатор (заглушка)
        services.AddTransient<IInteractiveNormsAnalyzer, StubInteractiveNormsAnalyzer>();

        return services;
    }

    /// <summary>
    /// Сервисы Excel экспорта
    /// </summary>
    private static IServiceCollection AddExcelExportServices(this IServiceCollection services)
    {
        // Базовый экспортер
        services.AddTransient<IExcelExporter, StubExcelExporter>();

        return services;
    }

    /// <summary>
    /// Сервисы конфигурации
    /// </summary>
    private static IServiceCollection AddConfigurationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Базовый конфигурационный сервис
        services.AddSingleton<IConfigurationService, StubConfigurationService>();

        return services;
    }

    /// <summary>
    /// ViewModels
    /// </summary>
    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // Главная ViewModel
        services.AddTransient<EnhancedMainViewModel>();

        return services;
    }

    /// <summary>
    /// Расширенные сервисы CHAT 3-4 (заглушки)
    /// </summary>
    private static IServiceCollection AddAdvancedServices(this IServiceCollection services)
    {
        // Системная диагностика
        services.AddTransient<ISystemDiagnostics, StubSystemDiagnostics>();
        
        // Пользовательские настройки
        services.AddSingleton<IUserPreferencesService, StubUserPreferencesService>();

        return services;
    }
}

// =============================================================================
// ЗАГЛУШКИ ДЛЯ КОМПИЛЯЦИИ - будут заменены в полных реализациях
// =============================================================================

/// <summary>
/// Заглушка HTML парсера
/// </summary>
public class StubHtmlParser : IHtmlParser
{
    public Task<object> ParseRoutesAsync(Stream htmlContent, object? options = null)
    {
        return Task.FromResult<object>(new List<object>());
    }

    public Task<object> ParseNormsAsync(Stream htmlContent, object? options = null)
    {
        return Task.FromResult<object>(new List<object>());
    }
}

/// <summary>
/// Заглушка интерактивного анализатора
/// </summary>
public class StubInteractiveNormsAnalyzer : IInteractiveNormsAnalyzer
{
    public Task<bool> LoadRoutesFromHtmlAsync(List<string> filePaths)
    {
        return Task.FromResult(true);
    }

    public Task<bool> LoadNormsFromHtmlAsync(List<string> filePaths)
    {
        return Task.FromResult(true);
    }

    public Task<object> AnalyzeSectionAsync(string sectionName)
    {
        return Task.FromResult<object>(new { Section = sectionName, Status = "OK" });
    }
}

/// <summary>
/// Заглушка Excel экспортера
/// </summary>
public class StubExcelExporter : IExcelExporter
{
    public Task<bool> ExportRoutesAsync(IEnumerable<object> routes, string filePath)
    {
        return Task.FromResult(true);
    }

    public Task<bool> ExportAnalysisAsync(object analysisResult, string filePath)
    {
        return Task.FromResult(true);
    }
}

/// <summary>
/// Заглушка сервиса конфигурации
/// </summary>
public class StubConfigurationService : IConfigurationService
{
    public T GetConfiguration<T>() where T : class, new()
    {
        return new T();
    }

    public Task<bool> UpdateConfigurationAsync<T>(T newConfiguration) where T : class, new()
    {
        return Task.FromResult(true);
    }

    public void ResetToDefaults<T>() where T : class, new()
    {
        // Заглушка
    }

    public object GetDiagnostics()
    {
        return new { LoadedConfigurationsCount = 4 };
    }
}

/// <summary>
/// Заглушка системной диагностики
/// </summary>
public class StubSystemDiagnostics : ISystemDiagnostics
{
    public Task<object> QuickHealthCheckAsync()
    {
        return Task.FromResult<object>(new { IsHealthy = true, Status = "OK" });
    }

    public Task<object> RunFullDiagnosticsAsync()
    {
        return Task.FromResult<object>(new { Status = "All systems operational" });
    }
}

/// <summary>
/// Заглушка пользовательских настроек
/// </summary>
public class StubUserPreferencesService : IUserPreferencesService
{
    private readonly object _defaultPreferences = new
    {
        MainWindowPosition = new { X = 100, Y = 100, Width = 1600, Height = 1000, IsMaximized = false },
        Theme = "Light",
        Language = "ru-RU"
    };

    public Task<object> LoadUserPreferencesAsync()
    {
        return Task.FromResult(_defaultPreferences);
    }

    public Task<bool> SaveUserPreferencesAsync(object preferences)
    {
        return Task.FromResult(true);
    }
}