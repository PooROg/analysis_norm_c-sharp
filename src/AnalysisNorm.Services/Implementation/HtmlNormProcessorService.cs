using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;
// ИСПРАВЛЕНО: Explicit using alias для разрешения конфликтов
using CoreProcessingResult = AnalysisNorm.Core.Entities.ProcessingResult<System.Collections.Generic.IEnumerable<AnalysisNorm.Core.Entities.Norm>>;
using CoreProcessingStatistics = AnalysisNorm.Core.Entities.ProcessingStatistics;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Процессор HTML файлов норм расхода электроэнергии
/// Точное соответствие HTMLNormProcessor из Python analysis/html_norm_processor.py
/// ИСПРАВЛЕНО: Правильные возвращаемые типы из AnalysisNorm.Core.Entities
/// </summary>
public class HtmlNormProcessorService : IHtmlNormProcessorService
{
    private readonly ILogger<HtmlNormProcessorService> _logger;
    private readonly IFileEncodingDetector _encodingDetector;
    private readonly ITextNormalizer _textNormalizer;
    private readonly ApplicationSettings _settings;

    // ИСПРАВЛЕНО: Используем CoreProcessingStatistics из AnalysisNorm.Core.Entities
    private readonly CoreProcessingStatistics _stats = new();

    // Compiled regex patterns for norm extraction (аналог Python patterns)
    private static readonly Regex NormIdPattern = new(@"(?:Норма|НОРМА)\s*№?\s*(\d+(?:\.\d+)?(?:\-\d+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NormTypePattern = new(@"(Нажатие|Н/Ф|Уд\.?\s*на\s*работу)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LoadPattern = new(@"(\d+(?:\.\d+)?)\s*т/ось",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ConsumptionPattern = new(@"(\d+(?:\.\d+)?)\s*кВт",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public HtmlNormProcessorService(
        ILogger<HtmlNormProcessorService> logger,
        IFileEncodingDetector encodingDetector,
        ITextNormalizer textNormalizer,
        IOptions<ApplicationSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _encodingDetector = encodingDetector ?? throw new ArgumentNullException(nameof(encodingDetector));
        _textNormalizer = textNormalizer ?? throw new ArgumentNullException(nameof(textNormalizer));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Обрабатывает список HTML файлов норм
    /// Соответствует process_html_files из Python HTMLNormProcessor
    /// ИСПРАВЛЕНО: Правильный возвращаемый тип CoreProcessingResult
    /// </summary>
    public async Task<CoreProcessingResult> ProcessHtmlFilesAsync(
        IEnumerable<string> htmlFiles,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var allNorms = new ConcurrentBag<Norm>();
        var fileStats = new ConcurrentDictionary<string, object>();

        _logger.LogInformation("Начинаем обработку {FileCount} HTML файлов норм", htmlFiles.Count());

        var filesArray = htmlFiles.ToArray();
        _stats.TotalFiles = filesArray.Length;
        _stats.StartTime = DateTime.UtcNow;

        try
        {
            // Параллельная обработка файлов для производительности (улучшение по сравнению с Python)
            var tasks = filesArray.Select(async filePath =>
            {
                try
                {
                    var norms = await ProcessSingleNormFileAsync(filePath, cancellationToken);

                    foreach (var norm in norms)
                    {
                        allNorms.Add(norm);
                    }

                    Interlocked.Increment(ref _stats.ProcessedFiles);
                    _logger.LogDebug("Файл {FilePath} обработан: {NormCount} норм", filePath, norms.Count);

                    fileStats[filePath] = new { NormCount = norms.Count, Success = true };
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _stats.ErrorFiles);
                    _stats.Errors.Add($"Ошибка обработки файла {filePath}: {ex.Message}");
                    _logger.LogError(ex, "Ошибка обработки файла норм {FilePath}", filePath);

                    fileStats[filePath] = new { NormCount = 0, Success = false, Error = ex.Message };
                }
            });

            await Task.WhenAll(tasks);

            stopwatch.Stop();
            _stats.ProcessingTime = stopwatch.Elapsed;
            _stats.EndTime = DateTime.UtcNow;

            var resultNorms = allNorms.ToList();

            // Валидация и очистка норм (аналог Python validation)
            var validatedNorms = await ValidateAndCleanNormsAsync(resultNorms);

            _logger.LogInformation("Обработка завершена за {ElapsedMs}мс: {ValidNormCount}/{TotalNormCount} валидных норм из {FileCount} файлов",
                stopwatch.ElapsedMilliseconds, validatedNorms.Count, resultNorms.Count, filesArray.Length);

            // ИСПРАВЛЕНО: Используем CoreProcessingResult.Success из AnalysisNorm.Core.Entities
            return AnalysisNorm.Core.Entities.ProcessingResult<IEnumerable<Norm>>.Success(
                validatedNorms,
                _stats
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при обработке HTML файлов норм");

            stopwatch.Stop();
            _stats.ProcessingTime = stopwatch.Elapsed;
            _stats.EndTime = DateTime.UtcNow;

            // ИСПРАВЛЕНО: Используем CoreProcessingResult.Failure из AnalysisNorm.Core.Entities
            return AnalysisNorm.Core.Entities.ProcessingResult<IEnumerable<Norm>>.Failure(
                ex.Message,
                _stats
            );
        }
    }

    /// <summary>
    /// Обрабатывает один HTML файл норм
    /// ИСПРАВЛЕНО: Правильный возвращаемый тип CoreProcessingResult
    /// </summary>
    public async Task<CoreProcessingResult> ProcessHtmlFileAsync(
        string htmlFile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var norms = await ProcessSingleNormFileAsync(htmlFile, cancellationToken);
            var validatedNorms = await ValidateAndCleanNormsAsync(norms);

            return AnalysisNorm.Core.Entities.ProcessingResult<IEnumerable<Norm>>.Success(
                validatedNorms,
                _stats
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки файла {HtmlFile}", htmlFile);
            return AnalysisNorm.Core.Entities.ProcessingResult<IEnumerable<Norm>>.Failure(
                ex.Message,
                _stats
            );
        }
    }

    /// <summary>
    /// Получает статистику обработки норм
    /// ИСПРАВЛЕНО: Правильный возвращаемый тип CoreProcessingStatistics
    /// </summary>
    public CoreProcessingStatistics GetProcessingStatistics()
    {
        return _stats;
    }

    #region Private Methods - Полная реализация обработки норм

    /// <summary>
    /// Обрабатывает один HTML файл норм
    /// Соответствует _process_single_file из Python
    /// </summary>
    private async Task<List<Norm>> ProcessSingleNormFileAsync(string filePath, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Начинаем обработку файла норм: {FilePath}", filePath);

        // Читаем файл с детекцией кодировки (аналог read_text из Python)
        var htmlContent = await _encodingDetector.ReadTextWithEncodingDetectionAsync(filePath);
        if (string.IsNullOrEmpty(htmlContent))
        {
            _logger.LogWarning("Файл норм {FilePath} пуст или не удалось прочитать", filePath);
            return new List<Norm>();
        }

        // Парсим HTML для извлечения норм (аналог BeautifulSoup из Python)
        var document = new HtmlDocument();
        document.LoadHtml(htmlContent);

        var norms = new List<Norm>();

        // Извлекаем таблицы с нормами (аналог find_all('table') из Python)
        var tables = document.DocumentNode.SelectNodes("//table");
        if (tables != null)
        {
            foreach (var table in tables)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tableNorms = ExtractNormsFromTable(table, filePath);
                norms.AddRange(tableNorms);
            }
        }

        // Также ищем нормы в div и других контейнерах
        var divs = document.DocumentNode.SelectNodes("//div[contains(@class,'norm') or contains(@class,'table')]");
        if (divs != null)
        {
            foreach (var div in divs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var divNorms = ExtractNormsFromContainer(div, filePath);
                norms.AddRange(divNorms);
            }
        }

        _logger.LogTrace("Извлечено {Count} норм из файла {FilePath}", norms.Count, filePath);
        return norms;
    }

    /// <summary>
    /// Извлекает нормы из HTML таблицы
    /// Соответствует extract_norms_from_table из Python
    /// </summary>
    private List<Norm> ExtractNormsFromTable(HtmlNode table, string filePath)
    {
        var norms = new List<Norm>();

        try
        {
            var rows = table.SelectNodes(".//tr");
            if (rows == null || !rows.Any())
                return norms;

            // Анализируем структуру таблицы
            var headerRow = rows.FirstOrDefault();
            var isNormTable = IsNormTable(headerRow);

            if (!isNormTable)
                return norms;

            // Пропускаем заголовок и обрабатываем строки данных
            foreach (var row in rows.Skip(1))
            {
                var cells = row.SelectNodes(".//td | .//th");
                if (cells == null || cells.Count < 2)
                    continue;

                var norm = ExtractNormFromRow(cells, filePath);
                if (norm != null)
                {
                    norms.Add(norm);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка извлечения норм из таблицы в файле {FilePath}", filePath);
        }

        return norms;
    }

    /// <summary>
    /// Извлекает нормы из любого HTML контейнера
    /// Дополнительный метод для гибкости парсинга
    /// </summary>
    private List<Norm> ExtractNormsFromContainer(HtmlNode container, string filePath)
    {
        var norms = new List<Norm>();

        try
        {
            // Ищем паттерны норм в тексте контейнера
            var text = _textNormalizer.CleanText(container.InnerText);

            var normMatches = NormIdPattern.Matches(text);
            foreach (Match normMatch in normMatches)
            {
                var norm = ExtractNormFromText(text, normMatch, filePath);
                if (norm != null)
                {
                    norms.Add(norm);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка извлечения норм из контейнера в файле {FilePath}", filePath);
        }

        return norms;
    }

    /// <summary>
    /// Проверяет является ли таблица таблицей норм
    /// Аналог is_norm_table из Python
    /// </summary>
    private bool IsNormTable(HtmlNode? headerRow)
    {
        if (headerRow == null) return false;

        var headerText = _textNormalizer.CleanText(headerRow.InnerText).ToLowerInvariant();

        // Ключевые слова которые указывают на таблицу норм
        var normKeywords = new[] { "норма", "нагрузка", "расход", "т/ось", "квт", "consumption" };

        return normKeywords.Any(keyword => headerText.Contains(keyword));
    }

    /// <summary>
    /// Извлекает норму из строки таблицы
    /// Соответствует extract_norm_from_row из Python
    /// </summary>
    private Norm? ExtractNormFromRow(HtmlNodeCollection cells, string filePath)
    {
        try
        {
            // Извлекаем текст из ячеек
            var cellTexts = cells.Select(cell => _textNormalizer.CleanText(cell.InnerText)).ToList();

            // Ищем ID нормы в первой ячейке или в объединенном тексте
            var allText = string.Join(" ", cellTexts);
            var normIdMatch = NormIdPattern.Match(allText);
            if (!normIdMatch.Success)
                return null;

            var normId = normIdMatch.Groups[1].Value;

            // Определяем тип нормы (аналог determine_norm_type из Python)
            var normType = DetermineNormType(allText);

            var norm = new Norm
            {
                NormId = normId,
                NormType = normType,
                Points = new List<NormPoint>(),
                CreatedAt = DateTime.UtcNow,
                Description = ExtractDescription(allText)
            };

            // Извлекаем точки нагрузка-расход (аналог extract_load_consumption_pairs из Python)
            ExtractNormPoints(cellTexts, norm);

            // Валидируем норму перед возвратом
            return IsValidNorm(norm) ? norm : null;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка извлечения нормы из строки таблицы в файле {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Извлекает норму из произвольного текста
    /// Дополнительный метод для парсинга свободного текста
    /// </summary>
    private Norm? ExtractNormFromText(string text, Match normIdMatch, string filePath)
    {
        try
        {
            var normId = normIdMatch.Groups[1].Value;
            var normType = DetermineNormType(text);

            var norm = new Norm
            {
                NormId = normId,
                NormType = normType,
                Points = new List<NormPoint>(),
                CreatedAt = DateTime.UtcNow,
                Description = ExtractDescription(text)
            };

            // Ищем числовые пары в тексте
            ExtractNormPointsFromText(text, norm);

            return IsValidNorm(norm) ? norm : null;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка извлечения нормы из текста в файле {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Определяет тип нормы на основе текста
    /// Соответствует determine_norm_type из Python
    /// </summary>
    private string DetermineNormType(string text)
    {
        var typeMatch = NormTypePattern.Match(text);
        if (typeMatch.Success)
        {
            return typeMatch.Groups[1].Value;
        }

        // Дополнительная логика определения типа по контексту
        var lowerText = text.ToLowerInvariant();

        if (lowerText.Contains("нажати") || lowerText.Contains("ось"))
            return "Нажатие";

        if (lowerText.Contains("н/ф") || lowerText.Contains("нефтян"))
            return "Н/Ф";

        if (lowerText.Contains("работ") || lowerText.Contains("удельн"))
            return "Уд. на работу";

        return "Нажатие"; // По умолчанию
    }

    /// <summary>
    /// Извлекает описание нормы
    /// </summary>
    private string? ExtractDescription(string text)
    {
        // Извлекаем первое предложение как описание
        var sentences = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var firstSentence = sentences.FirstOrDefault()?.Trim();

        return !string.IsNullOrEmpty(firstSentence) && firstSentence.Length > 10
            ? firstSentence
            : null;
    }

    /// <summary>
    /// Извлекает точки нормы из ячеек таблицы
    /// Соответствует extract_load_consumption_pairs из Python
    /// </summary>
    private void ExtractNormPoints(List<string> cellTexts, Norm norm)
    {
        // Метод 1: Парные ячейки (нагрузка, расход)
        for (int i = 0; i < cellTexts.Count - 1; i++)
        {
            var loadMatch = LoadPattern.Match(cellTexts[i]);
            var consumptionMatch = ConsumptionPattern.Match(cellTexts[i + 1]);

            if (loadMatch.Success && consumptionMatch.Success)
            {
                if (decimal.TryParse(loadMatch.Groups[1].Value, out var load) &&
                    decimal.TryParse(consumptionMatch.Groups[1].Value, out var consumption))
                {
                    norm.Points.Add(new NormPoint
                    {
                        Load = load,
                        Consumption = consumption
                    });
                }
            }
        }

        // Метод 2: Поиск в каждой ячейке пар значений
        foreach (var cellText in cellTexts)
        {
            ExtractNormPointsFromText(cellText, norm);
        }
    }

    /// <summary>
    /// Извлекает точки нормы из произвольного текста
    /// Использует регулярные выражения для поиска числовых пар
    /// </summary>
    private void ExtractNormPointsFromText(string text, Norm norm)
    {
        // Паттерн для поиска пар "нагрузка т/ось - расход кВт"
        var pairPattern = new Regex(@"(\d+(?:\.\d+)?)\s*т/ось[^\d]*(\d+(?:\.\d+)?)\s*кВт", RegexOptions.IgnoreCase);
        var matches = pairPattern.Matches(text);

        foreach (Match match in matches)
        {
            if (decimal.TryParse(match.Groups[1].Value, out var load) &&
                decimal.TryParse(match.Groups[2].Value, out var consumption))
            {
                // Проверяем что такая точка еще не добавлена
                if (!norm.Points.Any(p => p.Load == load && p.Consumption == consumption))
                {
                    norm.Points.Add(new NormPoint
                    {
                        Load = load,
                        Consumption = consumption
                    });
                }
            }
        }

        // Дополнительные паттерны для разных форматов записи
        var alternatePatterns = new[]
        {
            new Regex(@"(\d+(?:\.\d+)?)\s*-\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase),
            new Regex(@"(\d+(?:\.\d+)?)\s*:\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)
        };

        foreach (var pattern in alternatePatterns)
        {
            var altMatches = pattern.Matches(text);
            foreach (Match match in altMatches)
            {
                if (decimal.TryParse(match.Groups[1].Value, out var load) &&
                    decimal.TryParse(match.Groups[2].Value, out var consumption) &&
                    load <= 30 && consumption <= 1000) // Разумные границы
                {
                    if (!norm.Points.Any(p => p.Load == load && p.Consumption == consumption))
                    {
                        norm.Points.Add(new NormPoint
                        {
                            Load = load,
                            Consumption = consumption
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Проверяет валидность нормы
    /// Соответствует validate_norm из Python
    /// </summary>
    private bool IsValidNorm(Norm norm)
    {
        if (norm == null || string.IsNullOrEmpty(norm.NormId))
        {
            return false;
        }

        // Минимальное количество точек для валидной нормы
        if (norm.Points.Count < 2)
        {
            _logger.LogTrace("Норма {NormId} отклонена: недостаточно точек ({Count})", norm.NormId, norm.Points.Count);
            return false;
        }

        // Проверяем валидность точек
        var validPoints = norm.Points.Where(p =>
            p.Load >= 1 && p.Load <= 30 &&           // Разумные границы нагрузки
            p.Consumption >= 10 && p.Consumption <= 1000  // Разумные границы расхода
        ).ToList();

        if (validPoints.Count < 2)
        {
            _logger.LogTrace("Норма {NormId} отклонена: недостаточно валидных точек ({ValidCount}/{TotalCount})",
                norm.NormId, validPoints.Count, norm.Points.Count);
            return false;
        }

        // Обновляем норму только валидными точками
        norm.Points = validPoints;

        return true;
    }

    /// <summary>
    /// Валидирует и очищает нормы
    /// Соответствует финальной обработке в Python
    /// </summary>
    private async Task<List<Norm>> ValidateAndCleanNormsAsync(List<Norm> norms)
    {
        _logger.LogDebug("Валидируем и очищаем {Count} норм", norms.Count);

        var validNorms = new List<Norm>();
        var duplicateNormIds = new HashSet<string>();

        // Группируем нормы по ID для обработки дубликатов
        var normGroups = norms.GroupBy(n => n.NormId).ToList();

        foreach (var group in normGroups)
        {
            var groupNorms = group.ToList();

            if (groupNorms.Count > 1)
            {
                duplicateNormIds.Add(group.Key!);
                _logger.LogDebug("Найдено {Count} дубликатов нормы {NormId}, оставляем лучшую",
                    groupNorms.Count, group.Key);

                // Выбираем норму с наибольшим количеством точек
                var bestNorm = groupNorms.OrderByDescending(n => n.Points.Count).First();
                validNorms.Add(bestNorm);
            }
            else
            {
                validNorms.Add(groupNorms.First());
            }
        }

        // Дополнительная валидация и сортировка
        validNorms = validNorms
            .Where(IsValidNorm)
            .OrderBy(n => n.NormId)
            .ToList();

        // Обновляем статистику
        _stats.ProcessedRoutes = validNorms.Count; // Используем как количество обработанных норм
        _stats.TotalRoutes = norms.Count; // Используем как общее количество найденных норм
        _stats.DuplicateRoutes = norms.Count - validNorms.Count;

        if (duplicateNormIds.Any())
        {
            _stats.Warnings.Add($"Найдено дубликатов норм: {string.Join(", ", duplicateNormIds)}");
        }

        _logger.LogInformation("Валидация завершена: {ValidCount}/{TotalCount} норм прошли валидацию",
            validNorms.Count, norms.Count);

        return validNorms;
    }

    #endregion
}