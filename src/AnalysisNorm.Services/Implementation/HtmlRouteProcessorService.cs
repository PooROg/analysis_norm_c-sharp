using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Основной процессор HTML файлов маршрутов
/// Точное соответствие HTMLRouteProcessor из Python analysis/html_route_processor.py
/// </summary>
public class HtmlRouteProcessorService : IHtmlRouteProcessorService
{
    private readonly ILogger<HtmlRouteProcessorService> _logger;
    private readonly IFileEncodingDetector _encodingDetector;
    private readonly ITextNormalizer _textNormalizer;
    private readonly ApplicationSettings _settings;

    // Processing statistics - аналог processing_stats из Python
    private readonly ProcessingStatistics _stats = new();
    
    // Compiled regex patterns for performance (как в Python)
    private static readonly Regex RouteNumberPattern = new(@"Маршрут\s*№?\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DatePattern = new(@"(\d{1,2})\.(\d{1,2})\.(\d{4})", RegexOptions.Compiled);
    private static readonly Regex DriverTabPattern = new(@"Табельный\s+машиниста[:\s]+(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CleanupPattern = new(@"\s+", RegexOptions.Compiled);
    
    public HtmlRouteProcessorService(
        ILogger<HtmlRouteProcessorService> logger,
        IFileEncodingDetector encodingDetector,
        ITextNormalizer textNormalizer,
        IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _encodingDetector = encodingDetector;
        _textNormalizer = textNormalizer;
        _settings = settings.Value;
    }

    /// <summary>
    /// Обрабатывает список HTML файлов маршрутов
    /// Соответствует process_html_files из Python HTMLRouteProcessor
    /// </summary>
    public async Task<ProcessingResult<IEnumerable<Route>>> ProcessHtmlFilesAsync(
        IEnumerable<string> htmlFiles, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var allRoutes = new ConcurrentBag<Route>();
        var fileStats = new ConcurrentDictionary<string, object>();
        
        _logger.LogInformation("Начинаем обработку {FileCount} HTML файлов маршрутов", htmlFiles.Count());
        
        var filesArray = htmlFiles.ToArray();
        _stats.TotalFiles = filesArray.Length;

        try
        {
            // Параллельная обработка файлов (улучшение по сравнению с Python)
            var tasks = filesArray.Select(async filePath =>
            {
                try 
                {
                    var routes = await ProcessSingleHtmlFileAsync(filePath, cancellationToken);
                    foreach (var route in routes)
                    {
                        allRoutes.Add(route);
                    }
                    
                    Interlocked.Increment(ref _stats.ProcessedFiles);
                    _logger.LogDebug("Файл {FilePath} обработан успешно, маршрутов: {Count}", 
                        filePath, routes.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка обработки файла {FilePath}", filePath);
                    Interlocked.Increment(ref _stats.SkippedFiles);
                    fileStats[filePath] = new { Error = ex.Message };
                }
            });

            await Task.WhenAll(tasks);
            
            var routesList = allRoutes.ToList();
            _stats.TotalRoutes = routesList.Count;
            _stats.ProcessedRoutes = routesList.Count(r => !string.IsNullOrEmpty(r.RouteNumber));
            _stats.ProcessingTime = stopwatch.Elapsed;
            
            // Группировка и обработка дубликатов (как в Python extract_route_key)
            var processedRoutes = await ProcessDuplicatesAsync(routesList);
            
            _logger.LogInformation("Обработка завершена: {ProcessedFiles}/{TotalFiles} файлов, {TotalRoutes} маршрутов", 
                _stats.ProcessedFiles, _stats.TotalFiles, processedRoutes.Count);

            return new ProcessingResult<IEnumerable<Route>>(
                Success: true, 
                Data: processedRoutes,
                ErrorMessage: null,
                Statistics: _stats with { Details = fileStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при обработке HTML файлов");
            return new ProcessingResult<IEnumerable<Route>>(
                Success: false,
                Data: null,
                ErrorMessage: ex.Message,
                Statistics: _stats
            );
        }
    }

    /// <summary>
    /// Обрабатывает один HTML файл
    /// Соответствует логике _process_routes из Python
    /// </summary>
    private async Task<List<Route>> ProcessSingleHtmlFileAsync(string filePath, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Начинаем обработку файла: {FilePath}", filePath);
        
        // Читаем файл с детекцией кодировки (аналог read_text из Python)
        var htmlContent = await _encodingDetector.ReadTextWithEncodingDetectionAsync(filePath);
        if (string.IsNullOrEmpty(htmlContent))
        {
            _logger.LogWarning("Файл {FilePath} пуст или не удалось прочитать", filePath);
            return new List<Route>();
        }

        // Очистка HTML контента (аналог _clean_html_content из Python)
        var cleanedContent = CleanHtmlContent(htmlContent);
        
        // Извлекаем маршруты (аналог extract_routes_from_html из Python)
        var routeBlocks = ExtractRouteBlocks(cleanedContent);
        if (!routeBlocks.Any())
        {
            _logger.LogWarning("В файле {FilePath} не найдены маршруты", filePath);
            return new List<Route>();
        }

        var routes = new List<Route>();
        foreach (var (routeHtml, metadata) in routeBlocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                // Проверяем фильтр Ю6 (аналог check_yu6_filter из Python)
                if (CheckYu6Filter(routeHtml))
                {
                    Interlocked.Increment(ref _stats.SkippedFiles);
                    continue;
                }

                // Парсим маршрут в объекты Route (аналог parse_html_route из Python)
                var routeData = ParseHtmlRoute(routeHtml, metadata);
                if (routeData.Any())
                {
                    routes.AddRange(routeData);
                    Interlocked.Add(ref _stats.ProcessedRoutes, routeData.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка парсинга маршрута в файле {FilePath}", filePath);
                Interlocked.Increment(ref _stats.SkippedFiles);
            }
        }

        _logger.LogDebug("Файл {FilePath} обработан: {RouteCount} маршрутов", filePath, routes.Count);
        return routes;
    }

    /// <summary>
    /// Очищает HTML контент от лишних элементов
    /// Соответствует _clean_html_content из Python HTMLRouteProcessor
    /// </summary>
    private string CleanHtmlContent(string htmlContent)
    {
        _logger.LogDebug("Очищаем HTML код от лишних элементов");
        var originalSize = htmlContent.Length;

        // Удаляем лишние элементы (как в Python регулярными выражениями)
        var patterns = new[]
        {
            (@"<font class = rcp12 ><center>Дата получения:.*?</font>\s*<br>", RegexOptions.Singleline),
            (@"<font class = rcp12 ><center>Номер маршрута:.*?</font><br>", RegexOptions.Singleline),
            (@"<tr class=tr_numline>.*?</tr>", RegexOptions.Singleline),
            (@"\s+ALIGN=center", RegexOptions.IgnoreCase),
            (@"\s+align=left", RegexOptions.IgnoreCase),
            (@"\s+align=right", RegexOptions.IgnoreCase),
            (@"<center>", RegexOptions.None),
            (@"</center>", RegexOptions.None),
            (@"<pre>", RegexOptions.None),
            (@"</pre>", RegexOptions.None),
            (@">[ \t]+<", RegexOptions.None)
        };

        foreach (var (pattern, options) in patterns)
        {
            htmlContent = Regex.Replace(htmlContent, pattern, "", options);
        }

        // Финальная очистка пробелов
        htmlContent = Regex.Replace(htmlContent, @">[ \t]+<", "><");

        var removedBytes = originalSize - htmlContent.Length;
        _logger.LogDebug("Удалено {RemovedBytes:N0} байт лишнего кода ({Percentage:F1}%)", 
            removedBytes, (double)removedBytes / Math.Max(originalSize, 1) * 100);
        
        return htmlContent;
    }

    /// <summary>
    /// Извлекает блоки маршрутов из HTML
    /// Соответствует extract_routes_from_html из Python
    /// </summary>
    private List<(string Html, RouteMetadata Metadata)> ExtractRouteBlocks(string htmlContent)
    {
        _logger.LogDebug("Извлекаем маршруты из HTML контента");

        var routes = new List<(string, RouteMetadata)>();
        
        // Поиск маркеров начала и конца маршрутов (как в Python)
        const string startMarker = "<!-- НАЧАЛО_ПЕРВОГО_МАРШРУТА -->";
        const string endMarker = "<!-- КОНЕЦ_ПОСЛЕДНЕГО_МАРШРУТА -->";
        
        var startPos = htmlContent.IndexOf(startMarker);
        var endPos = htmlContent.IndexOf(endMarker);
        
        var routesSection = (startPos == -1 || endPos == -1) 
            ? htmlContent 
            : htmlContent.Substring(startPos + startMarker.Length, endPos - startPos - startMarker.Length);

        var lines = routesSection.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            // Ищем строки с таблицами маршрутов (как в Python регулярными выражениями)
            if (Regex.IsMatch(trimmed, @"<table width=\d+%") && 
                (trimmed.Contains("Маршрут №") || trimmed.Contains("Маршрут")))
            {
                var metadata = ExtractRouteMetadata(trimmed);
                if (metadata != null)
                {
                    routes.Add((trimmed, metadata));
                    _logger.LogTrace("Найден маршрут: №{RouteNumber}", metadata.Number);
                }
            }
        }

        _logger.LogDebug("Извлечено маршрутов: {Count}", routes.Count);
        return routes;
    }

    /// <summary>
    /// Извлекает метаданные маршрута из HTML строки
    /// Соответствует extract_route_header_from_html из Python
    /// </summary>
    private RouteMetadata? ExtractRouteMetadata(string routeHtml)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(routeHtml);
            
            var metadata = new RouteMetadata();
            var text = _textNormalizer.NormalizeText(doc.DocumentNode.InnerText);

            // Извлечение номера маршрута (как в Python RouteNumberPattern)
            var routeMatch = RouteNumberPattern.Match(text);
            if (routeMatch.Success)
            {
                metadata.Number = routeMatch.Groups[1].Value;
            }

            // Извлечение дат (как в Python DatePattern)
            var dates = DatePattern.Matches(text)
                .Cast<Match>()
                .Select(m => $"{m.Groups[3].Value}{m.Groups[2].Value.PadLeft(2, '0')}{m.Groups[1].Value.PadLeft(2, '0')}")
                .ToList();

            if (dates.Count >= 2)
            {
                metadata.RouteDate = dates[0]; // Дата маршрута
                metadata.TripDate = dates[1];  // Дата поездки
            }
            else if (dates.Count == 1)
            {
                metadata.RouteDate = dates[0];
                metadata.TripDate = dates[0];
            }

            // Извлечение табельного номера машиниста (как в Python DriverTabPattern)
            var driverMatch = DriverTabPattern.Match(text);
            if (driverMatch.Success)
            {
                metadata.DriverTab = driverMatch.Groups[1].Value;
            }

            // Проверяем что все критические поля заполнены
            if (string.IsNullOrEmpty(metadata.Number) || 
                string.IsNullOrEmpty(metadata.TripDate) || 
                string.IsNullOrEmpty(metadata.DriverTab))
            {
                _logger.LogTrace("Неполные метаданные маршрута: {Text}", text.Substring(0, Math.Min(100, text.Length)));
                return null;
            }

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка извлечения метаданных маршрута");
            return null;
        }
    }

    /// <summary>
    /// Проверяет фильтр Ю6 (исключаем служебные маршруты)
    /// Соответствует check_yu6_filter из Python
    /// </summary>
    private bool CheckYu6Filter(string routeHtml)
    {
        // Ищем признаки служебных маршрутов Ю6
        var indicators = new[] { "Ю6", "служебн", "подач", "расстанов" };
        var normalizedHtml = _textNormalizer.NormalizeText(routeHtml).ToLower();
        
        return indicators.Any(indicator => normalizedHtml.Contains(indicator.ToLower()));
    }

    /// <summary>
    /// Парсит HTML маршрута в объекты Route
    /// Соответствует parse_html_route из Python
    /// </summary>
    private List<Route> ParseHtmlRoute(string routeHtml, RouteMetadata metadata)
    {
        var routes = new List<Route>();
        
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(routeHtml);
            
            // Извлекаем основные данные маршрута
            var baseRoute = new Route
            {
                RouteNumber = metadata.Number,
                RouteDate = metadata.RouteDate,
                TripDate = metadata.TripDate,
                DriverTab = metadata.DriverTab,
                CreatedAt = DateTime.UtcNow
            };

            // Генерируем ключ для группировки дубликатов (как в Python extract_route_key)
            baseRoute.GenerateRouteKey();
            
            // Парсим таблицы с данными маршрута
            var tables = doc.DocumentNode.SelectNodes("//table");
            if (tables != null)
            {
                foreach (var table in tables)
                {
                    var routeData = ParseRouteTable(table, baseRoute);
                    if (routeData != null)
                    {
                        routes.Add(routeData);
                    }
                }
            }

            // Если не удалось распарсить таблицы, возвращаем базовый маршрут
            if (!routes.Any())
            {
                routes.Add(baseRoute);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка парсинга HTML маршрута");
        }

        return routes;
    }

    /// <summary>
    /// Парсит таблицу маршрута и извлекает данные
    /// Детальный парсинг полей Route (аналог Python логики)
    /// </summary>
    private Route? ParseRouteTable(HtmlNode table, Route baseRoute)
    {
        try
        {
            var route = new Route
            {
                RouteNumber = baseRoute.RouteNumber,
                RouteDate = baseRoute.RouteDate,
                TripDate = baseRoute.TripDate,
                DriverTab = baseRoute.DriverTab,
                RouteKey = baseRoute.RouteKey,
                CreatedAt = baseRoute.CreatedAt
            };

            // Парсим ячейки таблицы и извлекаем данные
            var cells = table.SelectNodes(".//td");
            if (cells == null) return null;

            var cellTexts = cells.Select(cell => _textNormalizer.NormalizeText(cell.InnerText)).ToList();
            
            // Извлекаем данные по паттернам (как в Python с использованием регулярных выражений)
            ExtractRouteFields(route, cellTexts);
            
            return route;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка парсинга таблицы маршрута");
            return null;
        }
    }

    /// <summary>
    /// Извлекает поля маршрута из текстовых данных ячеек
    /// Реализация детальной логики парсинга (как в Python)
    /// </summary>
    private void ExtractRouteFields(Route route, List<string> cellTexts)
    {
        for (int i = 0; i < cellTexts.Count; i++)
        {
            var text = cellTexts[i].ToLower();
            
            // Серия и номер локомотива
            if (text.Contains("серия") && i + 1 < cellTexts.Count)
            {
                route.LocomotiveSeries = cellTexts[i + 1];
            }
            if (text.Contains("номер лок") && i + 1 < cellTexts.Count)
            {
                route.LocomotiveNumber = _textNormalizer.SafeInt(cellTexts[i + 1]);
            }
            
            // Веса и нагрузки
            if (text.Contains("нетто") && i + 1 < cellTexts.Count)
            {
                route.NettoTons = _textNormalizer.SafeDecimal(cellTexts[i + 1]);
            }
            if (text.Contains("брутто") && i + 1 < cellTexts.Count)
            {
                route.BruttoTons = _textNormalizer.SafeDecimal(cellTexts[i + 1]);
            }
            if (text.Contains("оси") && i + 1 < cellTexts.Count)
            {
                route.AxesCount = _textNormalizer.SafeInt(cellTexts[i + 1]);
            }
            
            // Участок и норма
            if (text.Contains("участок") && i + 1 < cellTexts.Count)
            {
                route.SectionName = cellTexts[i + 1];
            }
            if (text.Contains("номер нормы") && i + 1 < cellTexts.Count)
            {
                route.NormNumber = cellTexts[i + 1];
            }
            
            // Расходы
            if (text.Contains("расход факт") && i + 1 < cellTexts.Count)
            {
                route.FactConsumption = _textNormalizer.SafeDecimal(cellTexts[i + 1]);
            }
            if (text.Contains("расход по норме") && i + 1 < cellTexts.Count)
            {
                route.NormConsumption = _textNormalizer.SafeDecimal(cellTexts[i + 1]);
            }
            
            // Километраж и работа
            if (text.Contains("км") && i + 1 < cellTexts.Count && !text.Contains("ткм"))
            {
                route.Kilometers = _textNormalizer.SafeDecimal(cellTexts[i + 1]);
            }
            if (text.Contains("ткм") && i + 1 < cellTexts.Count)
            {
                route.TonKilometers = _textNormalizer.SafeDecimal(cellTexts[i + 1]);
            }
        }
        
        // Вычисляем производные поля (как в Python)
        CalculateDerivedFields(route);
    }

    /// <summary>
    /// Вычисляет производные поля маршрута
    /// Аналог вычислений в Python parse_html_route
    /// </summary>
    private void CalculateDerivedFields(Route route)
    {
        // Нагрузка на ось
        if (route.BruttoTons.HasValue && route.AxesCount.HasValue && route.AxesCount > 0)
        {
            route.AxleLoad = route.BruttoTons.Value / route.AxesCount.Value;
        }
        
        // Удельный расход
        if (route.FactConsumption.HasValue && route.TonKilometers.HasValue && route.TonKilometers > 0)
        {
            route.FactUd = route.FactConsumption.Value / route.TonKilometers.Value * 10000;
        }
        
        // Отклонение в процентах
        if (route.FactConsumption.HasValue && route.NormConsumption.HasValue && route.NormConsumption > 0)
        {
            var deviation = (route.FactConsumption.Value - route.NormConsumption.Value) / route.NormConsumption.Value * 100;
            route.DeviationPercent = Math.Round(deviation, 2);
            
            // Определяем статус отклонения (как в Python StatusClassifier)
            route.Status = DeviationStatus.GetStatus(deviation);
        }
    }

    /// <summary>
    /// Обрабатывает дубликаты маршрутов
    /// Соответствует логике группировки в Python по route_key
    /// </summary>
    private async Task<List<Route>> ProcessDuplicatesAsync(List<Route> routes)
    {
        _logger.LogDebug("Обрабатываем дубликаты маршрутов");
        
        var groupedRoutes = routes
            .Where(r => !string.IsNullOrEmpty(r.RouteKey))
            .GroupBy(r => r.RouteKey)
            .ToList();

        var processedRoutes = new List<Route>();
        var duplicateCount = 0;

        foreach (var group in groupedRoutes)
        {
            var routeGroup = group.ToList();
            
            if (routeGroup.Count > 1)
            {
                duplicateCount += routeGroup.Count - 1;
                _logger.LogTrace("Найдены дубликаты для маршрута {RouteKey}: {Count} версий", 
                    group.Key, routeGroup.Count);
                
                // Выбираем лучший маршрут из группы (аналог select_best_route из Python)
                var bestRoute = SelectBestRoute(routeGroup);
                if (bestRoute != null)
                {
                    bestRoute.DuplicatesCount = (routeGroup.Count - 1).ToString();
                    processedRoutes.Add(bestRoute);
                }
            }
            else
            {
                processedRoutes.Add(routeGroup.First());
            }
        }

        _stats.DuplicateRoutes = duplicateCount;
        _logger.LogDebug("Обработка дубликатов завершена: {DuplicateCount} дубликатов, {UniqueCount} уникальных маршрутов", 
            duplicateCount, processedRoutes.Count);

        return processedRoutes;
    }

    /// <summary>
    /// Выбирает лучший маршрут из группы дубликатов
    /// Соответствует select_best_route из Python
    /// </summary>
    private Route? SelectBestRoute(List<Route> routes)
    {
        if (!routes.Any()) return null;
        if (routes.Count == 1) return routes.First();

        // Приоритеты выбора (как в Python):
        // 1. Маршрут с наиболее полными данными
        // 2. Маршрут с корректными расчетами
        // 3. Самый свежий маршрут
        
        return routes
            .OrderByDescending(r => GetRouteCompleteness(r))  // Полнота данных
            .ThenByDescending(r => r.CreatedAt)               // Свежесть
            .FirstOrDefault();
    }

    /// <summary>
    /// Вычисляет полноту данных маршрута для выбора лучшего
    /// </summary>
    private int GetRouteCompleteness(Route route)
    {
        var score = 0;
        
        if (!string.IsNullOrEmpty(route.LocomotiveSeries)) score++;
        if (route.LocomotiveNumber.HasValue && route.LocomotiveNumber > 0) score++;
        if (route.NettoTons.HasValue && route.NettoTons > 0) score++;
        if (route.BruttoTons.HasValue && route.BruttoTons > 0) score++;
        if (!string.IsNullOrEmpty(route.SectionName)) score++;
        if (!string.IsNullOrEmpty(route.NormNumber)) score++;
        if (route.FactConsumption.HasValue && route.FactConsumption > 0) score++;
        if (route.NormConsumption.HasValue && route.NormConsumption > 0) score++;
        if (route.DeviationPercent.HasValue) score++;
        
        return score;
    }

    public ProcessingStatistics GetProcessingStatistics()
    {
        return _stats;
    }
}

/// <summary>
/// Метаданные маршрута для промежуточного хранения
/// </summary>
public record RouteMetadata
{
    public string? Number { get; set; }
    public string? RouteDate { get; set; }
    public string? TripDate { get; set; }
    public string? DriverTab { get; set; }
    public string? Identifier { get; set; }
}