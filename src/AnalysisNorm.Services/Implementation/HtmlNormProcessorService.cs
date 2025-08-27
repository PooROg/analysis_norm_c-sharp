using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Процессор HTML файлов норм расхода электроэнергии
/// Точное соответствие HTMLNormProcessor из Python analysis/html_norm_processor.py
/// </summary>
public class HtmlNormProcessorService : IHtmlNormProcessorService
{
    private readonly ILogger<HtmlNormProcessorService> _logger;
    private readonly IFileEncodingDetector _encodingDetector;
    private readonly ITextNormalizer _textNormalizer;
    private readonly ApplicationSettings _settings;

    // Processing statistics
    private readonly ProcessingStatistics _stats = new();
    
    // Compiled regex patterns for norm extraction
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
        _logger = logger;
        _encodingDetector = encodingDetector;
        _textNormalizer = textNormalizer;
        _settings = settings.Value;
    }

    /// <summary>
    /// Обрабатывает список HTML файлов норм
    /// Соответствует process_html_files из Python HTMLNormProcessor
    /// </summary>
    public async Task<ProcessingResult<IEnumerable<Norm>>> ProcessHtmlFilesAsync(
        IEnumerable<string> htmlFiles,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var allNorms = new List<Norm>();
        var fileStats = new Dictionary<string, object>();

        _logger.LogInformation("Начинаем обработку {FileCount} HTML файлов норм", htmlFiles.Count());
        
        var filesArray = htmlFiles.ToArray();
        _stats.TotalFiles = filesArray.Length;

        try
        {
            foreach (var filePath in filesArray)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var norms = await ProcessSingleNormFileAsync(filePath, cancellationToken);
                    allNorms.AddRange(norms);
                    
                    Interlocked.Increment(ref _stats.ProcessedFiles);
                    _logger.LogDebug("Файл {FilePath} обработан: {NormCount} норм", filePath, norms.Count);
                    
                    fileStats[filePath] = new { NormCount = norms.Count, Success = true };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка обработки файла норм {FilePath}", filePath);
                    Interlocked.Increment(ref _stats.SkippedFiles);
                    fileStats[filePath] = new { Error = ex.Message, Success = false };
                }
            }

            _stats.TotalRoutes = allNorms.Count; // В контексте норм это количество норм
            _stats.ProcessedRoutes = allNorms.Count(n => !string.IsNullOrEmpty(n.NormId));
            _stats.ProcessingTime = stopwatch.Elapsed;

            // Валидация и очистка норм (аналог validation в Python)
            var validatedNorms = await ValidateAndCleanNormsAsync(allNorms);

            _logger.LogInformation("Обработка норм завершена: {ProcessedFiles}/{TotalFiles} файлов, {ValidNorms} валидных норм", 
                _stats.ProcessedFiles, _stats.TotalFiles, validatedNorms.Count);

            return new ProcessingResult<IEnumerable<Norm>>(
                Success: true,
                Data: validatedNorms,
                ErrorMessage: null,
                Statistics: _stats with { Details = fileStats }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при обработке HTML файлов норм");
            return new ProcessingResult<IEnumerable<Norm>>(
                Success: false,
                Data: null,
                ErrorMessage: ex.Message,
                Statistics: _stats
            );
        }
    }

    /// <summary>
    /// Обрабатывает один HTML файл норм
    /// Соответствует логике обработки файла в Python HTMLNormProcessor
    /// </summary>
    private async Task<List<Norm>> ProcessSingleNormFileAsync(string filePath, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Обрабатываем файл норм: {FilePath}", filePath);

        // Читаем файл с детекцией кодировки
        var htmlContent = await _encodingDetector.ReadTextWithEncodingDetectionAsync(filePath);
        if (string.IsNullOrEmpty(htmlContent))
        {
            _logger.LogWarning("Файл норм {FilePath} пуст или не удалось прочитать", filePath);
            return new List<Norm>();
        }

        // Очищаем HTML контент
        var cleanedContent = CleanNormHtmlContent(htmlContent);
        
        // Извлекаем нормы из HTML
        var normBlocks = ExtractNormBlocks(cleanedContent);
        if (!normBlocks.Any())
        {
            _logger.LogWarning("В файле {FilePath} не найдены нормы", filePath);
            return new List<Norm>();
        }

        var norms = new List<Norm>();
        foreach (var normBlock in normBlocks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var parsedNorm = ParseNormBlock(normBlock);
                if (parsedNorm != null && IsValidNorm(parsedNorm))
                {
                    norms.Add(parsedNorm);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка парсинга блока нормы в файле {FilePath}", filePath);
            }
        }

        _logger.LogDebug("Файл {FilePath} обработан: {NormCount} норм", filePath, norms.Count);
        return norms;
    }

    /// <summary>
    /// Очищает HTML контент норм от лишних элементов
    /// Соответствует очистке HTML в Python HTMLNormProcessor
    /// </summary>
    private string CleanNormHtmlContent(string htmlContent)
    {
        _logger.LogTrace("Очищаем HTML контент норм");

        // Удаляем лишние элементы специфичные для норм
        var patterns = new[]
        {
            (@"<font[^>]*>.*?</font>", RegexOptions.Singleline | RegexOptions.IgnoreCase),
            (@"<center>.*?</center>", RegexOptions.Singleline | RegexOptions.IgnoreCase),
            (@"<b>.*?</b>", RegexOptions.Singleline | RegexOptions.IgnoreCase),
            (@"<i>.*?</i>", RegexOptions.Singleline | RegexOptions.IgnoreCase),
            (@"<u>.*?</u>", RegexOptions.Singleline | RegexOptions.IgnoreCase),
            (@"&nbsp;", RegexOptions.IgnoreCase),
            (@"\s+", RegexOptions.None)
        };

        foreach (var (pattern, options) in patterns)
        {
            htmlContent = Regex.Replace(htmlContent, pattern, " ", options);
        }

        return htmlContent.Trim();
    }

    /// <summary>
    /// Извлекает блоки норм из HTML контента
    /// Соответствует извлечению норм в Python
    /// </summary>
    private List<string> ExtractNormBlocks(string htmlContent)
    {
        _logger.LogTrace("Извлекаем блоки норм из HTML");

        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);

        var normBlocks = new List<string>();

        // Ищем таблицы с нормами
        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables != null)
        {
            foreach (var table in tables)
            {
                var tableText = _textNormalizer.NormalizeText(table.InnerText);
                
                // Проверяем что таблица содержит данные нормы
                if (ContainsNormData(tableText))
                {
                    normBlocks.Add(table.OuterHtml);
                }
            }
        }

        // Если не найдены таблицы, ищем другие блоки с нормами
        if (!normBlocks.Any())
        {
            var divs = doc.DocumentNode.SelectNodes("//div") ?? new List<HtmlNode>();
            var paragraphs = doc.DocumentNode.SelectNodes("//p") ?? new List<HtmlNode>();
            
            var allBlocks = divs.Concat(paragraphs);
            
            foreach (var block in allBlocks)
            {
                var blockText = _textNormalizer.NormalizeText(block.InnerText);
                if (ContainsNormData(blockText))
                {
                    normBlocks.Add(block.OuterHtml);
                }
            }
        }

        _logger.LogTrace("Найдено блоков норм: {Count}", normBlocks.Count);
        return normBlocks;
    }

    /// <summary>
    /// Проверяет содержит ли блок данные нормы
    /// </summary>
    private bool ContainsNormData(string text)
    {
        var lowerText = text.ToLower();
        
        // Ключевые слова для идентификации норм
        var keywords = new[] { "норма", "расход", "кВт", "т/ось", "нажатие", "н/ф" };
        var containsKeywords = keywords.Any(keyword => lowerText.Contains(keyword));
        
        // Проверяем наличие числовых данных
        var hasNumbers = Regex.IsMatch(text, @"\d+(?:\.\d+)?");
        
        return containsKeywords && hasNumbers;
    }

    /// <summary>
    /// Парсит блок нормы и создает объект Norm
    /// Соответствует парсингу нормы в Python
    /// </summary>
    private Norm? ParseNormBlock(string normBlockHtml)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(normBlockHtml);
            
            var text = _textNormalizer.NormalizeText(doc.DocumentNode.InnerText);
            
            var norm = new Norm
            {
                CreatedAt = DateTime.UtcNow,
                Points = new List<NormPoint>()
            };

            // Извлекаем ID нормы
            var normIdMatch = NormIdPattern.Match(text);
            if (normIdMatch.Success)
            {
                norm.NormId = normIdMatch.Groups[1].Value;
            }
            else
            {
                _logger.LogTrace("Не удалось извлечь ID нормы из блока: {Text}", 
                    text.Substring(0, Math.Min(100, text.Length)));
                return null;
            }

            // Извлекаем тип нормы
            var normTypeMatch = NormTypePattern.Match(text);
            if (normTypeMatch.Success)
            {
                norm.NormType = NormalizeNormType(normTypeMatch.Groups[1].Value);
            }
            else
            {
                norm.NormType = "Нажатие"; // Значение по умолчанию
            }

            // Парсим точки нормы
            ParseNormPoints(norm, doc);

            return norm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка парсинга блока нормы");
            return null;
        }
    }

    /// <summary>
    /// Нормализует тип нормы
    /// </summary>
    private string NormalizeNormType(string normType)
    {
        return normType.ToLower() switch
        {
            var t when t.Contains("нажатие") => "Нажатие",
            var t when t.Contains("н/ф") => "Н/Ф", 
            var t when t.Contains("уд") && t.Contains("работ") => "Уд. на работу",
            _ => "Нажатие"
        };
    }

    /// <summary>
    /// Парсит точки нормы из HTML документа
    /// Соответствует извлечению точек в Python
    /// </summary>
    private void ParseNormPoints(Norm norm, HtmlDocument doc)
    {
        var points = new List<NormPoint>();
        
        // Ищем таблицы с данными точек
        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables != null)
        {
            foreach (var table in tables)
            {
                var tablePoints = ExtractPointsFromTable(table, norm.NormId!);
                points.AddRange(tablePoints);
            }
        }

        // Если не найдены точки в таблицах, пытаемся извлечь из текста
        if (!points.Any())
        {
            var textPoints = ExtractPointsFromText(doc.DocumentNode.InnerText, norm.NormId!);
            points.AddRange(textPoints);
        }

        // Сортируем точки по нагрузке и присваиваем порядок
        points = points
            .Where(p => p.Load > 0 && p.Consumption > 0)
            .OrderBy(p => p.Load)
            .Select((p, index) => 
            {
                p.Order = index + 1;
                return p;
            })
            .ToList();

        norm.Points = points;
    }

    /// <summary>
    /// Извлекает точки нормы из таблицы
    /// </summary>
    private List<NormPoint> ExtractPointsFromTable(HtmlNode table, string normId)
    {
        var points = new List<NormPoint>();
        
        var rows = table.SelectNodes(".//tr");
        if (rows == null) return points;

        foreach (var row in rows)
        {
            var cells = row.SelectNodes(".//td | .//th");
            if (cells == null || cells.Count < 2) continue;

            try
            {
                var cellTexts = cells.Select(cell => _textNormalizer.NormalizeText(cell.InnerText)).ToList();
                
                // Ищем пары нагрузка-расход
                for (int i = 0; i < cellTexts.Count - 1; i++)
                {
                    var loadMatch = LoadPattern.Match(cellTexts[i]);
                    var consumptionMatch = ConsumptionPattern.Match(cellTexts[i + 1]);
                    
                    if (loadMatch.Success && consumptionMatch.Success)
                    {
                        var load = _textNormalizer.SafeDecimal(loadMatch.Groups[1].Value);
                        var consumption = _textNormalizer.SafeDecimal(consumptionMatch.Groups[1].Value);
                        
                        if (load > 0 && consumption > 0)
                        {
                            points.Add(new NormPoint
                            {
                                NormId = normId,
                                Load = load,
                                Consumption = consumption,
                                PointType = "base"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Ошибка извлечения точки из строки таблицы");
            }
        }

        return points;
    }

    /// <summary>
    /// Извлекает точки нормы из текста
    /// </summary>
    private List<NormPoint> ExtractPointsFromText(string text, string normId)
    {
        var points = new List<NormPoint>();
        
        // Ищем все числа, которые могут быть нагрузками и расходами
        var loadMatches = LoadPattern.Matches(text);
        var consumptionMatches = ConsumptionPattern.Matches(text);
        
        // Пытаемся сопоставить нагрузки с расходами
        var loads = loadMatches.Cast<Match>()
            .Select(m => new { Position = m.Index, Value = _textNormalizer.SafeDecimal(m.Groups[1].Value) })
            .Where(item => item.Value > 0)
            .ToList();
            
        var consumptions = consumptionMatches.Cast<Match>()
            .Select(m => new { Position = m.Index, Value = _textNormalizer.SafeDecimal(m.Groups[1].Value) })
            .Where(item => item.Value > 0)
            .ToList();

        // Сопоставляем ближайшие нагрузки и расходы
        foreach (var load in loads)
        {
            var closestConsumption = consumptions
                .Where(c => c.Position > load.Position)
                .OrderBy(c => c.Position - load.Position)
                .FirstOrDefault();
                
            if (closestConsumption != null)
            {
                points.Add(new NormPoint
                {
                    NormId = normId,
                    Load = load.Value,
                    Consumption = closestConsumption.Value,
                    PointType = "base"
                });
                
                // Удаляем использованный расход
                consumptions.Remove(closestConsumption);
            }
        }

        return points;
    }

    /// <summary>
    /// Проверяет валидность нормы
    /// Соответствует валидации в Python
    /// </summary>
    private bool IsValidNorm(Norm norm)
    {
        // Проверяем обязательные поля
        if (string.IsNullOrEmpty(norm.NormId))
        {
            _logger.LogTrace("Норма отклонена: отсутствует ID");
            return false;
        }

        if (!norm.Points.Any())
        {
            _logger.LogTrace("Норма {NormId} отклонена: нет точек", norm.NormId);
            return false;
        }

        // Проверяем что есть минимум 2 точки для интерполяции
        if (norm.Points.Count < 2)
        {
            _logger.LogTrace("Норма {NormId} отклонена: недостаточно точек ({Count})", norm.NormId, norm.Points.Count);
            return false;
        }

        // Проверяем валидность точек
        var validPoints = norm.Points.Where(p => 
            p.Load >= AnalysisConstants.MinValidLoad && 
            p.Load <= AnalysisConstants.MaxValidLoad &&
            p.Consumption >= AnalysisConstants.MinValidConsumption && 
            p.Consumption <= AnalysisConstants.MaxValidConsumption
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
                _logger.LogWarning("Найдены дублированные нормы с ID {NormId}: {Count} экземпляров", 
                    group.Key, groupNorms.Count);
                
                // Выбираем норму с наибольшим количеством точек
                var bestNorm = groupNorms
                    .OrderByDescending(n => n.Points.Count)
                    .ThenByDescending(n => n.CreatedAt)
                    .First();
                    
                validNorms.Add(bestNorm);
            }
            else
            {
                validNorms.Add(groupNorms.First());
            }
        }

        // Финальная валидация
        var finalNorms = validNorms.Where(IsValidNorm).ToList();

        _logger.LogInformation("Валидация норм завершена: {ValidCount}/{TotalCount} норм прошли валидацию, {DuplicateCount} дубликатов удалено", 
            finalNorms.Count, norms.Count, duplicateNormIds.Count);

        return finalNorms;
    }

    public ProcessingStatistics GetProcessingStatistics()
    {
        return _stats;
    }
}

/// <summary>
/// Расширения для работы с нормами
/// </summary>
public static class NormExtensions
{
    /// <summary>
    /// Проверяет может ли норма использоваться для интерполяции
    /// </summary>
    public static bool CanInterpolate(this Norm norm)
    {
        return norm.Points.Count >= 2 && 
               norm.Points.Any(p => p.Load > 0 && p.Consumption > 0);
    }
    
    /// <summary>
    /// Получает диапазон нагрузок нормы
    /// </summary>
    public static (decimal Min, decimal Max) GetLoadRange(this Norm norm)
    {
        if (!norm.Points.Any())
            return (0, 0);
            
        var loads = norm.Points.Select(p => p.Load).ToList();
        return (loads.Min(), loads.Max());
    }
    
    /// <summary>
    /// Получает среднее значение расхода
    /// </summary>
    public static decimal GetAverageConsumption(this Norm norm)
    {
        if (!norm.Points.Any())
            return 0;
            
        return norm.Points.Average(p => p.Consumption);
    }
}