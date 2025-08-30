// === src/AnalysisNorm.Services/Utils/utility_classes.cs ===
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Объединенный детектор кодировки файлов
/// Комбинирует лучшие части из Utils/utility_classes.cs и Implementation/service_implementations.cs
/// Соответствует Python read_text функциональности с enterprise улучшениями
/// </summary>
public class FileEncodingDetector : IFileEncodingDetector
{
    private readonly ILogger<FileEncodingDetector> _logger;
    private readonly string[] _supportedEncodings;

    public FileEncodingDetector(ILogger<FileEncodingDetector> logger, IOptions<ApplicationSettings>? settings = null)
    {
        _logger = logger;
        
        // Используем настройки если доступны, иначе значения по умолчанию
        _supportedEncodings = settings?.Value?.SupportedEncodings ?? 
                             new[] { "cp1251", "utf-8", "utf-8-sig" };
        
        // Регистрируем дополнительные кодировки для поддержки cp1251
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Определяет кодировку файла методом проб (как в Python read_text)
    /// Приоритет: cp1251 -> utf-8 -> utf-8-sig
    /// </summary>
    public async Task<string> DetectEncodingAsync(string filePath)
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
                if (encoding == null) continue;

                using var reader = new StreamReader(filePath, encoding);
                
                // Читаем первые 1000 символов для анализа (больше чем в простой версии)
                var buffer = new char[1000];
                var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                
                if (charsRead > 0)
                {
                    var sample = new string(buffer, 0, charsRead);
                    
                    // Используем улучшенную проверку из Implementation версии
                    if (!HasDecodingErrors(sample))
                    {
                        _logger.LogDebug("Файл {FilePath} успешно декодирован с кодировкой {Encoding}", 
                            Path.GetFileName(filePath), encodingName);
                        return encodingName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Не удалось декодировать файл {FilePath} с кодировкой {Encoding}", 
                    Path.GetFileName(filePath), encodingName);
            }
        }

        _logger.LogWarning("Не удалось определить кодировку файла {FilePath}, используем UTF-8", 
            Path.GetFileName(filePath));
        return "utf-8";
    }

    /// <summary>
    /// Читает файл с автоматическим определением кодировки
    /// Полный аналог read_text из Python с error handling
    /// </summary>
    public async Task<string> ReadTextWithEncodingDetectionAsync(string filePath)
    {
        var detectedEncoding = await DetectEncodingAsync(filePath);
        var encoding = GetEncoding(detectedEncoding);
        
        if (encoding == null)
        {
            throw new NotSupportedException($"Неподдерживаемая кодировка: {detectedEncoding}");
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, encoding);
            _logger.LogTrace("Файл {FilePath} прочитан успешно ({Size:N0} символов, кодировка: {Encoding})", 
                Path.GetFileName(filePath), content.Length, detectedEncoding);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка чтения файла {FilePath} с кодировкой {Encoding}", 
                Path.GetFileName(filePath), detectedEncoding);
            
            // Fallback: последняя попытка с UTF-8 и игнорированием ошибок
            _logger.LogWarning("Принудительное чтение файла {FilePath} с UTF-8", Path.GetFileName(filePath));
            return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Получает объект Encoding по имени
    /// Улучшенная версия из Implementation
    /// </summary>
    public Encoding GetEncoding(string encodingName)
    {
        try
        {
            return encodingName.ToLower() switch
            {
                "cp1251" or "windows-1251" => Encoding.GetEncoding("windows-1251"),
                "utf-8" => new UTF8Encoding(false), // Без BOM
                "utf-8-sig" => new UTF8Encoding(true), // С BOM
                "windows-1252" => Encoding.GetEncoding("windows-1252"),
                _ => Encoding.UTF8 // Fallback вместо null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания кодировки {Encoding}", encodingName);
            return Encoding.UTF8; // Безопасный fallback
        }
    }

    /// <summary>
    /// Проверяет наличие ошибок декодирования
    /// Улучшенная версия из Implementation с дополнительными проверками
    /// </summary>
    public bool HasDecodingErrors(string content)
    {
        if (string.IsNullOrEmpty(content)) return true;

        // Проверяем на replacement characters (основная проверка)
        if (content.Contains('�') || content.Contains('\uFFFD'))
            return true;

        // Проверяем на характерные ошибки cp1251 -> utf-8 (из Utils версии)
        if (content.Contains("Ð") && content.Contains("¡"))
            return true;

        // Проверяем процент управляющих символов (из Implementation версии)
        var controlCharCount = content.Count(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');
        if (controlCharCount > content.Length * 0.1) // Больше 10% управляющих символов
            return true;

        // Проверяем процент printable символов
        var printableCount = content.Count(c => !char.IsControl(c) || char.IsWhiteSpace(c));
        return printableCount < content.Length * 0.7; // Меньше 70% печатных символов
    }
}

/// <summary>
/// Нормализатор текста и безопасные конверторы
/// Полная версия из Utils/utility_classes.cs с улучшениями
/// Соответствует normalize_text и safe_* функциям из Python utils.py
/// </summary>
public class TextNormalizer : ITextNormalizer
{
    private readonly ILogger<TextNormalizer> _logger;

    // Compiled regex for performance
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex LettersAndDigitsRegex = new(@"[^А-ЯA-Z0-9]", RegexOptions.Compiled);

    public TextNormalizer(ILogger<TextNormalizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Нормализует текст - убирает лишние пробелы, HTML entities
    /// Точный аналог normalize_text из Python utils.py
    /// </summary>
    public string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        try
        {
            // Заменяем неразрывные пробелы и HTML entities
            text = text.Replace('\u00A0', ' ')       // &nbsp;
                      .Replace("&nbsp;", " ")
                      .Replace('\u00AD', ' ')        // soft hyphen
                      .Replace('\t', ' ')            // табуляции
                      .Replace('\r', ' ')            // возврат каретки
                      .Replace('\n', ' ');           // новая строка

            // Убираем множественные пробелы (аналог re.sub(r'\s+', ' ', text))
            text = MultipleSpacesRegex.Replace(text, " ");

            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка нормализации текста, возвращаем исходный: {Text}", 
                text?.Substring(0, Math.Min(50, text.Length ?? 0)));
            return text?.Trim() ?? string.Empty;
        }
    }

    /// <summary>
    /// Безопасное преобразование к decimal
    /// Соответствует safe_float из Python utils.py
    /// </summary>
    public decimal SafeDecimal(object? input, decimal defaultValue = 0)
    {
        if (input == null) return defaultValue;

        try
        {
            return input switch
            {
                decimal d => d,
                double db => (decimal)db,
                float f => (decimal)f,
                int i => i,
                long l => l,
                string s when decimal.TryParse(s.Replace(',', '.'), 
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var result) => result,
                string s when decimal.TryParse(s.Replace('.', ','), 
                    NumberStyles.Float, CultureInfo.CurrentCulture, out var result) => result,
                _ => defaultValue
            };
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Не удалось преобразовать значение к decimal: {Input}", input);
            return defaultValue;
        }
    }

    /// <summary>
    /// Безопасное преобразование к int
    /// </summary>
    public int SafeInt(object? input, int defaultValue = 0)
    {
        if (input == null) return defaultValue;

        try
        {
            return input switch
            {
                int i => i,
                long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
                decimal d when d >= int.MinValue && d <= int.MaxValue => (int)d,
                double db when db >= int.MinValue && db <= int.MaxValue => (int)db,
                float f when f >= int.MinValue && f <= int.MaxValue => (int)f,
                string s when int.TryParse(s, out var result) => result,
                _ => defaultValue
            };
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Не удалось преобразовать значение к int: {Input}", input);
            return defaultValue;
        }
    }

    /// <summary>
    /// Безопасное преобразование к DateTime
    /// </summary>
    public DateTime SafeDateTime(object? input, DateTime defaultValue = default)
    {
        if (input == null) return defaultValue;

        try
        {
            return input switch
            {
                DateTime dt => dt,
                string s when DateTime.TryParse(s, out var result) => result,
                string s when DateTime.TryParseExact(s, "dd.MM.yyyy", 
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) => result,
                _ => defaultValue
            };
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Не удалось преобразовать значение к DateTime: {Input}", input);
            return defaultValue;
        }
    }

    /// <summary>
    /// Проверяет является ли значение пустым/null
    /// Соответствует is_empty из Python utils.py
    /// </summary>
    public bool IsEmpty(object? input)
    {
        if (input == null) return true;

        return input switch
        {
            string s => string.IsNullOrWhiteSpace(s) || 
                       s.Trim().ToLower() is "-" or "—" or "н/д" or "n/a" or "nan" or "none" or "null",
            decimal d => d == 0m,
            double db => db == 0.0,
            float f => f == 0.0f,
            int i => i == 0,
            _ => false
        };
    }

    /// <summary>
    /// Форматирует decimal с заданной точностью
    /// Соответствует format_number из Python utils.py
    /// </summary>
    public string FormatDecimal(object? value, int decimals = 1, string fallback = "N/A")
    {
        if (value == null) return fallback;

        try
        {
            var decimalValue = SafeDecimal(value);
            
            if (decimalValue == 0m && IsEmpty(value))
                return fallback;

            return decimalValue.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка форматирования decimal: {Value}", value);
            return fallback;
        }
    }

    /// <summary>
    /// Нормализует название серии локомотива
    /// Соответствует normalize_series из Python coefficients.py
    /// </summary>
    public string NormalizeLocomotiveSeries(string series)
    {
        if (string.IsNullOrEmpty(series)) 
            return string.Empty;
        
        try
        {
            // Убираем все кроме букв и цифр, приводим к верхнему регистру
            var normalized = LettersAndDigitsRegex.Replace(series.ToUpper(), string.Empty);
            return normalized;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка нормализации серии локомотива: {Series}", series);
            return series.Trim().ToUpper();
        }
    }

    /// <summary>
    /// Безопасное деление с проверкой на ноль
    /// </summary>
    public decimal SafeRatio(object? numerator, object? denominator, decimal defaultValue = 0m)
    {
        var num = SafeDecimal(numerator);
        var den = SafeDecimal(denominator);

        if (den == 0m)
        {
            _logger.LogTrace("Деление на ноль: {Numerator} / {Denominator}", numerator, denominator);
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

#region Collection Extensions

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

#endregion

#region Route Utilities

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
        if (value is null) return fallback;

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
}

#endregion

#region Analysis Constants

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

#endregion

#region Application Settings

/// <summary>
/// Настройки приложения
/// Объединенная версия из обоих файлов
/// </summary>
public class ApplicationSettings
{
    /// <summary>Поддерживаемые кодировки в порядке приоритета</summary>
    public string[] SupportedEncodings { get; set; } = { "cp1251", "utf-8", "utf-8-sig" };
    
    /// <summary>Максимальный размер файла для обработки (МБ)</summary>
    public int MaxFileSizeMB { get; set; } = 100;
    
    /// <summary>Минимальный порог работы</summary>
    public double MinWorkThreshold { get; set; } = 200.0;
    
    /// <summary>Допуск по умолчанию в процентах</summary>
    public double DefaultTolerancePercent { get; set; } = 5.0;
}

#endregion