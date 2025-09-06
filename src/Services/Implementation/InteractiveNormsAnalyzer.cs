// Services/Implementation/InteractiveNormsAnalyzer.cs
using System.Collections.Concurrent;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Models.Domain;
using AnalysisNorm.Infrastructure.Mathematics;
using AnalysisNorm.Infrastructure.Logging;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Главный анализатор норм - совместим с существующей архитектурой проекта
/// Использует только существующие типы и интерфейсы из analysis_norm_c-sharp
/// </summary>
public class InteractiveNormsAnalyzer : IInteractiveNormsAnalyzer, IDisposable
{
    private readonly IHtmlParser _htmlParser;
    private readonly INormStorage _normStorage;
    private readonly IApplicationLogger _logger;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly InterpolationEngine _interpolationEngine;

    // Основные данные
    private readonly List<Route> _loadedRoutes = new();
    private readonly ConcurrentDictionary<string, BasicAnalysisResult> _analyzedResults = new();
    private BasicProcessingStats _processingStats = new();

    public IReadOnlyList<Route> LoadedRoutes => _loadedRoutes.AsReadOnly();

    public InteractiveNormsAnalyzer(
        IHtmlParser htmlParser,
        INormStorage normStorage,
        IApplicationLogger logger,
        IPerformanceMonitor performanceMonitor)
    {
        _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
        _normStorage = normStorage ?? throw new ArgumentNullException(nameof(normStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        _interpolationEngine = new InterpolationEngine();

        _logger.LogInformation("Инициализирован анализатор норм");
    }

    /// <summary>
    /// Загружает маршруты из HTML файлов - использует существующий IHtmlParser
    /// </summary>
    public async Task<bool> LoadRoutesFromHtmlAsync(IEnumerable<string> htmlFiles)
    {
        using var operation = _performanceMonitor.StartOperation("Load_Routes_From_HTML");
        
        var fileList = htmlFiles.ToList();
        _logger.LogInformation("Загрузка маршрутов из {Count} HTML файлов", fileList.Count);

        try
        {
            _loadedRoutes.Clear();
            var totalProcessed = 0;
            var totalErrors = 0;

            foreach (var filePath in fileList)
            {
                try
                {
                    using var fileStream = File.OpenRead(filePath);
                    var parseResult = await _htmlParser.ParseRoutesAsync(fileStream);

                    if (parseResult.IsSuccess && parseResult.Data != null)
                    {
                        var routes = parseResult.Data.ToList();
                        _loadedRoutes.AddRange(routes);
                        totalProcessed += routes.Count;
                        _logger.LogDebug("Файл {File}: загружено {Count} маршрутов", Path.GetFileName(filePath), routes.Count);
                    }
                    else
                    {
                        totalErrors++;
                        _logger.LogWarning("Ошибка загрузки файла {File}: {Error}", 
                            Path.GetFileName(filePath), parseResult.ErrorMessage ?? "Неизвестная ошибка");
                    }
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    _logger.LogError(ex, "Критическая ошибка при обработке файла {File}", filePath);
                }
            }

            if (_loadedRoutes.Count == 0)
            {
                _logger.LogError("Не удалось загрузить ни одного маршрута из {Count} файлов", fileList.Count);
                return false;
            }

            _processingStats = new BasicProcessingStats
            {
                TotalRoutesLoaded = _loadedRoutes.Count,
                TotalErrors = totalErrors,
                ProcessingTime = TimeSpan.FromMilliseconds(Environment.TickCount),
                LastProcessingDate = DateTime.UtcNow
            };

            _logger.LogInformation("Загрузка завершена. Маршрутов: {Routes}, ошибок: {Errors}", 
                _loadedRoutes.Count, totalErrors);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при загрузке маршрутов");
            return false;
        }
    }

    /// <summary>
    /// Загружает нормы из HTML файлов - использует существующий INormStorage
    /// </summary>
    public async Task<bool> LoadNormsFromHtmlAsync(IEnumerable<string> htmlFiles)
    {
        using var operation = _performanceMonitor.StartOperation("Load_Norms_From_HTML");
        
        var fileList = htmlFiles.ToList();
        _logger.LogInformation("Загрузка норм из {Count} HTML файлов", fileList.Count);

        try
        {
            var totalNormsLoaded = 0;
            var totalErrors = 0;

            foreach (var filePath in fileList)
            {
                try
                {
                    using var fileStream = File.OpenRead(filePath);
                    var parseResult = await _htmlParser.ParseNormsAsync(fileStream);

                    if (parseResult.IsSuccess && parseResult.Data != null)
                    {
                        var norms = parseResult.Data.ToList();
                        
                        foreach (var norm in norms)
                        {
                            await _normStorage.SaveNormAsync(norm);
                        }
                        
                        totalNormsLoaded += norms.Count;
                        _logger.LogDebug("Файл {File}: загружено {Count} норм", Path.GetFileName(filePath), norms.Count);
                    }
                    else
                    {
                        totalErrors++;
                        _logger.LogWarning("Ошибка загрузки норм из файла {File}: {Error}", 
                            Path.GetFileName(filePath), parseResult.ErrorMessage ?? "Неизвестная ошибка");
                    }
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    _logger.LogError(ex, "Ошибка при обработке файла норм {File}", filePath);
                }
            }

            // Обновляем статистику
            _processingStats = _processingStats with { TotalNormsLoaded = totalNormsLoaded };

            _logger.LogInformation("Загрузка норм завершена. Норм: {Norms}, ошибок: {Errors}", 
                totalNormsLoaded, totalErrors);
                
            return totalNormsLoaded > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при загрузке норм");
            return false;
        }
    }

    /// <summary>
    /// Анализирует участок - упрощенная версия совместимая с существующими типами
    /// </summary>
    public async Task<BasicAnalysisResult> AnalyzeSectionAsync(string sectionName, string? specificNormId = null)
    {
        using var operation = _performanceMonitor.StartOperation($"Analyze_Section_{sectionName}");
        
        _logger.LogInformation("Анализ участка: {Section}, норма: {NormId}", 
            sectionName, specificNormId ?? "авто");

        try
        {
            // Фильтруем маршруты для анализа
            var filteredRoutes = _loadedRoutes.Where(r => r.Sections.Any(s => s.Name == sectionName)).ToList();
            if (!filteredRoutes.Any())
            {
                _logger.LogWarning("Нет маршрутов для анализа участка {Section}", sectionName);
                return BasicAnalysisResult.Empty(sectionName);
            }

            // Получаем нормы для участка
            var availableNorms = await _normStorage.GetAllNormsAsync();
            if (!availableNorms.Any())
            {
                _logger.LogWarning("Нет норм для анализа участка {Section}", sectionName);
                return BasicAnalysisResult.Empty(sectionName);
            }

            // Выполняем упрощенный анализ
            var analysisItems = 0;
            decimal totalDeviation = 0;

            foreach (var route in filteredRoutes)
            {
                var sectionsToAnalyze = route.Sections.Where(s => s.Name == sectionName);
                
                foreach (var section in sectionsToAnalyze)
                {
                    // Берем первую доступную норму для простоты
                    var norm = availableNorms.FirstOrDefault();
                    if (norm != null)
                    {
                        var deviation = CalculateBasicDeviation(section, norm);
                        totalDeviation += deviation;
                        analysisItems++;
                    }
                }
            }

            var meanDeviation = analysisItems > 0 ? totalDeviation / analysisItems : 0;

            var result = new BasicAnalysisResult
            {
                SectionName = sectionName,
                TotalRoutes = filteredRoutes.Count,
                AnalyzedItems = analysisItems,
                MeanDeviation = meanDeviation,
                ProcessingTime = TimeSpan.FromMilliseconds(Environment.TickCount),
                AnalysisDate = DateTime.UtcNow
            };

            // Кэшируем результат
            _analyzedResults[sectionName] = result;

            _logger.LogInformation("Анализ завершен. Участок: {Section}, маршрутов: {Routes}, элементов: {Items}", 
                sectionName, result.TotalRoutes, analysisItems);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при анализе участка {Section}", sectionName);
            return BasicAnalysisResult.Empty(sectionName);
        }
    }

    /// <summary>
    /// Получает список доступных участков - использует существующие типы
    /// </summary>
    public async Task<IEnumerable<string>> GetAvailableSectionsAsync()
    {
        await Task.CompletedTask;
        return _loadedRoutes
            .SelectMany(r => r.Sections)
            .Select(s => s.Name)
            .Distinct()
            .OrderBy(name => name);
    }

    /// <summary>
    /// Получает статистику обработки - упрощенная версия
    /// </summary>
    public BasicProcessingStats GetProcessingStats() => _processingStats;

    /// <summary>
    /// Вычисляет базовое отклонение - упрощенная версия для совместимости
    /// </summary>
    private decimal CalculateBasicDeviation(Section section, Norm norm)
    {
        try
        {
            if (!norm.Points.Any()) return 0;

            // Используем интерполяцию для получения нормативного значения
            var interpolationFunction = _interpolationEngine.CreateInterpolationFunction(norm.Points);
            var normConsumption = interpolationFunction(section.TkmBrutto);
            
            if (normConsumption == 0) return 0;
            
            // Вычисляем отклонение в процентах
            return ((section.ActualConsumption - normConsumption) / normConsumption) * 100;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Ошибка вычисления отклонения: {Error}", ex.Message);
            return 0;
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("Освобождение ресурсов анализатора норм");
        _loadedRoutes.Clear();
        _analyzedResults.Clear();
    }
}