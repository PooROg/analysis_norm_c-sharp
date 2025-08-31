using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// ИСПРАВЛЕНО: Полная реконструкция сервиса обработки HTML файлов маршрутов
/// Устранены структурные проблемы, восстановлена целостность класса
/// </summary>
public class HtmlRouteProcessorService : IHtmlRouteProcessorService
{
    #region Fields

    private readonly ILogger<HtmlRouteProcessorService> _logger;
    private readonly IFileEncodingDetector _encodingDetector;
    private readonly ITextNormalizer _textNormalizer;

    // ИСПРАВЛЕНО: Используем Core.Entities.ProcessingStatistics для устранения неоднозначности
    private readonly AnalysisNorm.Core.Entities.ProcessingStatistics _statistics =
        AnalysisNorm.Core.Entities.ProcessingStatistics.StartNew();

    // Регулярные выражения для извлечения данных
    private static readonly Regex RouteNameRegex = new(@"Маршрут:\s*([^\n\r]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DateRegex = new(@"Дата:\s*(\d{1,2}[\.\/\-]\d{1,2}[\.\/\-]\d{2,4})", RegexOptions.Compiled);
    private static readonly Regex LocomotiveRegex = new(@"([А-Я]{1,3})[-\s]*(\d+)", RegexOptions.Compiled);
    private static readonly Regex DistanceRegex = new(@"Расстояние:\s*([0-9,\.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MassRegex = new(@"Масса:\s*([0-9,\.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ConsumptionRegex = new(@"Расход:\s*([0-9,\.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    #endregion

    #region Constructor

    public HtmlRouteProcessorService(
        ILogger<HtmlRouteProcessorService> logger,
        IFileEncodingDetector encodingDetector,
        ITextNormalizer textNormalizer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _encodingDetector = encodingDetector ?? throw new ArgumentNullException(nameof(encodingDetector));
        _textNormalizer = textNormalizer ?? throw new ArgumentNullException(nameof(textNormalizer));
    }

    #endregion

    #region IHtmlRouteProcessorService Implementation - ИСПРАВЛЕНО

    /// <summary>
    /// ИСПРАВЛЕНО: Реализация обработки одного HTML файла
    /// </summary>
    public async Task<ProcessingResult<IEnumerable<Route>>> ProcessHtmlFileAsync(
        string htmlFile, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Начало обработки HTML файла: {FilePath}", htmlFile);

            if (!File.Exists(htmlFile))
            {
                var error = $"Файл не найден: {htmlFile}";
                _statistics.AddError(error);
                return ProcessingResult<IEnumerable<Route>>.Failure(error, _statistics);
            }

            // Читаем содержимое файла с автоматической детекцией кодировки
            var htmlContent = await _encodingDetector.ReadTextWithEncodingDetectionAsync(htmlFile);

            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                var error = $"Файл пустой или не может быть прочитан: {htmlFile}";
                _statistics.AddError(error);
                return ProcessingResult<IEnumerable<Route>>.Failure(error, _statistics);
            }

            // Парсим HTML
            var routes = await ParseHtmlContentAsync(htmlContent, htmlFile, cancellationToken);

            _statistics.ProcessedFiles++;
            _statistics.TotalFiles++;

            _logger.LogInformation("Обработка файла завершена. Найдено маршрутов: {Count}", routes.Count);

            return ProcessingResult<IEnumerable<Route>>.Success(routes, _statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке HTML файла: {FilePath}", htmlFile);
            _statistics.ErrorFiles++;
            _statistics.AddError($"Ошибка в файле {htmlFile}: {ex.Message}");
            return ProcessingResult<IEnumerable<Route>>.Failure(ex.Message, _statistics);
        }
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Реализация обработки множества HTML файлов
    /// </summary>
    public async Task<ProcessingResult<IEnumerable<Route>>> ProcessHtmlFilesAsync(
        IEnumerable<string> htmlFiles, CancellationToken cancellationToken = default)
    {
        try
        {
            var filesList = htmlFiles.ToList();
            _logger.LogInformation("Начало обработки {Count} HTML файлов", filesList.Count);

            _statistics.TotalFiles = filesList.Count;
            var allRoutes = new List<Route>();

            // Обрабатываем файлы параллельно для повышения производительности
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
            var tasks = filesList.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await ProcessHtmlFileAsync(file, cancellationToken);
                    if (result.IsSuccess && result.Data != null)
                    {
                        lock (allRoutes)
                        {
                            allRoutes.AddRange(result.Data);
                        }
                    }
                    return result;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);

            // Агрегируем статистику
            foreach (var result in results.Skip(1))
            {
                if (result.Statistics != null)
                    _statistics.Merge(result.Statistics);
            }

            // Удаляем дубликаты маршрутов
            var uniqueRoutes = await RemoveDuplicatesAsync(allRoutes, cancellationToken);

            _statistics.ProcessedRoutes = uniqueRoutes.Count;
            _statistics.DuplicateRoutes = allRoutes.Count - uniqueRoutes.Count;

            _logger.LogInformation("Обработка завершена. Всего маршрутов: {Total}, уникальных: {Unique}",
                allRoutes.Count, uniqueRoutes.Count);

            return ProcessingResult<IEnumerable<Route>>.Success(uniqueRoutes, _statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при обработке HTML файлов");
            _statistics.AddError($"Критическая ошибка: {ex.Message}");
            return ProcessingResult<IEnumerable<Route>>.Failure(ex.Message, _statistics);
        }
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Реализация получения статистики с правильным типом возврата
    /// </summary>
    public AnalysisNorm.Core.Entities.ProcessingStatistics GetProcessingStatistics()
    {
        _statistics.Finish(); // Устанавливаем время окончания если еще не установлено
        return _statistics;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Парсит содержимое HTML файла и извлекает маршруты
    /// </summary>
    private async Task<List<Route>> ParseHtmlContentAsync(string htmlContent, string fileName, CancellationToken cancellationToken)
    {
        var routes = new List<Route>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Ищем таблицы или структурированные данные маршрутов
            var routeNodes = doc.DocumentNode.SelectNodes("//tr[@class='route']") ??
                           doc.DocumentNode.SelectNodes("//div[@class='route']") ??
                           doc.DocumentNode.SelectNodes("//table//tr[position()>1]");

            if (routeNodes == null || routeNodes.Count == 0)
            {
                _logger.LogWarning("В файле {FileName} не найдены структурированные данные маршрутов", fileName);

                // Пытаемся парсить как неструктурированный текст
                routes.AddRange(await ParseUnstructuredTextAsync(htmlContent, fileName, cancellationToken));
                return routes;
            }

            foreach (var node in routeNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var route = await ParseRouteFromNodeAsync(node, fileName);
                    if (route != null && route.IsValid)
                    {
                        routes.Add(route);
                        _statistics.IncrementSuccess();
                    }
                    else
                    {
                        _statistics.AddWarning($"Маршрут в файле {fileName} не прошел валидацию");
                        _statistics.IncrementSkipped();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка при парсинге узла маршрута в файле {FileName}", fileName);
                    _statistics.IncrementError();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при парсинге HTML контента из файла {FileName}", fileName);
            throw;
        }

        return routes;
    }

    /// <summary>
    /// Парсит маршрут из HTML узла
    /// </summary>
    private async Task<Route?> ParseRouteFromNodeAsync(HtmlNode node, string fileName)
    {
        try
        {
            var route = Route.Create("", 0, 0, 0);
            route.DataSource = fileName;

            // Извлекаем текстовое содержимое узла
            var nodeText = _textNormalizer.CleanText(node.InnerText);

            // Парсим основные поля
            await ParseBasicFieldsAsync(route, nodeText, node);

            // Парсим локомотив
            ParseLocomotiveInfo(route, nodeText);

            // Парсим числовые значения
            ParseNumericValues(route, nodeText, node);

            // Парсим участки маршрута
            ParseRouteSections(route, nodeText, node);

            return route;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании маршрута из узла");
            return null;
        }
    }

    /// <summary>
    /// Парсит базовые поля маршрута
    /// </summary>
    private async Task ParseBasicFieldsAsync(Route route, string nodeText, HtmlNode node)
    {
        // Название маршрута
        var nameMatch = RouteNameRegex.Match(nodeText);
        if (nameMatch.Success)
        {
            route.Name = nameMatch.Groups[1].Value.Trim();
        }
        else
        {
            // Если не нашли явного названия, берем из первой ячейки или первых слов
            var firstCell = node.SelectSingleNode(".//td[1]") ?? node.SelectSingleNode(".//span[1]");
            route.Name = firstCell?.InnerText?.Trim() ?? "Неизвестный маршрут";
        }

        // Дата
        var dateMatch = DateRegex.Match(nodeText);
        if (dateMatch.Success)
        {
            if (DateTime.TryParse(dateMatch.Groups[1].Value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
            {
                route.Date = date;
                route.TripDate = date;
            }
        }
        else
        {
            route.Date = DateTime.UtcNow;
            route.TripDate = DateTime.UtcNow;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Парсит информацию о локомотиве
    /// </summary>
    private void ParseLocomotiveInfo(Route route, string nodeText)
    {
        var locoMatch = LocomotiveRegex.Match(nodeText);
        if (locoMatch.Success)
        {
            route.LocomotiveSeries = locoMatch.Groups[1].Value.Trim();

            if (int.TryParse(locoMatch.Groups[2].Value, out var number))
            {
                route.LocomotiveNumber = number;
            }
        }
    }

    /// <summary>
    /// Парсит числовые значения маршрута
    /// </summary>
    private void ParseNumericValues(Route route, string nodeText, HtmlNode node)
    {
        // Расстояние
        var distanceMatch = DistanceRegex.Match(nodeText);
        if (distanceMatch.Success)
        {
            var distanceStr = distanceMatch.Groups[1].Value.Replace(",", ".");
            if (decimal.TryParse(distanceStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
            {
                route.Distance = distance;
            }
        }

        // Масса состава
        var massMatch = MassRegex.Match(nodeText);
        if (massMatch.Success)
        {
            var massStr = massMatch.Groups[1].Value.Replace(",", ".");
            if (decimal.TryParse(massStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var mass))
            {
                route.TrainMass = mass;
                route.TrainWeight = mass;
                route.BruttoTons = mass;
            }
        }

        // Расход электроэнергии
        var consumptionMatch = ConsumptionRegex.Match(nodeText);
        if (consumptionMatch.Success)
        {
            var consumptionStr = consumptionMatch.Groups[1].Value.Replace(",", ".");
            if (decimal.TryParse(consumptionStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var consumption))
            {
                route.ElectricConsumption = consumption;
                route.ActualConsumption = consumption;
                route.FactConsumption = consumption;
            }
        }

        // Если не нашли базовые значения, попробуем найти их в ячейках таблицы
        FillMissingValuesFromCells(route, node);
    }

    /// <summary>
    /// Заполняет отсутствующие значения из ячеек таблицы
    /// </summary>
    private void FillMissingValuesFromCells(Route route, HtmlNode node)
    {
        var cells = node.SelectNodes(".//td");
        if (cells == null) return;

        for (int i = 0; i < cells.Count; i++)
        {
            var cellText = _textNormalizer.CleanText(cells[i].InnerText);

            if (decimal.TryParse(cellText.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                // Эвристика для определения типа значения по позиции и величине
                if (route.Distance == 0 && value > 0 && value < 1000) // Расстояние обычно до 1000 км
                {
                    route.Distance = value;
                }
                else if (route.TrainMass == 0 && value > 100 && value < 10000) // Масса обычно от 100 до 10000 т
                {
                    route.TrainMass = value;
                    route.TrainWeight = value;
                }
                else if (!route.ElectricConsumption.HasValue && value > 1 && value < 5000) // Расход обычно от 1 до 5000 кВт*ч
                {
                    route.ElectricConsumption = value;
                    route.ActualConsumption = value;
                }
            }
        }

        // Устанавливаем нагрузку на ось как производную от массы состава
        if (route.AxleLoad == 0 && route.TrainMass > 0)
        {
            // Примерная формула: нагрузка на ось = масса состава / количество осей (примерно 4-6 осей на вагон)
            route.AxleLoad = Math.Min(25m, route.TrainMass / 50); // Ограничиваем 25 тоннами на ось
        }
    }

    /// <summary>
    /// Парсит участки маршрута
    /// </summary>
    private void ParseRouteSections(Route route, string nodeText, HtmlNode node)
    {
        var sectionsNode = node.SelectSingleNode(".//td[@class='sections']") ??
                          node.SelectSingleNode(".//span[@class='sections']");

        if (sectionsNode != null)
        {
            var sectionsText = _textNormalizer.CleanText(sectionsNode.InnerText);
            var sections = sectionsText.Split(new[] { ',', ';', '-' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim())
                                     .Where(s => !string.IsNullOrEmpty(s))
                                     .ToList();

            route.SectionNames = sections;
        }

        // Если не нашли специальный узел, попробуем найти участки в общем тексте
        if (route.SectionNames.Count == 0)
        {
            var possibleSections = ExtractSectionNamesFromText(nodeText);
            route.SectionNames = possibleSections;
        }
    }

    /// <summary>
    /// Извлекает названия участков из текста
    /// </summary>
    private List<string> ExtractSectionNamesFromText(string text)
    {
        var sections = new List<string>();

        // Паттерны для поиска станций/участков
        var stationPatterns = new[]
        {
            @"ст\.\s*([А-Я][а-яё\-\s]+)",
            @"([А-Я][а-яё\-\s]+)\s*-\s*([А-Я][а-яё\-\s]+)",
            @"участок\s+([А-Я][а-яё\-\s]+)",
            @"от\s+([А-Я][а-яё\-\s]+)\s+до\s+([А-Я][а-яё\-\s]+)"
        };

        foreach (var pattern in stationPatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    var section = match.Groups[i].Value.Trim();
                    if (!string.IsNullOrEmpty(section) && section.Length > 2)
                    {
                        sections.Add(section);
                    }
                }
            }
        }

        return sections.Distinct().Take(5).ToList(); // Ограничиваем 5 участками
    }

    /// <summary>
    /// Парсит неструктурированный текст
    /// </summary>
    private async Task<List<Route>> ParseUnstructuredTextAsync(string htmlContent, string fileName, CancellationToken cancellationToken)
    {
        var routes = new List<Route>();

        // Удаляем HTML теги и получаем чистый текст
        var plainText = Regex.Replace(htmlContent, "<[^>]+>", " ");
        plainText = _textNormalizer.CleanText(plainText);

        // Разбиваем на потенциальные маршруты по ключевым словам
        var routeBlocks = Regex.Split(plainText, @"(?=маршрут|рейс|поездка)", RegexOptions.IgnoreCase)
                              .Where(block => block.Length > 50) // Фильтруем слишком короткие блоки
                              .Take(100) // Ограничиваем количество для производительности
                              .ToList();

        foreach (var block in routeBlocks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var route = await CreateRouteFromTextBlock(block, fileName);
                if (route != null && route.IsValid)
                {
                    routes.Add(route);
                    _statistics.IncrementSuccess();
                }
                else
                {
                    _statistics.IncrementSkipped();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при парсинге блока текста");
                _statistics.IncrementError();
            }
        }

        return routes;
    }

    /// <summary>
    /// Создает маршрут из текстового блока
    /// </summary>
    private async Task<Route?> CreateRouteFromTextBlock(string textBlock, string fileName)
    {
        try
        {
            var route = Route.Create(ExtractRouteName(textBlock), 0, 0, 0);
            route.DataSource = fileName;

            // Парсим базовые поля из текстового блока
            await ParseBasicFieldsAsync(route, textBlock, new HtmlTextNode(null!, textBlock, 0));
            ParseLocomotiveInfo(route, textBlock);
            ParseNumericValues(route, textBlock, new HtmlTextNode(null!, textBlock, 0));

            // Извлекаем участки из текста
            route.SectionNames = ExtractSectionNamesFromText(textBlock);

            return route.IsValid ? route : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании маршрута из текстового блока");
            return null;
        }
    }

    /// <summary>
    /// Извлекает название маршрута из текстового блока
    /// </summary>
    private string ExtractRouteName(string textBlock)
    {
        // Попытка найти название после ключевых слов
        var patterns = new[]
        {
            @"маршрут\s*:?\s*([^\n\r]{10,100})",
            @"рейс\s*:?\s*([^\n\r]{10,100})",
            @"поездка\s*:?\s*([^\n\r]{10,100})",
            @"направление\s*:?\s*([^\n\r]{10,100})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(textBlock, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        // Если не нашли, берем первые слова блока
        var firstLine = textBlock.Split('\n')[0].Trim();
        return firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
    }

    /// <summary>
    /// Удаляет дубликаты маршрутов
    /// </summary>
    private async Task<List<Route>> RemoveDuplicatesAsync(List<Route> routes, CancellationToken cancellationToken)
    {
        var uniqueRoutes = new Dictionary<string, Route>();

        foreach (var route in routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = $"{route.RouteNumber}_{route.Date:yyyyMMdd}_{route.LocomotiveSeries}_{route.LocomotiveNumber}";

            if (!uniqueRoutes.ContainsKey(key))
            {
                uniqueRoutes[key] = route;
            }
            else
            {
                // Если есть дубликат, выбираем более полный по данным
                var existing = uniqueRoutes[key];
                if (IsMoreComplete(route, existing))
                {
                    uniqueRoutes[key] = route;
                }
                _statistics.DuplicateRoutes++;
            }
        }

        return await Task.FromResult(uniqueRoutes.Values.ToList());
    }

    /// <summary>
    /// Определяет какой маршрут более полный по данным
    /// </summary>
    private bool IsMoreComplete(Route newRoute, Route existingRoute)
    {
        var newScore = GetCompletenessScore(newRoute);
        var existingScore = GetCompletenessScore(existingRoute);
        return newScore > existingScore;
    }

    /// <summary>
    /// Вычисляет оценку полноты данных маршрута
    /// </summary>
    private int GetCompletenessScore(Route route)
    {
        var score = 0;

        if (!string.IsNullOrEmpty(route.RouteNumber)) score++;
        if (!string.IsNullOrEmpty(route.LocomotiveSeries)) score++;
        if (route.LocomotiveNumber.HasValue) score++;
        if (route.Distance > 0) score++;
        if (route.TrainMass > 0) score++;
        if (route.AxleLoad > 0) score++;
        if (route.ElectricConsumption.HasValue || route.ActualConsumption.HasValue) score++;
        if (route.MechanicalWork.HasValue) score++;
        if (route.TravelTime != default(TimeSpan)) score++;

        return score;
    }

    #endregion
}

/// <summary>
/// Простая обертка для текстовых узлов HtmlAgilityPack
/// </summary>
internal class HtmlTextNode : HtmlNode
{
    public HtmlTextNode(HtmlDocument doc, string text, int position) : base(HtmlNodeType.Text, doc, position)
    {
        InnerHtml = text;
    }
}