// Services/DependencyInjection/AdvancedServicesExtensions.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Services.Implementation;
using AnalysisNorm.Configuration;

namespace AnalysisNorm.Services.DependencyInjection;

/// <summary>
/// Расширения DI для сервисов CHAT 3-4 без конфликтов с существующими
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
        // Расширенные сервисы (без конфликтов с базовыми)
        services.AddAdvancedExcelServices();
        services.AddAdvancedConfigurationServices(configuration);
        services.AddSystemDiagnosticsServices();
        services.AddUserPreferencesServices();
        
        // Конфигурационные модели
        services.AddConfigurationModels(configuration);
        
        return services;
    }

    /// <summary>
    /// Расширенные сервисы Excel экспорта
    /// </summary>
    private static IServiceCollection AddAdvancedExcelServices(this IServiceCollection services)
    {
        // Расширенный экспорт (дополняет базовый IExcelExporter)
        services.AddTransient<IAdvancedExcelExporter, AdvancedExcelExportService>();
        
        return services;
    }

    /// <summary>
    /// Расширенные сервисы конфигурации
    /// </summary>
    private static IServiceCollection AddAdvancedConfigurationServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Расширенная конфигурация (дополняет базовый IConfigurationService)
        services.AddSingleton<IAdvancedConfigurationService, AdvancedConfigurationService>();
        
        return services;
    }

    /// <summary>
    /// Сервисы системной диагностики
    /// </summary>
    private static IServiceCollection AddSystemDiagnosticsServices(this IServiceCollection services)
    {
        services.AddTransient<ISystemDiagnostics, SystemDiagnosticsService>();
        
        return services;
    }

    /// <summary>
    /// Сервисы пользовательских настроек
    /// </summary>
    private static IServiceCollection AddUserPreferencesServices(this IServiceCollection services)
    {
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
        
        return services;
    }

    /// <summary>
    /// Конфигурационные модели
    /// </summary>
    private static IServiceCollection AddConfigurationModels(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Регистрируем конфигурационные секции
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
        services.Configure<LoggingConfiguration>(
            configuration.GetSection("Logging"));

        return services;
    }

    /// <summary>
    /// Валидация регистрации сервисов (для диагностики)
    /// </summary>
    public static IServiceCollection ValidateAdvancedServices(this IServiceCollection services)
    {
        // Проверяем что все критические сервисы зарегистрированы
        var requiredServices = new[]
        {
            typeof(IAdvancedExcelExporter),
            typeof(IAdvancedConfigurationService),
            typeof(ISystemDiagnostics),
            typeof(IUserPreferencesService)
        };

        foreach (var serviceType in requiredServices)
        {
            var isRegistered = services.Any(s => s.ServiceType == serviceType);
            if (!isRegistered)
            {
                throw new InvalidOperationException(
                    $"Критический сервис {serviceType.Name} не зарегистрирован в DI контейнере");
            }
        }

        return services;
    }
}