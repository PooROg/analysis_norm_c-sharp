using System.Text;
using System.Globalization;
using AnalysisNorm.Core.Entities;

namespace AnalysisNorm.Services.Implementation;

// ========================================
// НЕДОСТАЮЩИЕ ТИПЫ И КЛАССЫ
// ========================================

/// <summary>
/// Класс фильтрации локомотивов
/// Соответствует LocomotiveFilter из Python filter.py
/// </summary>
public class LocomotiveFilter
{
    /// <summary>
    /// Выбранные локомотивы (серия, номер)
    /// </summary>
    public List<(string Series, int Number)> SelectedLocomotives { get; set; } = new();

    /// <summary>
    /// Использовать коэффициенты корректировки
    /// </summary>
    public bool UseCoefficients { get; set; }

    /// <summary>
    /// Исключить локомотивы с малой работой
    /// </summary>
    public bool ExcludeLowWork { get; set; }

    /// <summary>
    /// Минимальное количество поездок для включения
    /// </summary>
    public int MinTrips { get; set; } = 5;

    /// <summary>
    /// Фильтр по сериям локомотивов
    /// </summary>
    public HashSet<string> SelectedSeries { get; set; } = new();

    /// <summary>
    /// Проверяет проходит ли маршрут фильтр
    /// </summary>
    public bool PassesFilter(Route route)
    {
        if (string.IsNullOrEmpty(route.LocomotiveSeries) || !route.LocomotiveNumber.HasValue)
            return false;

        // Проверяем по выбранным локомотивам
        if (SelectedLocomotives.Any())
        {
            var locomotive = (route.LocomotiveSeries, route.LocomotiveNumber.Value);
            if (!SelectedLocomotives.Contains(locomotive))
                return false;
        }

        // Проверяем по выбранным сериям
        if (SelectedSeries.Any())
        {
            if (!SelectedSeries.Contains(route.LocomotiveSeries))
                return false;
        }

        return true;
    }
}

/// <summary>
/// Результаты валидации данных
/// Соответствует ValidationResults из Python validation.py
/// </summary>
public class ValidationResults
{
    /// <summary>
    /// Общее количество проверенных элементов
    /// </summary>
    public int TotalChecked { get; set; }

    /// <summary>
    /// Количество валидных элементов
    /// </summary>
    public int ValidCount { get; set; }

    /// <summary>
    /// Количество невалидных элементов
    /// </summary>
    public int InvalidCount { get; set; }

    /// <summary>
    /// Список ошибок валидации
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Список предупреждений
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Детальные результаты по элементам
    /// </summary>
    public Dictionary<string, List<string>> Details { get; set; } = new();

    /// <summary>
    /// Процент успешной валидации
    /// </summary>
    public double SuccessRate => TotalChecked > 0 ? (double)ValidCount / TotalChecked * 100 : 0;

    /// <summary>
    /// Есть ли критические ошибки
    /// </summary>
    public bool HasCriticalErrors => Errors.Any(e => e.Contains("критич", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Добавляет ошибку валидации
    /// </summary>
    public void AddError(string elementId, string error)
    {
        Errors.Add($"{elementId}: {error}");
        
        if (!Details.ContainsKey(elementId))
            Details[elementId] = new List<string>();
        
        Details[elementId].Add($"ERROR: {error}");
        InvalidCount++;
    }

    /// <summary>
    /// Добавляет предупреждение
    /// </summary>
    public void AddWarning(string elementId, string warning)
    {
        Warnings.Add($"{elementId}: {warning}");
        
        if (!Details.ContainsKey(elementId))
            Details[elementId] = new List<string>();
        
        Details[elementId].Add($"WARNING: {warning}");
    }

    /// <summary>
    /// Отмечает элемент как валидный
    /// </summary>
    public void MarkAsValid(string elementId)
    {
        ValidCount++;
        
        if (!Details.ContainsKey(elementId))
            Details[elementId] = new List<string>();
        
        Details[elementId].Add("VALID");
    }

    /// <summary>
    /// Получает сводку результатов валидации
    /// </summary>
    public string GetSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== РЕЗУЛЬТАТЫ ВАЛИДАЦИИ ===");
        sb.AppendLine($"Проверено элементов: {TotalChecked}");
        sb.AppendLine($"Валидных: {ValidCount} ({SuccessRate:F1}%)");
        sb.AppendLine($"Невалидных: {InvalidCount}");
        sb.AppendLine($"Предупреждений: {Warnings.Count}");
        
        if (HasCriticalErrors)
            sb.AppendLine("⚠️ ОБНАРУЖЕНЫ КРИТИЧЕСКИЕ ОШИБКИ!");

        return sb.ToString();
    }
}

// ========================================
// НЕДОСТАЮЩИЕ ИНТЕРФЕЙСЫ
// ========================================

/// <summary>
/// Интерфейс для детекции кодировки файлов
/// Соответствует EncodingDetector из Python
/// </summary>
public interface IFileEncodingDetector
{
    /// <summary>
    /// Читает текстовый файл с автоматической детекцией кодировки
    /// </summary>
    Task<string> ReadTextWithEncodingDetectionAsync(string filePath);

    /// <summary>
    /// Определяет кодировку файла
    /// </summary>
    Encoding DetectEncoding(string filePath);

    /// <summary>
    /// Проверяет является ли файл текстовым
    /// </summary>
    bool IsTextFile(string filePath);
}

/// <summary>
/// Интерфейс для нормализации и очистки текста
/// Соответствует TextNormalizer из Python
/// </summary>
public interface ITextNormalizer
{
    /// <summary>
    /// Очищает и нормализует текст
    /// </summary>
    string CleanText(string text);

    /// <summary>
    /// Безопасно парсит decimal значение
    /// </summary>
    decimal? SafeDecimal(string text);

    /// <summary>
    /// Безопасно парсит int значение
    /// </summary>
    int? SafeInt(string text);

    /// <summary>
    /// Безопасно парсит DateTime
    /// </summary>
    DateTime? SafeDateTime(string text);

    /// <summary>
    /// Удаляет HTML теги из текста
    /// </summary>
    string StripHtml(string html);

    /// <summary>
    /// Нормализует пробелы в тексте
    /// </summary>
    string NormalizeWhitespace(string text);
}

// ========================================
// РЕАЛИЗАЦИИ НЕДОСТАЮЩИХ ИНТЕРФЕЙСОВ
// ========================================

/// <summary>
/// Реализация детектора кодировки файлов
/// </summary>
public class FileEncodingDetector : IFileEncodingDetector
{
    private readonly ILogger<FileEncodingDetector> _logger;

    // Порядок проверки кодировок (наиболее вероятные первыми)
    private static readonly Encoding[] EncodingsToTry = 
    {
        Encoding.UTF8,
        Encoding.GetEncoding("windows-1251"), // cp1251
        Encoding.GetEncoding("koi8-r"),
        Encoding.ASCII,
        Encoding.GetEncoding("iso-8859-1")
    };

    public FileEncodingDetector(ILogger<FileEncodingDetector> logger)
    {
        _logger = logger;
        
        // Регистрируем провайдер кодировок для поддержки cp1251
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<string> ReadTextWithEncodingDetectionAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Файл не найден: {FilePath}", filePath);
            return string.Empty;
        }

        try
        {
            var detectedEncoding = DetectEncoding(filePath);
            var content = await File.ReadAllTextAsync(filePath, detectedEncoding);
            
            _logger.LogTrace("Файл {FilePath} прочитан с кодировкой {Encoding}", 
                filePath, detectedEncoding.EncodingName);
            
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка чтения файла {FilePath}", filePath);
            return string.Empty;
        }
    }

    public Encoding DetectEncoding(string filePath)
    {
        try
        {
            var buffer = new byte[4096];
            using var stream = File.OpenRead(filePath);
            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            // Проверяем BOM для UTF-8
            if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                return Encoding.UTF8;
            }

            // Пробуем различные кодировки
            foreach (var encoding in EncodingsToTry)
            {
                try
                {
                    var text = encoding.GetString(buffer, 0, bytesRead);
                    
                    // Проверяем на валидность (нет странных символов)
                    if (IsValidText(text))
                    {
                        _logger.LogTrace("Определена кодировка {Encoding} для файла {FilePath}", 
                            encoding.EncodingName, filePath);
                        return encoding;
                    }
                }
                catch
                {
                    // Пробуем следующую кодировку
                }
            }

            // По умолчанию UTF-8
            return Encoding.UTF8;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка определения кодировки файла {FilePath}", filePath);
            return Encoding.UTF8;
        }
    }

    public bool IsTextFile(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var textExtensions = new[] { ".txt", ".html", ".htm", ".xml", ".csv", ".json" };
            
            return textExtensions.Contains(extension);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Проверяет валидность декодированного текста
    /// </summary>
    private static bool IsValidText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        // Считаем процент контрольных символов
        var controlChars = text.Count(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');
        var controlPercentage = (double)controlChars / text.Length;

        // Если контрольных символов больше 5%, текст скорее всего декодирован неверно
        return controlPercentage < 0.05;
    }
}

/// <summary>
/// Реализация нормализатора текста
/// </summary>
public class TextNormalizer : ITextNormalizer
{
    private readonly ILogger<TextNormalizer> _logger;

    // Паттерны для очистки текста
    private static readonly System.Text.RegularExpressions.Regex WhitespacePattern = 
        new(@"\s+", System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex HtmlTagPattern = 
        new(@"<[^>]*>", System.Text.RegularExpressions.RegexOptions.Compiled);

    public TextNormalizer(ILogger<TextNormalizer> logger)
    {
        _logger = logger;
    }

    public string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        try
        {
            // Удаляем HTML теги
            text = StripHtml(text);
            
            // Нормализуем пробелы
            text = NormalizeWhitespace(text);
            
            // Удаляем управляющие символы кроме \r\n\t
            text = new string(text.Where(c => !char.IsControl(c) || c == '\r' || c == '\n' || c == '\t').ToArray());
            
            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка очистки текста");
            return text?.Trim() ?? string.Empty;
        }
    }

    public decimal? SafeDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            // Очищаем текст от лишних символов
            var cleaned = CleanNumericText(text);
            
            // Пробуем различные форматы
            var formats = new[] 
            { 
                CultureInfo.InvariantCulture, 
                CultureInfo.CurrentCulture,
                new CultureInfo("ru-RU"),
                new CultureInfo("en-US") 
            };

            foreach (var format in formats)
            {
                if (decimal.TryParse(cleaned, NumberStyles.Any, format, out var result))
                {
                    return result;
                }
            }

            // Пробуем заменить запятую на точку и наоборот
            var withDot = cleaned.Replace(',', '.');
            if (decimal.TryParse(withDot, NumberStyles.Any, CultureInfo.InvariantCulture, out var resultDot))
            {
                return resultDot;
            }

            var withComma = cleaned.Replace('.', ',');
            if (decimal.TryParse(withComma, NumberStyles.Any, new CultureInfo("ru-RU"), out var resultComma))
            {
                return resultComma;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка парсинга decimal из '{Text}'", text);
            return null;
        }
    }

    public int? SafeInt(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var cleaned = CleanNumericText(text);
            
            if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            // Пробуем извлечь только цифры
            var digitsOnly = new string(cleaned.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrEmpty(digitsOnly) && int.TryParse(digitsOnly, out var digitsResult))
            {
                return digitsResult;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка парсинга int из '{Text}'", text);
            return null;
        }
    }

    public DateTime? SafeDateTime(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var cleaned = CleanText(text);

            // Стандартные форматы даты
            var formats = new[]
            {
                "dd.MM.yyyy",
                "dd/MM/yyyy", 
                "yyyy-MM-dd",
                "dd.MM.yy",
                "dd/MM/yy",
                "d.M.yyyy",
                "d/M/yyyy"
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(cleaned, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                {
                    return result;
                }
            }

            // Пробуем стандартный парсинг
            if (DateTime.TryParse(cleaned, CultureInfo.CurrentCulture, DateTimeStyles.None, out var standardResult))
            {
                return standardResult;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка парсинга DateTime из '{Text}'", text);
            return null;
        }
    }

    public string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        try
        {
            // Заменяем некоторые HTML entity
            var text = html
                .Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'");

            // Удаляем HTML теги
            text = HtmlTagPattern.Replace(text, " ");

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка удаления HTML тегов");
            return html;
        }
    }

    public string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        try
        {
            // Заменяем множественные пробелы на одиночные
            text = WhitespacePattern.Replace(text, " ");
            
            // Нормализуем переводы строк
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            
            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка нормализации пробелов");
            return text?.Trim() ?? string.Empty;
        }
    }

    /// <summary>
    /// Очищает текст для парсинга числовых значений
    /// </summary>
    private string CleanNumericText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Удаляем все кроме цифр, точек, запятых и знака минус
        var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-').ToArray());
        
        // Обрабатываем случай множественных разделителей
        if (cleaned.Count(c => c == '.' || c == ',') > 1)
        {
            // Если есть и точки и запятые, последний символ - десятичный разделитель
            var lastDotIndex = cleaned.LastIndexOf('.');
            var lastCommaIndex = cleaned.LastIndexOf(',');
            
            if (lastDotIndex > lastCommaIndex)
            {
                // Точка - десятичный разделитель, убираем все запятые
                cleaned = cleaned.Replace(",", "");
            }
            else if (lastCommaIndex > lastDotIndex)
            {
                // Запятая - десятичный разделитель, убираем все точки
                cleaned = cleaned.Replace(".", "");
            }
        }

        return cleaned;
    }
}

// ========================================
// EXTENSION METHODS для исправления синтаксических ошибок
// ========================================

/// <summary>
/// Extension methods для работы с nullable типами
/// ИСПРАВЛЕНО: Решает проблему с оператором ?? для int
/// </summary>
public static class NullableExtensions
{
    /// <summary>
    /// Возвращает значение или значение по умолчанию для nullable int
    /// </summary>
    public static int GetValueOrDefault(this int? value, int defaultValue = 0)
    {
        return value.HasValue ? value.Value : defaultValue;
    }

    /// <summary>
    /// Возвращает значение или значение по умолчанию для nullable decimal
    /// </summary>
    public static decimal GetValueOrDefault(this decimal? value, decimal defaultValue = 0)
    {
        return value.HasValue ? value.Value : defaultValue;
    }

    /// <summary>
    /// Возвращает значение или значение по умолчанию для nullable double
    /// </summary>
    public static double GetValueOrDefault(this double? value, double defaultValue = 0)
    {
        return value.HasValue ? value.Value : defaultValue;
    }

    /// <summary>
    /// Возвращает значение или значение по умолчанию для nullable DateTime
    /// </summary>
    public static DateTime GetValueOrDefault(this DateTime? value, DateTime defaultValue = default)
    {
        return value.HasValue ? value.Value : (defaultValue == default ? DateTime.UtcNow : defaultValue);
    }
}

/// <summary>
/// Extension methods для Dictionary для безопасного получения значений
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// Безопасное получение значения из Dictionary с возвращением значения по умолчанию
    /// </summary>
    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default!)
        where TKey : notnull
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Безопасное получение значения из Dictionary с возвращением значения по умолчанию
    /// </summary>
    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default!)
        where TKey : notnull
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }
}

// ========================================
// ДОПОЛНИТЕЛЬНЫЕ ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ
// ========================================

/// <summary>
/// Константы для анализа данных
/// </summary>
public static class AnalysisConstants
{
    // Границы валидности данных
    public const decimal MinValidLoad = 0.1m;
    public const decimal MaxValidLoad = 50m;
    public const decimal MinValidConsumption = 1m;
    public const decimal MaxValidConsumption = 2000m;

    // Границы отклонений для классификации
    public const decimal EconomyStrongThreshold = -10m;
    public const decimal EconomyMediumThreshold = -5m;
    public const decimal EconomyWeakThreshold = -2m;
    public const decimal NormalThreshold = 2m;
    public const decimal OverrunWeakThreshold = 5m;
    public const decimal OverrunMediumThreshold = 10m;

    // Настройки интерполяции
    public const int MinInterpolationPoints = 2;
    public const int MaxInterpolationPoints = 1000;
    public const int DefaultInterpolationPoints = 100;

    // Настройки производительности
    public const int DefaultBatchSize = 1000;
    public static readonly int MaxConcurrentTasks = Environment.ProcessorCount;
    public const int DefaultTimeoutSeconds = 300;
}

/// <summary>
/// Вспомогательный класс для работы с путями файлов
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Проверяет является ли путь валидным
    /// </summary>
    public static bool IsValidPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Создает директорию если она не существует
    /// </summary>
    public static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Получает безопасное имя файла
    /// </summary>
    public static string GetSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
    }

    /// <summary>
    /// Получает уникальное имя файла если файл уже существует
    /// </summary>
    public static string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
            return filePath;

        var directory = Path.GetDirectoryName(filePath) ?? "";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        var counter = 1;
        string newPath;
        
        do
        {
            newPath = Path.Combine(directory, $"{fileNameWithoutExt}_{counter}{extension}");
            counter++;
        }
        while (File.Exists(newPath));

        return newPath;
    }
}