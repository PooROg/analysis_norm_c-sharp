// Services/Implementation/InteractiveNormsAnalyzer.cs (ОБНОВЛЕННЫЙ для CHAT 2)
using System.Collections.Concurrent;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Models.Domain;
using AnalysisNorm.Infrastructure.Mathematics;
using AnalysisNorm.Infrastructure.Logging;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// ОБНОВЛЕННЫЙ InteractiveNormsAnalyzer для CHAT 2
/// Добавляет: load_routes_from_html, load_norms_from_html, _build_sections_norms_map
/// Точное соответствие Python analyzer.py функциональности
/// </summary>
public class InteractiveNormsAnalyzer : IInteractiveNormsAnalyzer, IDisposable
{
    private readonly IHtmlParser _htmlParser;
    private readonly INormStorage _normStorage;
    private readonly IApplicationLogger _logger;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly InterpolationEngine _interpolationEngine;
    private readonly StatusClassifier _statusClassifier;

    // Основные данные - обновлены для CHAT 2
    private readonly List<Route> _loadedRoutes = new();
    private readonly ConcurrentDictionary<string, BasicAnalysisResult> _analyzedResults = new();
    private readonly ConcurrentDictionary<string, List<string>> _sectionsNormsMap = new(); // НОВОЕ: карта участков и норм
    private BasicProcessingStats _processingStats = new();

    // НОВОЕ для CHAT 2: дополнительная статистика
    private DateTime _lastLoadTime = DateTime.MinValue;
    private int _totalProcessedFiles = 0;

    public IReadOnlyList<Route> LoadedRoutes => _loadedRoutes.AsReadOnly();

    // НОВОЕ для CHAT 2: публичный доступ к результатам анализа
    public IReadOnlyDictionary<string, BasicAnalysisResult> AnalyzedResults => 
        new Dictionary<string, BasicAnalysisResult>(_analyzedResults);

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
        
        _interpolationEngine = new InterpolationEngine(logger);
        _statusClassifier = new StatusClassifier();

        _logger.LogInformation("Инициализирован InteractiveNormsAnalyzer с расширенными возможностями CHAT 2");
    }

    /// <summary>
    /// НОВЫЙ метод: Загрузка маршрутов из HTML файлов - точная копия Python load_routes_from_html
    /// </summary>
    public async Task<bool> LoadRoutesFromHtmlAsync(List<string> htmlFiles)
    {
        if (!htmlFiles?.Any() == true)
        {
            _logger.LogWarning("Список HTML файлов пуст");
            return false;
        }

        var operationId = Guid.NewGuid().ToString("N")[..8];
        _performanceMonitor.StartOperation($"LoadRoutesFromHtml_{operationId}");

        try
        {
            _logger.LogInformation("Загрузка маршрутов из {Count} HTML файлов", htmlFiles.Count);

            // Очищаем предыдущие данные
            _loadedRoutes.Clear();
            _analyzedResults.Clear();
            _totalProcessedFiles = 0;

            var allRoutes = new List<Route>();
            var totalProcessingTime = TimeSpan.Zero;

            // Обрабатываем файлы последовательно (как в Python)
            foreach (var filePath in htmlFiles)
            {
                try
                {
                    var fileStartTime = DateTime.UtcNow;
                    _logger.LogDebug("Обработка файла: {FileName}", Path.GetFileName(filePath));

                    using var fileStream = File.OpenRead(filePath);
                    var parseResult = await _htmlParser.ParseRoutesAsync(fileStream);

                    if (parseResult.IsSuccess && parseResult.Data != null)
                    {
                        var routesFromFile = parseResult.Data.ToList();
                        allRoutes.AddRange(routesFromFile);
                        _totalProcessedFiles++;

                        var fileProcessingTime = DateTime.UtcNow - fileStartTime;
                        totalProcessingTime += fileProcessingTime;

                        _logger.LogDebug("Файл {FileName} обработан за {Time}мс. Маршрутов: {Count}", 
                            Path.GetFileName(filePath), fileProcessingTime.TotalMilliseconds, routesFromFile.Count);
                    }
                    else
                    {
                        _logger.LogWarning("Ошибка парсинга файла {FileName}: {Error}", 
                            Path.GetFileName(filePath), parseResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка обработки файла {FileName}", Path.GetFileName(filePath));
                }
            }

            if (allRoutes.Count == 0)
            {
                _logger.LogError("Не удалось загрузить маршруты ни из одного файла");
                return false;
            }

            // Добавляем все маршруты в основную коллекцию
            foreach (var route in allRoutes)
            {
                _loadedRoutes.Add(route);
            }

            // НОВОЕ: Строим карту участков и норм (из Python _build_sections_norms_map)
            await BuildSectionsNormsMapAsync();

            // Обновляем статистику
            _processingStats = new BasicProcessingStats
            {
                TotalRoutesLoaded = _loadedRoutes.Count,
                TotalNormsLoaded = (await _normStorage.GetAllNormsAsync()).Count(),
                TotalErrors = htmlFiles.Count - _totalProcessedFiles,
                ProcessingTime = totalProcessingTime,
                LastProcessingDate = DateTime.UtcNow
            };

            _lastLoadTime = DateTime.UtcNow;

            _logger.LogInformation("Загрузка маршрутов завершена. Обработано файлов: {Files}/{Total}, маршрутов: {Routes}", 
                _totalProcessedFiles, htmlFiles.Count, _loadedRoutes.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при загрузке маршрутов из HTML");
            return false;
        }
        finally
        {
            _performanceMonitor.EndOperation($"LoadRoutesFromHtml_{operationId}");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Загрузка норм из HTML файлов - точная копия Python load_norms_from_html
    /// </summary>
    public async Task<bool> LoadNormsFromHtmlAsync(List<string> htmlFiles)
    {
        if (!htmlFiles?.Any() == true)
        {
            _logger.LogWarning("Список HTML файлов норм пуст");
            return false;
        }

        var operationId = Guid.NewGuid().ToString("N")[..8];
        _performanceMonitor.StartOperation($"LoadNormsFromHtml_{operationId}");

        try
        {
            _logger.LogInformation("Загрузка норм из {Count} HTML файлов", htmlFiles.Count);

            var totalNormsLoaded = 0;
            var successfulFiles = 0;

            foreach (var filePath in htmlFiles)
            {
                try
                {
                    _logger.LogDebug("Обработка файла норм: {FileName}", Path.GetFileName(filePath));

                    using var fileStream = File.OpenRead(filePath);
                    var parseResult = await _htmlParser.ParseNormsAsync(fileStream);

                    if (parseResult.IsSuccess && parseResult.Data != null)
                    {
                        var normsFromFile = parseResult.Data.ToList();
                        
                        // Сохраняем нормы в хранилище
                        foreach (var norm in normsFromFile)
                        {
                            await _normStorage.StoreNormAsync(norm);
                            totalNormsLoaded++;
                        }

                        successfulFiles++;
                        _logger.LogDebug("Из файла {FileName} загружено норм: {Count}", 
                            Path.GetFileName(filePath), normsFromFile.Count);
                    }
                    else
                    {
                        _logger.LogWarning("Ошибка парсинга норм из файла {FileName}: {Error}", 
                            Path.GetFileName(filePath), parseResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка обработки файла норм {FileName}", Path.GetFileName(filePath));
                }
            }

            if (totalNormsLoaded == 0)
            {
                _logger.LogWarning("Не найдено норм в предоставленных файлах");
                return false;
            }

            // Обновляем карту участков и норм после загрузки новых норм
            await BuildSectionsNormsMapAsync();

            _logger.LogInformation("Загрузка норм завершена. Обработано файлов: {Files}/{Total}, норм: {Norms}", 
                successfulFiles, htmlFiles.Count, totalNormsLoaded);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при загрузке норм из HTML");
            return false;
        }
        finally
        {
            _performanceMonitor.EndOperation($"LoadNormsFromHtml_{operationId}");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Построение карты участков и норм - точная копия Python _build_sections_norms_map
    /// </summary>
    private async Task BuildSectionsNormsMapAsync()
    {
        try
        {
            _logger.LogDebug("Построение карты участков и норм");

            _sectionsNormsMap.Clear();

            // Получаем все уникальные участки из загруженных маршрутов
            var uniqueSections = _loadedRoutes
                .SelectMany(route => route.Sections)
                .Select(section => section.Name)
                .Distinct()
                .ToList();

            // Получаем все доступные нормы
            var allNorms = await _normStorage.GetAllNormsAsync();
            var normsList = allNorms.ToList();

            // Строим карту соответствий
            foreach (var sectionName in uniqueSections)
            {
                var applicableNormIds = new List<string>();

                foreach (var norm in normsList)
                {
                    // Простая логика сопоставления (может быть расширена в будущем)
                    if (IsSectionMatchingNorm(sectionName, norm))
                    {
                        applicableNormIds.Add(norm.Id);
                    }
                }

                _sectionsNormsMap.TryAdd(sectionName, applicableNormIds);
            }

            _logger.LogInformation("Построена карта участков и норм. Участков: {Sections}, норм: {Norms}, соответствий: {Mappings}", 
                uniqueSections.Count, normsList.Count, _sectionsNormsMap.Values.Sum(list => list.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка построения карты участков и норм");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Проверка соответствия участка норме
    /// </summary>
    private bool IsSectionMatchingNorm(string sectionName, Norm norm)
    {
        // Упрощенная логика сопоставления
        var normalizedSection = sectionName.ToLowerInvariant().Trim();
        var normalizedNormName = norm.Metadata.Description?.ToLowerInvariant() ?? "";
        
        // Если у нормы нет описания, считаем её универсальной
        if (string.IsNullOrEmpty(normalizedNormName))
            return true;

        // Проверяем вхождение названий друг в друга
        return normalizedSection.Contains(normalizedNormName) || 
               normalizedNormName.Contains(normalizedSection);
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод анализа участка с использованием карты норм
    /// </summary>
    public async Task<BasicAnalysisResult> AnalyzeSectionAsync(string sectionName, string? specificNormId = null)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            throw new ArgumentException("Название участка не может быть пустым", nameof(sectionName));
        }

        // Проверяем кэш результатов
        var cacheKey = $"{sectionName}_{specificNormId ?? "all"}";
        if (_analyzedResults.TryGetValue(cacheKey, out var cachedResult))
        {
            _logger.LogDebug("Использован кэшированный результат анализа для участка {Section}", sectionName);
            return cachedResult;
        }

        var operationId = Guid.NewGuid().ToString("N")[..8];
        _performanceMonitor.StartOperation($"AnalyzeSection_{operationId}");

        try
        {
            // Фильтруем маршруты по участку
            var relevantRoutes = _loadedRoutes
                .Where(route => route.Sections.Any(section => 
                    section.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (!relevantRoutes.Any())
            {
                _logger.LogWarning("Не найдено маршрутов с участком {Section}", sectionName);
                return BasicAnalysisResult.Empty(sectionName);
            }

            // Получаем применимые нормы
            var applicableNorms = await GetApplicableNormsForSectionAsync(sectionName, specificNormId);
            
            if (!applicableNorms.Any())
            {
                _logger.LogWarning("Не найдено применимых норм для участка {Section}", sectionName);
                return BasicAnalysisResult.Empty(sectionName);
            }

            // Выполняем анализ
            var analysisItems = new List<decimal>();
            var totalDeviation = 0m;

            foreach (var route in relevantRoutes)
            {
                var sectionsInRoute = route.Sections
                    .Where(s => s.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase));

                foreach (var section in sectionsInRoute)
                {
                    var deviation = await CalculateSectionDeviationAsync(section, applicableNorms);
                    analysisItems.Add(deviation);
                    totalDeviation += deviation;
                }
            }

            var meanDeviation = analysisItems.Any() ? totalDeviation / analysisItems.Count : 0;

            var result = new BasicAnalysisResult
            {
                SectionName = sectionName,
                TotalRoutes = relevantRoutes.Count,
                AnalyzedItems = analysisItems.Count,
                MeanDeviation = meanDeviation,
                ProcessingTime = TimeSpan.FromMilliseconds(Environment.TickCount),
                AnalysisDate = DateTime.UtcNow
            };

            // Кэшируем результат
            _analyzedResults.TryAdd(cacheKey, result);

            _logger.LogInformation("Анализ участка {Section} завершен. Маршрутов: {Routes}, элементов: {Items}, среднее отклонение: {Deviation}%", 
                sectionName, result.TotalRoutes, result.AnalyzedItems, result.MeanDeviation);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при анализе участка {Section}", sectionName);
            return BasicAnalysisResult.Empty(sectionName);
        }
        finally
        {
            _performanceMonitor.EndOperation($"AnalyzeSection_{operationId}");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Получение применимых норм для участка
    /// </summary>
    private async Task<List<Norm>> GetApplicableNormsForSectionAsync(string sectionName, string? specificNormId)
    {
        var applicableNorms = new List<Norm>();

        if (!string.IsNullOrEmpty(specificNormId))
        {
            // Ищем конкретную норму
            var specificNorm = await _normStorage.GetNormByIdAsync(specificNormId);
            if (specificNorm != null)
            {
                applicableNorms.Add(specificNorm);
            }
        }
        else
        {
            // Используем карту участков и норм
            if (_sectionsNormsMap.TryGetValue(sectionName, out var normIds))
            {
                foreach (var normId in normIds)
                {
                    var norm = await _normStorage.GetNormByIdAsync(normId);
                    if (norm != null)
                    {
                        applicableNorms.Add(norm);
                    }
                }
            }

            // Если в карте ничего не найдено, пытаемся найти универсальные нормы
            if (!applicableNorms.Any())
            {
                var allNorms = await _normStorage.GetAllNormsAsync();
                applicableNorms.AddRange(allNorms.Where(norm => IsSectionMatchingNorm(sectionName, norm)));
            }
        }

        return applicableNorms;
    }

    /// <summary>
    /// НОВЫЙ метод: Расчет отклонения участка с использованием интерполяции
    /// </summary>
    private async Task<decimal> CalculateSectionDeviationAsync(Section section, List<Norm> applicableNorms)
    {
        await Task.Yield();

        try
        {
            if (!applicableNorms.Any() || section.ActualConsumption <= 0)
                return 0;

            // Выбираем наиболее подходящую норму
            var bestNorm = SelectBestNormForSection(section, applicableNorms);
            
            if (bestNorm?.Points?.Any() != true)
                return 0;

            // Используем интерполяцию для получения нормативного значения
            var interpolationFunction = _interpolationEngine.CreateInterpolationFunction(bestNorm.Points);
            var normConsumption = interpolationFunction(section.TkmBrutto);
            
            if (normConsumption <= 0)
                return 0;
            
            // Вычисляем отклонение в процентах
            var deviation = ((section.ActualConsumption - normConsumption) / normConsumption) * 100;
            
            _logger.LogDebug("Участок {Section}: факт={Actual}, норма={Norm}, отклонение={Deviation}%", 
                section.Name, section.ActualConsumption, normConsumption, deviation);
            
            return deviation;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Ошибка вычисления отклонения для участка {Section}: {Error}", section.Name, ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Выбор наилучшей нормы для участка
    /// </summary>
    private Norm? SelectBestNormForSection(Section section, List<Norm> applicableNorms)
    {
        if (!applicableNorms.Any())
            return null;

        if (applicableNorms.Count == 1)
            return applicableNorms[0];

        // Выбираем норму с наибольшим количеством точек данных (более точная интерполяция)
        return applicableNorms
            .OrderByDescending(norm => norm.Points?.Count ?? 0)
            .ThenBy(norm => norm.Type) // Приоритет по типу нормы
            .First();
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод получения доступных участков
    /// </summary>
    public async Task<IEnumerable<string>> GetAvailableSectionsAsync()
    {
        await Task.Yield();
        
        var sections = _loadedRoutes
            .SelectMany(r => r.Sections)
            .Select(s => s.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        _logger.LogDebug("Доступно участков для анализа: {Count}", sections.Count);
        return sections;
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод получения статистики обработки
    /// </summary>
    public BasicProcessingStats GetProcessingStats()
    {
        return _processingStats with
        {
            TotalNormsLoaded = _normStorage.GetAllNormsAsync().Result.Count(),
            LastProcessingDate = _lastLoadTime
        };
    }

    public void Dispose()
    {
        _logger.LogInformation("Освобождение ресурсов InteractiveNormsAnalyzer");
        _loadedRoutes.Clear();
        _analyzedResults.Clear();
        _sectionsNormsMap.Clear();
    }
}