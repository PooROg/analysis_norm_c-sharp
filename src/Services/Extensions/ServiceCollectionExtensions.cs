// Services/Extensions/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Services.Implementation;
using AnalysisNorm.Infrastructure.Caching;
using AnalysisNorm.Infrastructure.Logging;

namespace AnalysisNorm.Services.Extensions;

/// <summary>
/// Расширения для регистрации сервисов - совместимые с существующей архитектурой
/// Добавляет только новые сервисы без конфликтов с существующими
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует новые сервисы анализа норм - дополняет существующую регистрацию
    /// </summary>
    public static IServiceCollection AddAnalysisNormServices(this IServiceCollection services)
    {
        // Core Services (уже существующие)
        services.AddSingleton<IApplicationLogger, SerilogLogger>();
        services.AddSingleton<IPerformanceMonitor, SimplePerformanceMonitor>();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddSingleton<INormStorage, MemoryNormStorage>();

        // НОВОЕ для CHAT 2 - обновленные парсеры
        services.AddScoped<IHtmlParser, EnhancedHtmlParser>();
        services.AddScoped<IInteractiveNormsAnalyzer, InteractiveNormsAnalyzer>();

        // Mathematical Services
        services.AddTransient<InterpolationEngine>();
        services.AddTransient<StatusClassifier>();

        // UI Services  
        services.AddTransient<MainViewModel>();
        services.AddTransient<Views.MainWindow>();

        return services;
    }

    /// <summary>
    /// Заменяет заглушку HTML парсера на усиленную версию
    /// Вызывать ТОЛЬКО если нужно заменить существующую регистрацию
    /// </summary>
    public static IServiceCollection ReplaceHtmlParser(this IServiceCollection services)
    {
        // Удаляем существующую регистрацию IHtmlParser
        var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IHtmlParser));
        if (existingDescriptor != null)
        {
            services.Remove(existingDescriptor);
        }

        // Добавляем усиленную версию
        services.AddTransient<IHtmlParser, EnhancedHtmlParser>();

        return services;
    }

    /// <summary>
    /// Валидирует что все новые сервисы доступны
    /// </summary>
    public static void ValidateEnhancedServices(this IServiceProvider serviceProvider)
    {
        var requiredServices = new[]
        {
            typeof(IInteractiveNormsAnalyzer),
            typeof(EnhancedHtmlParser)
        };

        foreach (var serviceType in requiredServices)
        {
            var service = serviceProvider.GetService(serviceType);
            if (service == null)
            {
                throw new InvalidOperationException(
                    $"Новый сервис {serviceType.Name} не зарегистрирован в DI контейнере");
            }
        }
    }
}