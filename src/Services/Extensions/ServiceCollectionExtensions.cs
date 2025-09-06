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
    public static IServiceCollection AddEnhancedAnalysisServices(this IServiceCollection services)
    {
        // Главный анализатор - НОВЫЙ сервис
        services.AddSingleton<IInteractiveNormsAnalyzer, InteractiveNormsAnalyzer>();

        // Усиленный HTML парсер - заменяет заглушку если нужно
        services.AddTransient<EnhancedHtmlParser>();

        // НЕ регистрируем существующие сервисы чтобы избежать конфликтов:
        // - INormStorage уже зарегистрирован
        // - IPerformanceMonitor уже зарегистрирован  
        // - IApplicationLogger уже зарегистрирован
        // - ViewModels уже зарегистрированы

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