using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Data;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Полная реализация сервиса анализа данных
/// Соответствует InteractiveNormsAnalyzer + RouteDataAnalyzer из Python
/// </summary>
public class DataAnalysisService : IDataAnalysisService
{
    private readonly ILogger<DataAnalysisService> _logger;
    private readonly AnalysisNormDbContext _context;
    private readonly INormStorageService _normStorage;
    private readonly INormInterpolationService _interpolationService;
    private readonly ILocomotiveCoefficientService _coefficientService;
    private readonly IAnalysisCacheService _cacheService;
    private readonly ApplicationSettings _settings;

    public DataAnalysisService(
        ILogger<DataAnalysisService> logger,
        AnalysisNormDbContext context,
        INormStorageService normStorage,
        INormInterpolationService interpolationService,
        ILocomotiveCoefficientService coefficientService,
        IAnalysisCacheService cacheService,
        IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _context = context;
        _normStorage = normStorage;
        _interpolationService = interpolationService;
        _coefficientService = coefficientService;
        _cacheService = cacheService;
        _settings = settings.Value;
    }

    /// <summary>
    /// Анализирует участок с построением результатов для визуализации
    /// Соответствует analyze_section из Python InteractiveNormsAnalyzer
    /// </summary>
    public async Task<AnalysisResult> AnalyzeSectionAsync(
        string sectionName,
        string? normId = null,
        bool singleSectionOnly = false,
        AnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        options ??= new AnalysisOptions();

        _logger.LogInformation("Начинаем анализ участка {SectionName} (норма: {NormId}, только участок: {SingleSection})", 
            sectionName, normId ?? "все", singleSectionOnly);

        var analysisResult = new AnalysisResult
        {
            SectionName = sectionName,
            NormId = normId,
            SingleSectionOnly = singleSectionOnly,
            UseCoefficients = options.UseCoefficients,
            CreatedAt = DateTime.UtcNow,
            Routes = new List<Route>()
        };

        // Генерируем хэш для кэширования
        analysisResult.GenerateAnalysisHash();

        try
        {
            // Проверяем кэш
            var cachedResult = await _cacheService.GetCachedAnalysisAsync(analysisResult.AnalysisHash!);
            if (cachedResult != null)
            {
                _logger.LogDebug("Найден кэшированный результат анализа для участка {SectionName}", sectionName);
                return cachedResult;
            }

            // Загружаем маршруты для анализа
            var routes = await LoadRoutesForAnalysisAsync(sectionName, singleSectionOnly, options, cancellationToken);
            if (!routes.Any())
            {
                _logger.LogWarning("Не найдены маршруты для анализа участка {SectionName}", sectionName);
                analysisResult.ErrorMessage = "Не найдены маршруты для анализа";
                return analysisResult;
            }

            _logger.LogDebug("Загружено {RouteCount} маршрутов для анализа", routes.Count);
            analysisResult.TotalRoutes = routes.Count;

            // Применяем фильтрацию локомотивов если указана
            if (options.SelectedLocomotives?.Any() == true)
            {
                routes = FilterRoutesByLocomotives(routes, options.SelectedLocomotives).ToList();
                _logger.LogDebug("После фильтрации локомотивов осталось {RouteCount} маршрутов", routes.Count);
            }

            // Исключаем маршруты с малой работой если требуется
            if (options.ExcludeLowWork)
            {
                var beforeCount = routes.Count;
                routes = routes.Where(r => (r.WorkFact ?? 0) >= (decimal)_settings.MinWorkThreshold).ToList();
                _logger.LogDebug("Исключено {ExcludedCount} маршрутов с малой работой", beforeCount - routes.Count);
            }

            if (!routes.Any())
            {
                analysisResult.ErrorMessage = "Все маршруты отфильтрованы";
                return analysisResult;
            }

            // Применяем коэффициенты локомотивов если требуется
            if (options.UseCoefficients)
            {
                await _coefficientService.ApplyCoefficientsAsync(routes);
                _logger.LogDebug("Применены коэффициенты локомотивов к маршрутам");
            }

            // Выполняем основной анализ
            var analyzedRoutes = await PerformRouteAnalysisAsync(routes, normId, cancellationToken);
            analysisResult.Routes = analyzedRoutes.ToList();
            analysisResult.AnalyzedRoutes = analyzedRoutes.Count();

            // Вычисляем статистики
            await CalculateAnalysisStatisticsAsync(analysisResult);

            // Сохраняем результат в кэш
            analysisResult.CompletedAt = DateTime.UtcNow;
            analysisResult.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            await _cacheService.SaveAnalysisToCacheAsync(analysisResult);

            _logger.LogInformation("Анализ участка {SectionName} завершен: проанализировано {AnalyzedCount} из {TotalCount} маршрутов за {ElapsedMs}мс", 
                sectionName, analysisResult.AnalyzedRoutes, analysisResult.TotalRoutes, stopwatch.ElapsedMilliseconds);

            return analysisResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Анализ участка {SectionName} отменен пользователем", sectionName);
            analysisResult.ErrorMessage = "Анализ отменен пользователем";
            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка анализа участка {SectionName}", sectionName);
            analysisResult.ErrorMessage = ex.Message;
            analysisResult.CompletedAt = DateTime.UtcNow;
            return analysisResult;
        }
    }

    /// <summary>
    /// Получает список участков из базы данных
    /// Соответствует get_sections_list из Python HTMLRouteProcessor
    /// </summary>
    public async Task<IEnumerable<string>> GetSectionsListAsync()
    {
        _logger.LogDebug("Получаем список участков");

        try
        {
            var sections = await _context.Routes
                .Where(r => !string.IsNullOrEmpty(r.SectionName))
                .Select(r => r.SectionName!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            _logger.LogDebug("Найдено {Count} участков", sections.Count);
            return sections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения списка участков");
            return new List<string>();
        }
    }

    /// <summary>
    /// Получает нормы для участка с количествами маршрутов
    /// Соответствует get_norms_with_counts_for_section из Python
    /// </summary>
    public async Task<IEnumerable<NormWithCount>> GetNormsWithCountsForSectionAsync(
        string sectionName, 
        bool singleSectionOnly = false)
    {
        if (string.IsNullOrEmpty(sectionName))
            return new List<NormWithCount>();

        _logger.LogDebug("Получаем нормы с количествами для участка {SectionName} (только участок: {SingleSection})", 
            sectionName, singleSectionOnly);

        try
        {
            IQueryable<Route> routesQuery = _context.Routes.Where(r => !string.IsNullOrEmpty(r.NormNumber));

            if (singleSectionOnly)
            {
                routesQuery = routesQuery.Where(r => r.SectionName == sectionName);
            }
            else
            {
                // Включаем маршруты которые проходят через этот участок
                routesQuery = routesQuery.Where(r => r.SectionName!.Contains(sectionName) || 
                                                    r.SectionName == sectionName);
            }

            var normCounts = await routesQuery
                .GroupBy(r => r.NormNumber)
                .Select(g => new NormWithCount(g.Key!, g.Count()))
                .OrderByDescending(nc => nc.RouteCount)
                .ThenBy(nc => nc.NormId)
                .ToListAsync();

            _logger.LogDebug("Найдено {Count} норм для участка {SectionName}", normCounts.Count, sectionName);
            return normCounts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения норм для участка {SectionName}", sectionName);
            return new List<NormWithCount>();
        }
    }

    /// <summary>
    /// Получает подробную статистику по участку
    /// </summary>
    public async Task<SectionStatistics> GetSectionStatisticsAsync(string sectionName, bool singleSectionOnly = false)
    {
        if (string.IsNullOrEmpty(sectionName))
            return new SectionStatistics();

        _logger.LogDebug("Вычисляем статистику для участка {SectionName}", sectionName);

        try
        {
            var routesQuery = BuildSectionRoutesQuery(sectionName, singleSectionOnly);

            var routes = await routesQuery
                .Where(r => r.DeviationPercent.HasValue)
                .ToListAsync();

            if (!routes.Any())
            {
                return new SectionStatistics { SectionName = sectionName };
            }

            var deviations = routes.Select(r => r.DeviationPercent!.Value).ToList();
            var statusCounts = routes.GroupBy(r => r.Status ?? "Неизвестно")
                .ToDictionary(g => g.Key, g => g.Count());

            var statistics = new SectionStatistics
            {
                SectionName = sectionName,
                TotalRoutes = routes.Count,
                AverageDeviation = deviations.Average(),
                MinDeviation = deviations.Min(),
                MaxDeviation = deviations.Max(),
                MedianDeviation = CalculateMedian(deviations),
                StandardDeviation = CalculateStandardDeviation(deviations),
                StatusCounts = statusCounts,
                EconomyCount = statusCounts.Where(kvp => kvp.Key.Contains("Экономия")).Sum(kvp => kvp.Value),
                OverrunCount = statusCounts.Where(kvp => kvp.Key.Contains("Перерасход")).Sum(kvp => kvp.Value),
                NormalCount = statusCounts.ContainsKey(DeviationStatus.Normal) ? statusCounts[DeviationStatus.Normal] : 0
            };

            _logger.LogDebug("Статистика участка {SectionName}: {RouteCount} маршрутов, среднее отклонение {AvgDeviation:F2}%", 
                sectionName, statistics.TotalRoutes, statistics.AverageDeviation);

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка вычисления статистики для участка {SectionName}", sectionName);
            return new SectionStatistics { SectionName = sectionName };
        }
    }

    /// <summary>
    /// Загружает маршруты для анализа
    /// </summary>
    private async Task<List<Route>> LoadRoutesForAnalysisAsync(
        string sectionName, 
        bool singleSectionOnly, 
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var query = BuildSectionRoutesQuery(sectionName, singleSectionOnly);

        // Фильтруем только маршруты с необходимыми данными для анализа
        query = query.Where(r => 
            !string.IsNullOrEmpty(r.NormNumber) &&
            r.FactConsumption.HasValue &&
            r.FactConsumption > 0 &&
            r.AxleLoad.HasValue &&
            r.AxleLoad > 0);

        var routes = await query.ToListAsync(cancellationToken);

        _logger.LogTrace("Загружено {Count} подходящих маршрутов для анализа", routes.Count);
        return routes;
    }

    /// <summary>
    /// Строит запрос маршрутов для участка
    /// </summary>
    private IQueryable<Route> BuildSectionRoutesQuery(string sectionName, bool singleSectionOnly)
    {
        IQueryable<Route> query = _context.Routes;

        if (singleSectionOnly)
        {
            query = query.Where(r => r.SectionName == sectionName);
        }
        else
        {
            // Включаем маршруты которые проходят через этот участок (содержат название)
            query = query.Where(r => r.SectionName!.Contains(sectionName) || r.SectionName == sectionName);
        }

        return query.OrderBy(r => r.RouteDate).ThenBy(r => r.RouteNumber);
    }

    /// <summary>
    /// Выполняет основной анализ маршрутов
    /// Соответствует analyze_routes из Python RouteDataAnalyzer
    /// </summary>
    private async Task<IEnumerable<Route>> PerformRouteAnalysisAsync(
        List<Route> routes, 
        string? specificNormId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Выполняем анализ {Count} маршрутов", routes.Count);

        var analyzedRoutes = new List<Route>();
        var processedCount = 0;
        var skippedCount = 0;

        // Группируем маршруты по номеру нормы для эффективной обработки
        var routeGroups = routes.GroupBy(r => r.NormNumber).ToList();
        _logger.LogDebug("Маршруты сгруппированы по {GroupCount} нормам", routeGroups.Count);

        foreach (var routeGroup in routeGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normNumber = routeGroup.Key!;
            var routesInGroup = routeGroup.ToList();

            // Пропускаем если указана конкретная норма и это не она
            if (!string.IsNullOrEmpty(specificNormId) && normNumber != specificNormId)
            {
                skippedCount += routesInGroup.Count;
                continue;
            }

            try
            {
                // Анализируем группу маршрутов с одной нормой
                var analyzed = await AnalyzeRouteGroupAsync(routesInGroup, normNumber, cancellationToken);
                analyzedRoutes.AddRange(analyzed);
                processedCount += analyzed.Count();

                _logger.LogTrace("Проанализированы маршруты с нормой {NormId}: {Count} из {Total}", 
                    normNumber, analyzed.Count(), routesInGroup.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка анализа маршрутов с нормой {NormId}", normNumber);
                skippedCount += routesInGroup.Count;
            }
        }

        _logger.LogDebug("Анализ маршрутов завершен: обработано {ProcessedCount}, пропущено {SkippedCount}", 
            processedCount, skippedCount);

        return analyzedRoutes;
    }

    /// <summary>
    /// Анализирует группу маршрутов с одной нормой
    /// </summary>
    private async Task<IEnumerable<Route>> AnalyzeRouteGroupAsync(
        List<Route> routes, 
        string normId,
        CancellationToken cancellationToken)
    {
        var analyzedRoutes = new List<Route>();

        // Получаем норму из хранилища
        var norm = await _normStorage.GetNormAsync(normId);
        if (norm == null || !norm.CanInterpolate())
        {
            _logger.LogWarning("Норма {NormId} не найдена или не может использоваться для интерполяции", normId);
            return routes; // Возвращаем маршруты без анализа
        }

        foreach (var route in routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Интерполируем нормативное значение расхода
                var interpolatedNorm = await _interpolationService.InterpolateNormValueAsync(normId, route.AxleLoad!.Value);
                if (!interpolatedNorm.HasValue)
                {
                    _logger.LogTrace("Не удалось интерполировать норму {NormId} для нагрузки {Load}", 
                        normId, route.AxleLoad);
                    continue; // Пропускаем этот маршрут
                }

                // Заполняем интерполированные данные
                route.NormInterpolated = interpolatedNorm.Value;
                route.NormalizationParameter = GetNormalizationParameter(norm.NormType ?? "Нажатие");
                route.ParameterValue = route.AxleLoad.Value;

                // Вычисляем нормативный расход в том же формате что и фактический
                var normConsumptionForComparison = CalculateNormConsumptionForComparison(route, interpolatedNorm.Value);
                route.NormConsumption = normConsumptionForComparison;

                // Вычисляем отклонение
                if (route.FactConsumption.HasValue && normConsumptionForComparison > 0)
                {
                    var deviation = (route.FactConsumption.Value - normConsumptionForComparison) / normConsumptionForComparison * 100;
                    route.DeviationPercent = Math.Round(deviation, 2);
                    route.Status = DeviationStatus.GetStatus(deviation);

                    // Устанавливаем флаги для цветового выделения (как в Python)
                    route.UseRedColor = deviation > _settings.DefaultTolerancePercent;
                    route.UseRedRashod = Math.Abs(deviation) > _settings.DefaultTolerancePercent;
                }

                analyzedRoutes.Add(route);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка анализа маршрута {RouteNumber}", route.RouteNumber);
                // Добавляем маршрут без анализа
                analyzedRoutes.Add(route);
            }
        }

        return analyzedRoutes;
    }

    /// <summary>
    /// Определяет параметр нормализации в зависимости от типа нормы
    /// </summary>
    private string GetNormalizationParameter(string normType)
    {
        return normType.ToLower() switch
        {
            var t when t.Contains("нажатие") => "Нагрузка на ось, т/ось",
            var t when t.Contains("н/ф") => "Нагрузка на ось, т/ось", 
            var t when t.Contains("уд") => "Вес состава, т",
            _ => "Нагрузка на ось, т/ось"
        };
    }

    /// <summary>
    /// Вычисляет нормативный расход для сравнения с фактическим
    /// </summary>
    private decimal CalculateNormConsumptionForComparison(Route route, decimal interpolatedNormValue)
    {
        // Интерполированная норма обычно дается в кВт·ч/10⁴ ткм
        // Нужно привести к формату фактического расхода

        if (route.TonKilometers.HasValue && route.TonKilometers.Value > 0)
        {
            // Преобразуем удельную норму в абсолютный расход
            return interpolatedNormValue * route.TonKilometers.Value / 10000;
        }
        else if (route.Kilometers.HasValue && route.BruttoTons.HasValue && 
                 route.Kilometers.Value > 0 && route.BruttoTons.Value > 0)
        {
            // Рассчитываем ткм и затем норму
            var tonKm = route.Kilometers.Value * route.BruttoTons.Value;
            return interpolatedNormValue * tonKm / 10000;
        }

        // Если нет данных для преобразования, возвращаем как есть
        return interpolatedNormValue;
    }

    /// <summary>
    /// Фильтрует маршруты по выбранным локомотивам
    /// </summary>
    private IEnumerable<Route> FilterRoutesByLocomotives(
        IEnumerable<Route> routes, 
        IEnumerable<(string Series, int Number)> selectedLocomotives)
    {
        var selectedSet = selectedLocomotives.ToHashSet();
        
        return routes.Where(route =>
        {
            if (string.IsNullOrEmpty(route.LocomotiveSeries) || !route.LocomotiveNumber.HasValue)
                return false;

            return selectedSet.Contains((route.LocomotiveSeries, route.LocomotiveNumber.Value));
        });
    }

    /// <summary>
    /// Вычисляет статистики анализа
    /// </summary>
    private async Task CalculateAnalysisStatisticsAsync(AnalysisResult analysisResult)
    {
        var routes = analysisResult.Routes.ToList();
        if (!routes.Any())
        {
            analysisResult.AverageDeviation = null;
            return;
        }

        var routesWithDeviations = routes.Where(r => r.DeviationPercent.HasValue).ToList();
        
        if (routesWithDeviations.Any())
        {
            var deviations = routesWithDeviations.Select(r => r.DeviationPercent!.Value).ToList();
            
            analysisResult.AverageDeviation = deviations.Average();
            analysisResult.MinDeviation = deviations.Min();
            analysisResult.MaxDeviation = deviations.Max();
            analysisResult.MedianDeviation = CalculateMedian(deviations);
            analysisResult.StandardDeviation = CalculateStandardDeviation(deviations);

            // Статистика по статусам
            var statusCounts = routesWithDeviations.GroupBy(r => r.Status ?? "Неизвестно")
                .ToDictionary(g => g.Key, g => g.Count());
            
            analysisResult.EconomyCount = statusCounts.Where(kvp => kvp.Key.Contains("Экономия")).Sum(kvp => kvp.Value);
            analysisResult.OverrunCount = statusCounts.Where(kvp => kvp.Key.Contains("Перерасход")).Sum(kvp => kvp.Value);
            analysisResult.NormalCount = statusCounts.ContainsKey(DeviationStatus.Normal) ? statusCounts[DeviationStatus.Normal] : 0;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Вычисляет медиану
    /// </summary>
    private decimal CalculateMedian(List<decimal> values)
    {
        if (!values.Any()) return 0;
        
        var sorted = values.OrderBy(x => x).ToList();
        var count = sorted.Count;
        
        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
        }
        else
        {
            return sorted[count / 2];
        }
    }

    /// <summary>
    /// Вычисляет стандартное отклонение
    /// </summary>
    private decimal CalculateStandardDeviation(List<decimal> values)
    {
        if (values.Count <= 1) return 0;
        
        var mean = values.Average();
        var variance = values.Sum(x => (decimal)Math.Pow((double)(x - mean), 2)) / (values.Count - 1);
        
        return (decimal)Math.Sqrt((double)variance);
    }
}

/// <summary>
/// Подробная статистика по участку
/// </summary>
public class SectionStatistics
{
    public string SectionName { get; set; } = string.Empty;
    public int TotalRoutes { get; set; }
    public decimal AverageDeviation { get; set; }
    public decimal MinDeviation { get; set; }
    public decimal MaxDeviation { get; set; }
    public decimal MedianDeviation { get; set; }
    public decimal StandardDeviation { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    public int EconomyCount { get; set; }
    public int OverrunCount { get; set; }
    public int NormalCount { get; set; }
}