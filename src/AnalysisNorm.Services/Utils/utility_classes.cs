using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.Extensions.Logging;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Детектор кодировки файлов
/// Соответствует логике read_text из Python html_route_processor.py
/// </summary>
public class FileEncodingDetector : IFileEncodingDetector
{
    private readonly ILogger<FileEncodingDetector> _logger;
    private readonly string[] _supportedEncodings = ["cp1251", "utf-8", "utf-8-sig"];

    public FileEncodingDetector(ILogger<FileEncodingDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Определяет кодировку файла методом проб (как в Python)
    /// Приоритет: cp1251 -> utf-8 -> utf-8-sig
    /// </summary>
    public async Task<string> DetectEncodingAsync(string filePath)
    {
        foreach (var encodingName in _supportedEncodings)
        {
            try
            {
                var encoding = GetEncoding(encodingName);
                using var reader = new StreamReader(filePath, encoding);
                
                // Читаем первые 1000 символов для проверки
                var buffer = new char[1000];
                await reader.ReadAsync(buffer, 0, buffer.Length);
                
                // Проверяем что декодирование прошло успешно
                var text = new string(buffer);
                if (!HasDecodingErrors(text))
                {
                    _logger.LogDebug("Файл {FilePath} успешно декодирован с кодировкой {Encoding}", 
                        filePath, encodingName);
                    return encodingName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Ошибка декодирования файла {FilePath} с кодировкой {Encoding}: {Error}", 
                    filePath, encodingName, ex.Message);
                continue;
            }
        }

        _logger.LogWarning("Не удалось определить кодировку файла {FilePath}, используем UTF-8", filePath);
        return "utf-8";
    }

    /// <summary>
    /// Читает файл с автоматическим определением кодировки
    /// Полный аналог read_text из Python
    /// </summary>
    public async Task<string> ReadTextWithEncodingDetectionAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Файл не найден: {filePath}");
        }

        foreach (var encodingName in _supportedEncodings)
        {
            try
            {
                var encoding = GetEncoding(encodingName);
                var content = await File.ReadAllTextAsync(filePath, encoding);
                
                if (!HasDecodingErrors(content))
                {
                    _logger.LogDebug("Файл {FilePath} прочитан с кодировкой {Encoding}", 
                        Path.GetFileName(filePath), encodingName);
                    return content;
                }
            }
            catch (Exception ex) when (ex is DecoderFallbackException || ex is ArgumentException)
            {
                _logger.LogDebug("Кодировка {Encoding} не подходит для файла {FilePath}: {Error}", 
                    encodingName, Path.GetFileName(filePath), ex.Message);
                continue;
            }
        }

        // Последняя попытка с UTF-8 и игнорированием ошибок
        _logger.LogWarning("Принудительное чтение файла {FilePath} с UTF-8 и игнорированием ошибок", 
            Path.GetFileName(filePath));
        
        var utf8WithFallback = Encoding.UTF8;
        return await File.ReadAllTextAsync(filePath, utf8WithFallback);
    }

    private static Encoding GetEncoding(string encodingName)
    {
        return encodingName switch
        {
            "cp1251" => Encoding.GetEncoding("windows-1251"),
            "utf-8" => Encoding.UTF8,
            "utf-8-sig" => Encoding.UTF8, // .NET автоматически обрабатывает BOM
            _ => Encoding.UTF8
        };
    }

    private static bool HasDecodingErrors(string text)
    {
        // Проверяем на характерные признаки неправильной декодировки
        return text.Contains('\uFFFD') || // replacement character
               text.Contains("Ð") ||       // частая ошибка cp1251 -> utf-8
               string.IsNullOrEmpty(text.Trim());
    }
}

/// <summary>
/// Нормализатор текста и безопасные конверторы
/// Соответствует normalize_text и safe_* функциям из Python utils.py
/// </summary>
public class TextNormalizer : ITextNormalizer
{
    private readonly ILogger<TextNormalizer> _logger;

    public TextNormalizer(ILogger<TextNormalizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Нормализует текст - убирает лишние пробелы, nbsp и т.д.
    /// Точный аналог normalize_text из Python utils.py
    /// </summary>
    public string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Заменяем неразрывные пробелы и HTML entities
        text = text.Replace('\u00A0', ' ')       // &nbsp;
                  .Replace("&nbsp;", " ")
                  .Replace('\u00AD', ' ')        // soft hyphen
                  .Replace('\t', ' ');           // табуляции

        // Убираем множественные пробелы (аналог re.sub(r'\s+', ' ', text))
        text = Regex.Replace(text, @"\s+", " ");

        return text.Trim();
    }

    /// <summary>
    /// Безопасное преобразование к decimal
    /// Соответствует safe_float из Python utils.py
    /// </summary>
    public decimal SafeDecimal(object? value, decimal defaultValue = 0m)
    {
        if (value is null)
            return defaultValue;

        if (value is decimal d)
            return d;

        if (value is double db)
            return (decimal)db;

        if (value is float f)
            return (decimal)f;

        if (value is int i)
            return i;

        if (value is long l)
            return l;

        // Обрабатываем строки
        if (value is string str)
        {
            str = NormalizeText(str);
            
            if (string.IsNullOrEmpty(str) || 
                str.Equals("-", StringComparison.Ordinal) ||
                str.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("nan", StringComparison.OrdinalIgnoreCase))
            {
                return defaultValue;
            }

            // Убираем точку в конце (как в Python)
            if (str.EndsWith('.'))
                str = str[..^1];

            // Заменяем запятую на точку (европейский формат)
            str = str.Replace(',', '.');

            if (decimal.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return Math.Abs(result); // Возвращаем абсолютное значение (как в Python)
            }
        }

        try
        {
            return Math.Abs(Convert.ToDecimal(value));
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Не удалось преобразовать значение {Value} к decimal: {Error}", value, ex.Message);
            return defaultValue;
        }
    }

    /// <summary>
    /// Безопасное преобразование к int
    /// Соответствует safe_int из Python utils.py
    /// </summary>
    public int SafeInt(object? value, int defaultValue = 0)
    {
        var decimalValue = SafeDecimal(value, defaultValue);
        
        try
        {
            // Проверяем что значение помещается в int
            if (decimalValue > int.MaxValue || decimalValue < int.MinValue)
            {
                _logger.LogWarning("Значение {Value} выходит за пределы int, используем значение по умолчанию", decimalValue);
                return defaultValue;
            }

            return (int)Math.Round(decimalValue, MidpointRounding.AwayFromZero);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Не удалось преобразовать значение {Value} к int: {Error}", value, ex.Message);
            return defaultValue;
        }
    }

    /// <summary>
    /// Безопасное деление с проверкой на ноль
    /// Соответствует safe_divide из Python utils.py
    /// </summary>
    public decimal SafeDivide(object? numerator, object? denominator, decimal defaultValue = 0m)
    {
        var num = SafeDecimal(numerator);
        var den = SafeDecimal(denominator);

        if (den == 0m)
        {
            return defaultValue;
        }

        return Math.Abs(num / den);
    }

    /// <summary>
    /// Безопасное вычитание ряда значений
    /// Соответствует safe_subtract из Python html_route_processor.py
    /// </summary>
    public decimal SafeSubtract(params object?[] values)
    {
        if (values.Length == 0)
            return 0m;

        var validValues = values
            .Select(v => SafeDecimal(v))
            .Where(v => v != 0m) // Пропускаем нулевые значения (как None в Python)
            .ToArray();

        if (validValues.Length == 0)
            return 0m;

        var result = validValues[0];
        for (int i = 1; i < validValues.Length; i++)
        {
            result -= validValues[i];
        }

        return Math.Abs(result); // Возвращаем абсолютное значение
    }
}

/// <summary>
/// Утилиты для работы с маршрутами
/// Соответствует функциям из Python utils.py
/// </summary>
public static class RouteUtils
{
    /// <summary>
    /// Извлекает ключ маршрута для группировки дубликатов
    /// Точный аналог extract_route_key из Python utils.py
    /// </summary>
    public static string? ExtractRouteKey(string? routeNumber, string? tripDate, string? driverTab)
    {
        if (string.IsNullOrWhiteSpace(routeNumber) || 
            string.IsNullOrWhiteSpace(tripDate) || 
            string.IsNullOrWhiteSpace(driverTab))
        {
            return null;
        }

        return $"{routeNumber.Trim()}_{tripDate.Trim()}_{driverTab.Trim()}";
    }

    /// <summary>
    /// Форматирует число для отображения
    /// Соответствует format_number из Python utils.py
    /// </summary>
    public static string FormatNumber(object? value, int decimals = 1, string fallback = "N/A")
    {
        if (value is null)
            return fallback;

        try
        {
            var decimalValue = value is decimal d ? d : Convert.ToDecimal(value);
            
            if (decimalValue == 0m && (value is null || value.ToString() == "N/A"))
                return fallback;

            return decimalValue.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Нормализует название серии локомотива
    /// Соответствует normalize_series из Python coefficients.py
    /// </summary>
    public static string NormalizeLocomotiveSeries(string series)
    {
        if (string.IsNullOrEmpty(series)) 
            return string.Empty;
        
        // Убираем все кроме букв и цифр, приводим к верхнему регистру
        var normalized = Regex.Replace(series.ToUpper(), @"[^А-ЯA-Z0-9]", string.Empty);
        return normalized;
    }
}

/// <summary>
/// Константы и пороги для анализа отклонений
/// Соответствует StatusClassifier из Python utils.py
/// </summary>
public static class AnalysisConstants
{
    // Пороги отклонений в процентах (как в Python StatusClassifier.THRESHOLDS)
    public const decimal StrongEconomyThreshold = -30m;
    public const decimal MediumEconomyThreshold = -20m;
    public const decimal WeakEconomyThreshold = -5m;
    public const decimal NormalUpperThreshold = 5m;
    public const decimal WeakOverrunThreshold = 20m;
    public const decimal MediumOverrunThreshold = 30m;

    // Цвета для статусов (для будущего использования в графиках)
    public static readonly Dictionary<string, string> StatusColors = new()
    {
        [DeviationStatus.EconomyStrong] = "darkgreen",
        [DeviationStatus.EconomyMedium] = "green",
        [DeviationStatus.EconomyWeak] = "lightgreen", 
        [DeviationStatus.Normal] = "blue",
        [DeviationStatus.OverrunWeak] = "orange",
        [DeviationStatus.OverrunMedium] = "darkorange",
        [DeviationStatus.OverrunStrong] = "red"
    };

    // Настройки интерполяции норм
    public const decimal InterpolationTolerance = 0.1m;
    public const int MaxInterpolationPoints = 100;
    public const decimal MinValidLoad = 0.1m;
    public const decimal MaxValidLoad = 100m;
    public const decimal MinValidConsumption = 0.1m;
    public const decimal MaxValidConsumption = 1000m;

    // Настройки обработки HTML
    public static readonly string[] HtmlEncodingPriority = ["cp1251", "utf-8", "utf-8-sig"];
    public const int HtmlProcessingTimeout = 30000; // 30 секунд
    public const int MaxConcurrentFiles = 4;

    // Настройки экспорта
    public const string DefaultExcelExtension = ".xlsx";
    public const int ExcelMaxRowsPerSheet = 1000000; // Excel limitation
}

/// <summary>
/// Расширения для работы с коллекциями (аналоги Python pandas операций)
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Группирует элементы по ключу и подсчитывает количество (аналог pandas groupby.count)
    /// </summary>
    public static Dictionary<TKey, int> GroupByCount<T, TKey>(
        this IEnumerable<T> source, 
        Func<T, TKey> keySelector) where TKey : notnull
    {
        return source
            .GroupBy(keySelector)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Удаляет дубликаты на основе ключа (аналог pandas drop_duplicates)
    /// </summary>
    public static IEnumerable<T> DistinctBy<T, TKey>(
        this IEnumerable<T> source,
        Func<T, TKey> keySelector)
    {
        var seenKeys = new HashSet<TKey>();
        foreach (var element in source)
        {
            var key = keySelector(element);
            if (seenKeys.Add(key))
            {
                yield return element;
            }
        }
    }

    /// <summary>
    /// Фильтрует не null/пустые значения (аналог pandas dropna)
    /// </summary>
    public static IEnumerable<T> WhereNotNullOrEmpty<T>(this IEnumerable<T?> source)
        where T : class
    {
        return source
            .Where(item => item is not null && 
                          (item is not string str || !string.IsNullOrWhiteSpace(str)))
            .Cast<T>();
    }
}