// Services/Implementation/EnhancedHtmlParser.cs (ОБНОВЛЕННЫЙ для CHAT 2)
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using HtmlAgilityPack;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Models.Domain;
using AnalysisNorm.Infrastructure.Logging;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// ОБНОВЛЕННАЯ версия EnhancedHtmlParser для CHAT 2
/// Добавляет: детальную обработку таблиц, async file processing, продвинутую дедупликацию
/// Точное соответствие Python HTMLRouteProcessor + HTMLNormProcessor функциональности
/// </summary>
public class EnhancedHtmlParser : IHtmlParser
{
    private readonly IApplicationLogger _logger;
    private readonly IPerformanceMonitor _performanceMonitor;
    
    // НОВОЕ для CHAT 2: Кэширование для производительности
    private readonly ConcurrentDictionary<string, ProcessingResult<IEnumerable<Route>>> _routeCache = new();
    private readonly ConcurrentDictionary<string, ProcessingResult<IEnumerable<Norm>>> _normCache = new();

    // ОБНОВЛЕННЫЕ Regex patterns из Python html_route_processor.py (ДЕТАЛИЗИРОВАННЫЕ)
    private static readonly Regex RouteNumberPattern = new(@"№\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex DatePattern = new(@"(\d{1,2})[./](\d{1,2})[./](\d{4})", RegexOptions.Compiled);
    private static readonly Regex DepotPattern = new(@"ТЧЭ[^а-яё]*([а-яё\s\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LocomotivePattern = new(@"(?:ЭП20|2ЭС5К|ЭС5К|ВЛ80С)\s*-?\s*(\d+)", RegexOptions.Compiled);
    
    // НОВЫЕ для CHAT 2: Patterns для сложной обработки таблиц
    private static readonly Regex RouteTableStartPattern = new(
        @"<table[^>]*><tr><th\s+class=['""]?thl_common['""]?><font\s+class=['""]?filter_key['""]?>\s*Маршрут\s*№:",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex TableDataPattern = new(
        @"<td[^>]*>(.*?)</td>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    
    // НОВЫЕ для CHAT 2: Patterns для извлечения норм (из html_norm_processor.py)
    private static readonly Regex NormTablePattern = new(
        @"<font class=rcp12><center><b>Удельные нормы электроэнергии и топлива по (нагрузке на ось|весу поезда)</b></center></font>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ОБНОВЛЕННЫЕ Cleanup patterns с дополнительными из Python
    private static readonly Regex[] CleanupPatterns = {
        new(@"<script[^>]*>.*?</script>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new(@"<style[^>]*>.*?</style>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline),
        new(@"<meta[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"<link[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"&nbsp;", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\s+", RegexOptions.Compiled) // Множественные пробелы в один
    };

    public EnhancedHtmlParser(IApplicationLogger logger, IPerformanceMonitor? performanceMonitor = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceMonitor = performanceMonitor ?? new DummyPerformanceMonitor();
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод парсинга маршрутов - добавляет детальную обработку таблиц
    /// </summary>
    public async Task<ProcessingResult<IEnumerable<Route>>> ParseRoutesAsync(Stream htmlContent, object? options = null)
    {
        var operationId = Guid.NewGuid().ToString("N")[..8];
        _performanceMonitor.StartOperation($"ParseRoutes_{operationId}");

        try
        {
            // НОВОЕ: Кэширование для производительности
            var contentHash = await ComputeContentHashAsync(htmlContent);
            if (_routeCache.TryGetValue(contentHash, out var cachedResult))
            {
                _logger.LogDebug("Использован кэшированный результат парсинга маршрутов");
                return cachedResult;
            }

            var html = await ReadHtmlContentAsync(htmlContent);
            
            // НОВЫЕ этапы обработки из Python html_route_processor.py
            var processedHtml = await ProcessHtmlForRoutesAsync(html);
            var routes = await ExtractRoutesFromProcessedHtmlAsync(processedHtml);
            var deduplicatedRoutes = await ProcessAdvancedDeduplicationAsync(routes);

            var result = ProcessingResult<IEnumerable<Route>>.Success(deduplicatedRoutes);
            _routeCache.TryAdd(contentHash, result);

            _logger.LogInformation("Парсинг HTML завершен. Извлечено маршрутов: {Count}, после дедупликации: {Deduplicated}", 
                routes.Count, deduplicatedRoutes.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при парсинге HTML маршрутов");
            return ProcessingResult<IEnumerable<Route>>.Failure($"Ошибка парсинга HTML: {ex.Message}");
        }
        finally
        {
            _performanceMonitor.EndOperation($"ParseRoutes_{operationId}");
        }
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод парсинга норм - реализует логику из Python html_norm_processor.py
    /// </summary>
    public async Task<ProcessingResult<IEnumerable<Norm>>> ParseNormsAsync(Stream htmlContent, object? options = null)
    {
        var operationId = Guid.NewGuid().ToString("N")[..8];
        _performanceMonitor.StartOperation($"ParseNorms_{operationId}");

        try
        {
            var contentHash = await ComputeContentHashAsync(htmlContent);
            if (_normCache.TryGetValue(contentHash, out var cachedResult))
            {
                return cachedResult;
            }

            var html = await ReadHtmlContentAsync(htmlContent);
            
            // НОВАЯ детальная обработка норм из Python
            var cleanedHtml = await CleanHtmlForNormsAsync(html);
            var norms = await ExtractNormsFromCleanedHtmlAsync(cleanedHtml);

            var result = ProcessingResult<IEnumerable<Norm>>.Success(norms);
            _normCache.TryAdd(contentHash, result);

            _logger.LogInformation("Парсинг норм завершен. Извлечено норм: {Count}", norms.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при парсинге HTML норм");
            return ProcessingResult<IEnumerable<Norm>>.Failure($"Ошибка парсинга норм: {ex.Message}");
        }
        finally
        {
            _performanceMonitor.EndOperation($"ParseNorms_{operationId}");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Обработка HTML для маршрутов (из Python _split_routes_to_lines + _remove_vcht_routes + _clean_html_content)
    /// </summary>
    private async Task<string> ProcessHtmlForRoutesAsync(string html)
    {
        await Task.Yield();
        
        _logger.LogDebug("Начата многоэтапная обработка HTML для маршрутов");

        // Этап 1: Разбиение маршрутов на строки (из Python _split_routes_to_lines)
        var splitHtml = SplitRoutesToLines(html);
        
        // Этап 2: Удаление ВЧТЭ маршрутов (из Python _remove_vcht_routes)
        var filteredHtml = RemoveVchtRoutes(splitHtml);
        
        // Этап 3: Очистка HTML (из Python _clean_html_content)
        var cleanedHtml = CleanHtmlContent(filteredHtml);

        _logger.LogDebug("Обработка HTML завершена. Исходный размер: {Original}, финальный: {Final}", 
            html.Length, cleanedHtml.Length);

        return cleanedHtml;
    }

    /// <summary>
    /// НОВЫЙ метод: Разбивка маршрутов на строки - точная копия Python _split_routes_to_lines
    /// </summary>
    private string SplitRoutesToLines(string content)
    {
        const string routePattern = @"(<table[^>]*><tr><th class=['""]?thl_common['""]?><font class=['""]?filter_key['""]?>\s*Маршрут\s*№:)";
        var processed = Regex.Replace(content, routePattern, "\n$1", RegexOptions.IgnoreCase);
        
        _logger.LogDebug("Маршруты разбиты на отдельные строки");
        return processed;
    }

    /// <summary>
    /// НОВЫЙ метод: Удаление ВЧТЭ маршрутов - точная копия Python _remove_vcht_routes
    /// </summary>
    private string RemoveVchtRoutes(string content)
    {
        const string vchtPattern = @"ТЧЭ[^а-яё]*ВЧТЭ";
        var vchtRegex = new Regex(vchtPattern, RegexOptions.IgnoreCase);
        
        var lines = content.Split('\n');
        var filteredLines = new List<string>();
        
        int removedCount = 0;
        foreach (var line in lines)
        {
            if (!vchtRegex.IsMatch(line))
            {
                filteredLines.Add(line);
            }
            else
            {
                removedCount++;
            }
        }
        
        _logger.LogDebug("Исключено ВЧТЭ маршрутов: {Count}", removedCount);
        return string.Join('\n', filteredLines);
    }

    /// <summary>
    /// НОВЫЙ метод: Извлечение маршрутов из обработанного HTML - детальная логика таблиц
    /// </summary>
    private async Task<List<Route>> ExtractRoutesFromProcessedHtmlAsync(string processedHtml)
    {
        await Task.Yield();
        
        var routes = new List<Route>();
        var doc = new HtmlDocument();
        doc.LoadHtml(processedHtml);

        // Ищем таблицы с маршрутами по характерным признакам
        var routeTables = FindRouteTablesAdvanced(doc);
        
        foreach (var table in routeTables)
        {
            var extractedRoutes = await ExtractRoutesFromTableAdvancedAsync(table);
            routes.AddRange(extractedRoutes);
        }

        _logger.LogInformation("Извлечено маршрутов из {Tables} таблиц: {Routes}", routeTables.Count, routes.Count);
        return routes;
    }

    /// <summary>
    /// НОВЫЙ метод: Продвинутый поиск таблиц маршрутов
    /// </summary>
    private List<HtmlNode> FindRouteTablesAdvanced(HtmlDocument doc)
    {
        var routeTables = new List<HtmlNode>();
        
        // Ищем по нескольким критериям
        var tables = doc.DocumentNode.SelectNodes("//table") ?? new HtmlNodeCollection(null);

        foreach (var table in tables)
        {
            // Критерий 1: заголовок "Маршрут №"
            var routeHeaders = table.SelectNodes(".//th[contains(@class, 'thl_common')]//font[contains(@class, 'filter_key')]");
            if (routeHeaders?.Any(h => h.InnerText.Contains("Маршрут")) == true)
            {
                routeTables.Add(table);
                continue;
            }

            // Критерий 2: текст содержит номер маршрута
            if (table.InnerText.Contains("Маршрут №") || table.InnerText.Contains("№"))
            {
                routeTables.Add(table);
            }
        }

        _logger.LogDebug("Найдено таблиц маршрутов: {Count}", routeTables.Count);
        return routeTables;
    }

    /// <summary>
    /// НОВЫЙ метод: Продвинутое извлечение маршрутов из таблицы
    /// </summary>
    private async Task<List<Route>> ExtractRoutesFromTableAdvancedAsync(HtmlNode table)
    {
        await Task.Yield();
        
        var routes = new List<Route>();
        var rows = table.SelectNodes(".//tr") ?? new HtmlNodeCollection(null);

        Route? currentRoute = null;
        var currentSections = new List<Section>();

        foreach (var row in rows.Skip(1)) // Пропускаем заголовок
        {
            var cells = row.SelectNodes(".//td") ?? new HtmlNodeCollection(null);
            if (cells.Count < 3) continue;

            // Попытка извлечь информацию о новом маршруте
            var routeInfo = TryExtractRouteInfoAdvanced(cells);
            if (routeInfo != null)
            {
                // Сохраняем предыдущий маршрут
                if (currentRoute != null && currentSections.Any())
                {
                    var completeRoute = currentRoute with { Sections = currentSections.AsReadOnly() };
                    routes.Add(completeRoute);
                }

                currentRoute = routeInfo;
                currentSections = new List<Section>();
            }

            // Извлечение участка
            var section = TryExtractSectionAdvanced(cells, currentRoute);
            if (section != null && currentRoute != null)
            {
                currentSections.Add(section);
            }
        }

        // Добавляем последний маршрут
        if (currentRoute != null && currentSections.Any())
        {
            var completeRoute = currentRoute with { Sections = currentSections.AsReadOnly() };
            routes.Add(completeRoute);
        }

        return routes;
    }

    /// <summary>
    /// НОВЫЙ метод: Продвинутая дедупликация маршрутов - сложная логика из Python
    /// </summary>
    private async Task<List<Route>> ProcessAdvancedDeduplicationAsync(List<Route> routes)
    {
        await Task.Yield();
        
        _logger.LogDebug("Начата продвинутая дедупликация {Count} маршрутов", routes.Count);

        var deduplicatedRoutes = new List<Route>();
        
        // Группируем по ключу маршрута (номер + дата + депо)
        var routeGroups = routes
            .GroupBy(r => $"{r.Number}_{r.Date:yyyyMMdd}_{r.Depot}")
            .ToList();

        foreach (var group in routeGroups)
        {
            if (group.Count() == 1)
            {
                deduplicatedRoutes.Add(group.First());
            }
            else
            {
                // Сложная логика объединения дубликатов
                var mergedRoute = await MergeDuplicateRoutesAdvancedAsync(group.ToList());
                deduplicatedRoutes.Add(mergedRoute);
            }
        }

        _logger.LogInformation("Дедупликация завершена: {Original} -> {Deduplicated} маршрутов", 
            routes.Count, deduplicatedRoutes.Count);

        return deduplicatedRoutes;
    }

    /// <summary>
    /// НОВЫЙ метод: Продвинутое объединение дублирующихся маршрутов
    /// </summary>
    private async Task<Route> MergeDuplicateRoutesAdvancedAsync(List<Route> duplicateRoutes)
    {
        await Task.Yield();
        
        // Выбираем маршрут с наибольшим количеством участков как основу
        var baseRoute = duplicateRoutes.OrderByDescending(r => r.Sections.Count).First();
        
        // Объединяем участки с приоритетом по качеству данных
        var mergedSections = new Dictionary<string, Section>();

        foreach (var route in duplicateRoutes)
        {
            foreach (var section in route.Sections)
            {
                var key = section.Name.ToLowerInvariant().Trim();
                
                if (!mergedSections.ContainsKey(key))
                {
                    mergedSections[key] = section;
                }
                else
                {
                    // Выбираем участок с более полными данными
                    var existing = mergedSections[key];
                    if (IsMoreCompleteSection(section, existing))
                    {
                        mergedSections[key] = section;
                    }
                }
            }
        }

        var finalSections = mergedSections.Values.OrderBy(s => s.Name).ToList();
        
        _logger.LogDebug("Объединен маршрут {Route}: {Original} -> {Merged} участков", 
            baseRoute.Number, duplicateRoutes.Sum(r => r.Sections.Count), finalSections.Count);

        return baseRoute with { Sections = finalSections.AsReadOnly() };
    }

    /// <summary>
    /// НОВЫЙ метод: Определение более полного участка
    /// </summary>
    private bool IsMoreCompleteSection(Section candidate, Section existing)
    {
        // Критерии полноты данных
        int candidateScore = 0;
        int existingScore = 0;

        if (candidate.ActualConsumption > 0) candidateScore++;
        if (candidate.TkmBrutto > 0) candidateScore++;
        if (candidate.Distance > 0) candidateScore++;
        if (!string.IsNullOrEmpty(candidate.NormId)) candidateScore++;

        if (existing.ActualConsumption > 0) existingScore++;
        if (existing.TkmBrutto > 0) existingScore++;
        if (existing.Distance > 0) existingScore++;
        if (!string.IsNullOrEmpty(existing.NormId)) existingScore++;

        return candidateScore > existingScore;
    }

    /// <summary>
    /// НОВЫЙ метод: Очистка HTML для норм - из Python html_norm_processor.py
    /// </summary>
    private async Task<string> CleanHtmlForNormsAsync(string html)
    {
        await Task.Yield();
        
        // Ищем специфичные секции с нормами (из Python _clean_html_content)
        const string pattern1 = @"(<font class=rcp12><center><b>Удельные нормы электроэнергии и топлива по нагрузке на ось</b></center></font>.*?</table>.*?</table>)";
        const string pattern2 = @"(<font class=rcp12><center><b>Удельные нормы электроэнергии и топлива по весу поезда</b></center></font>.*?</table>.*?</table>)";

        var match1 = Regex.Match(html, pattern1, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var match2 = Regex.Match(html, pattern2, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var cleanedParts = new List<string>
        {
            "<!DOCTYPE html>",
            "<html><head><meta charset=\"utf-8\"><title>Нормы</title></head><body>"
        };

        if (match1.Success)
        {
            cleanedParts.Add(match1.Groups[1].Value);
            _logger.LogDebug("Найдена таблица норм по нагрузке на ось");
        }

        if (match2.Success)
        {
            cleanedParts.Add(match2.Groups[1].Value);
            _logger.LogDebug("Найдена таблица норм по весу поезда");
        }

        if (!match1.Success && !match2.Success)
        {
            _logger.LogWarning("Искомые таблицы норм не найдены в файле");
            cleanedParts.Add("<h1>Искомые таблицы норм не найдены</h1>");
        }

        cleanedParts.Add("</body></html>");
        return string.Join("\n", cleanedParts);
    }

    /// <summary>
    /// НОВЫЙ метод: Извлечение норм из очищенного HTML - из Python _extract_norms_from_cleaned_html
    /// </summary>
    private async Task<List<Norm>> ExtractNormsFromCleanedHtmlAsync(string cleanedHtml)
    {
        await Task.Yield();
        
        var norms = new List<Norm>();
        var doc = new HtmlDocument();
        doc.LoadHtml(cleanedHtml);

        // Обработка норм по нагрузке на ось
        var loadNorms = ExtractNormsFromSectionByText(doc, "нагрузке на ось", "Нажатие");
        norms.AddRange(loadNorms);

        // Обработка норм по весу поезда
        var weightNorms = ExtractNormsFromSectionByText(doc, "весу поезда", "Вес");
        norms.AddRange(weightNorms);

        _logger.LogDebug("Извлечено норм: {Count} (по нагрузке: {Load}, по весу: {Weight})", 
            norms.Count, loadNorms.Count, weightNorms.Count);

        return norms;
    }

    /// <summary>
    /// НОВЫЙ метод: Извлечение норм из секции по тексту
    /// </summary>
    private List<Norm> ExtractNormsFromSectionByText(HtmlDocument doc, string searchText, string normType)
    {
        var norms = new List<Norm>();
        
        // Ищем элементы, содержащие искомый текст
        var textNodes = doc.DocumentNode.Descendants()
            .Where(n => n.InnerText.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var textNode in textNodes)
        {
            // Ищем ближайшую таблицу после найденного текста
            var table = FindNextTableWithHeaders(textNode);
            if (table != null)
            {
                var tableNorms = ExtractNormsFromTable(table, normType);
                norms.AddRange(tableNorms);
            }
        }

        return norms;
    }

    /// <summary>
    /// НОВЫЙ метод: Поиск следующей таблицы с заголовками
    /// </summary>
    private HtmlNode? FindNextTableWithHeaders(HtmlNode startNode)
    {
        var current = startNode;
        while (current != null)
        {
            var table = current.SelectSingleNode(".//table");
            if (table != null)
            {
                var headerRow = table.SelectSingleNode(".//tr[@class='tr_head']");
                if (headerRow != null)
                {
                    return table;
                }
            }
            current = current.NextSibling;
        }
        return null;
    }

    /// <summary>
    /// НОВЫЙ метод: Извлечение норм из таблицы
    /// </summary>
    private List<Norm> ExtractNormsFromTable(HtmlNode table, string normType)
    {
        var norms = new List<Norm>();
        
        var headers = GetTableHeaders(table);
        var dataRows = table.SelectNodes(".//tr")?.Skip(1).ToList() ?? new List<HtmlNode>();

        foreach (var row in dataRows)
        {
            var cells = row.SelectNodes(".//td") ?? new HtmlNodeCollection(null);
            if (cells.Count > 10)
            {
                var norm = ParseNormFromRow(cells, headers, normType);
                if (norm != null)
                {
                    norms.Add(norm);
                }
            }
        }

        return norms;
    }

    /// <summary>
    /// НОВЫЙ метод: Получение заголовков таблицы
    /// </summary>
    private List<string> GetTableHeaders(HtmlNode table)
    {
        var headerRow = table.SelectSingleNode(".//tr[@class='tr_head']");
        if (headerRow == null) return new List<string>();

        var headerCells = headerRow.SelectNodes(".//th") ?? new HtmlNodeCollection(null);
        return headerCells.Select(cell => NormalizeText(cell.InnerText)).ToList();
    }

    /// <summary>
    /// НОВЫЙ метод: Парсинг нормы из строки таблицы
    /// </summary>
    private Norm? ParseNormFromRow(HtmlNodeCollection cells, List<string> headers, string normType)
    {
        try
        {
            // Создаем ID нормы из первых колонок
            var normId = $"{normType}_{cells[0].InnerText.Trim()}_{DateTime.Now.Ticks}";
            
            // Извлекаем числовые данные (колонки 9 до len-2 как в Python)
            var dataPoints = new List<DataPoint>();
            int numericStart = 9;
            int numericEnd = Math.Min(headers.Count - 2, cells.Count);

            for (int i = numericStart; i < numericEnd; i++)
            {
                var xValue = ExtractNumericValue(headers[i]);
                var yValue = ParseDecimalSafe(cells[i].InnerText);
                
                if (xValue > 0 && yValue > 0)
                {
                    dataPoints.Add(new DataPoint(xValue, yValue));
                }
            }

            if (dataPoints.Any())
            {
                return new Norm(
                    normId,
                    normType,
                    dataPoints.AsReadOnly(),
                    new NormMetadata(
                        Description: $"Норма {normType}",
                        CreatedDate: DateTime.UtcNow,
                        Source: "HTML Import"
                    )
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Ошибка парсинга нормы: {Error}", ex.Message);
        }

        return null;
    }

    /// <summary>
    /// НОВЫЕ вспомогательные методы
    /// </summary>
    private Route? TryExtractRouteInfoAdvanced(HtmlNodeCollection cells)
    {
        try
        {
            // Более детальный парсинг информации о маршруте
            var firstCellText = NormalizeText(cells[0].InnerText);
            
            // Извлекаем номер маршрута
            var numberMatch = RouteNumberPattern.Match(firstCellText);
            if (!numberMatch.Success) return null;
            
            var routeNumber = numberMatch.Groups[1].Value;
            
            // Извлекаем дату из текста ячейки или соседних ячеек
            var dateMatch = DatePattern.Match(firstCellText);
            if (!dateMatch.Success && cells.Count > 1)
            {
                dateMatch = DatePattern.Match(NormalizeText(cells[1].InnerText));
            }
            
            DateTime routeDate = DateTime.Today;
            if (dateMatch.Success)
            {
                if (DateTime.TryParse($"{dateMatch.Groups[1].Value}.{dateMatch.Groups[2].Value}.{dateMatch.Groups[3].Value}", out var parsedDate))
                {
                    routeDate = parsedDate;
                }
            }

            // Извлекаем депо
            var depotMatch = DepotPattern.Match(firstCellText);
            var depot = depotMatch.Success ? depotMatch.Groups[1].Value.Trim() : "Неизвестно";

            // Извлекаем локомотив
            var locoMatch = LocomotivePattern.Match(firstCellText);
            var locomotive = locoMatch.Success ? 
                new Locomotive(locoMatch.Groups[0].Value, locoMatch.Groups[1].Value) :
                new Locomotive("Неизвестно", "0");

            return new Route(
                routeNumber,
                routeDate,
                depot,
                locomotive,
                new List<Section>().AsReadOnly(),
                new RouteMetadata()
            );
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Ошибка извлечения информации маршрута: {Error}", ex.Message);
            return null;
        }
    }

    private Section? TryExtractSectionAdvanced(HtmlNodeCollection cells, Route? currentRoute)
    {
        if (cells.Count < 4) return null;

        try
        {
            var name = NormalizeText(cells[0].InnerText);
            if (string.IsNullOrWhiteSpace(name) || IsRouteSeparator(name)) return null;

            // Продвинутое извлечение данных участка
            var tkmBrutto = ParseDecimalSafe(cells.Count > 1 ? cells[1].InnerText : "0");
            var distance = ParseDecimalSafe(cells.Count > 2 ? cells[2].InnerText : "0");
            var actualConsumption = ParseDecimalSafe(cells.Count > 3 ? cells[3].InnerText : "0");

            // Попытка извлечь дополнительные данные если доступны
            var normConsumption = cells.Count > 4 ? ParseDecimalSafe(cells[4].InnerText) : 0;
            var normId = cells.Count > 5 ? NormalizeText(cells[5].InnerText) : null;

            return new Section(
                name,
                tkmBrutto,
                distance,
                actualConsumption,
                normConsumption,
                normId,
                new SectionMetadata()
            );
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Ошибка создания участка: {Error}", ex.Message);
            return null;
        }
    }

    private decimal ExtractNumericValue(string text)
    {
        var matches = Regex.Matches(text, @"\d+(?:[.,]\d+)?");
        if (matches.Any())
        {
            return ParseDecimalSafe(matches[0].Value);
        }
        return 0;
    }

    private async Task<string> ComputeContentHashAsync(Stream content)
    {
        content.Position = 0;
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(content);
        content.Position = 0;
        return Convert.ToHexString(hashBytes)[..16];
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод чтения HTML с кодировками - точная копия Python
    /// </summary>
    private async Task<string> ReadHtmlContentAsync(Stream htmlContent)
    {
        htmlContent.Position = 0;
        
        // Пробуем кодировки в том же порядке что и Python
        var encodings = new[] { 
            Encoding.GetEncoding("cp1251"), 
            Encoding.UTF8, 
            Encoding.GetEncoding("windows-1251") 
        };
        
        foreach (var encoding in encodings)
        {
            try
            {
                htmlContent.Position = 0;
                using var reader = new StreamReader(htmlContent, encoding, true, leaveOpen: true);
                var content = await reader.ReadToEndAsync();
                
                // Валидация кодировки через русские символы
                if (content.Any(c => c >= 'а' && c <= 'я' || c >= 'А' && c <= 'Я'))
                {
                    _logger.LogDebug("HTML прочитан с кодировкой {Encoding}, размер: {Size} символов", 
                        encoding.EncodingName, content.Length);
                    return content;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Неудачная попытка чтения с {Encoding}: {Error}", encoding.EncodingName, ex.Message);
            }
        }

        // Fallback к UTF-8
        htmlContent.Position = 0;
        using var fallbackReader = new StreamReader(htmlContent, Encoding.UTF8);
        var fallbackContent = await fallbackReader.ReadToEndAsync();
        _logger.LogWarning("Использован fallback UTF-8 для чтения HTML");
        return fallbackContent;
    }

    /// <summary>
    /// ОБНОВЛЕННЫЙ метод очистки HTML - детализированный из Python
    /// </summary>
    private string CleanHtmlContent(string html)
    {
        string cleaned = html;
        
        foreach (var pattern in CleanupPatterns)
        {
            cleaned = pattern.Replace(cleaned, " ");
        }

        // Нормализация пробелов как в Python
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        cleaned = cleaned.Replace("&nbsp;", " ");
        
        _logger.LogDebug("HTML очищен от лишних элементов");
        return cleaned;
    }

    /// <summary>
    /// Вспомогательные методы для обработки текста
    /// </summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        
        return text.Trim()
                  .Replace("&nbsp;", " ")
                  .Replace("\u00A0", " ")
                  .Replace("\t", " ")
                  .Replace("\n", " ")
                  .Replace("\r", " ")
                  .Replace("  ", " ");
    }

    private static decimal ParseDecimalSafe(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        
        var normalized = text.Replace(",", ".")
                            .Replace(" ", "")
                            .Trim();
        
        return decimal.TryParse(normalized, out var result) ? result : 0;
    }

    private static bool IsRouteSeparator(string text)
    {
        var separators = new[] { "итого", "total", "всего", "сумма" };
        return separators.Any(sep => text.ToLowerInvariant().Contains(sep));
    }
}

/// <summary>
/// Dummy Performance Monitor для случаев когда IPerformanceMonitor не доступен
/// </summary>
internal class DummyPerformanceMonitor : IPerformanceMonitor
{
    public void StartOperation(string operationName) { }
    public void EndOperation(string operationName) { }
    public void LogMemoryUsage() { }
    public object GetCurrentMetrics() => new { };
}