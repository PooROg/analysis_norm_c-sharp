using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace AnalysisNorm.Services.Utilities;

/// <summary>
/// Детектор кодировки файлов (единственная версия)
/// Соответствует Python read_text() функциональности
/// </summary>
public class FileEncodingDetector
{
    private readonly ILogger<FileEncodingDetector> _logger;
    private readonly List<Encoding> _supportedEncodings;

    public FileEncodingDetector(ILogger<FileEncodingDetector> logger)
    {
        _logger = logger;
        _supportedEncodings = new List<Encoding>
        {
            Encoding.GetEncoding("windows-1251"), // Приоритет для русских файлов
            Encoding.UTF8,
            Encoding.GetEncoding("windows-1252"),
            Encoding.ASCII
        };
    }

    /// <summary>
    /// Определяет кодировку файла
    /// </summary>
    public async Task<Encoding> DetectEncodingAsync(string filePath)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            
            // Проверяем BOM для UTF-8
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8;
            }

            // Тестируем каждую кодировку
            foreach (var encoding in _supportedEncodings)
            {
                try
                {
                    var text = encoding.GetString(bytes);
                    if (!HasDecodingErrors(text))
                    {
                        _logger.LogDebug("Detected encoding: {Encoding} for file: {FilePath}", 
                            encoding.EncodingName, filePath);
                        return encoding;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Encoding {Encoding} failed for {FilePath}: {Error}", 
                        encoding.EncodingName, filePath, ex.Message);
                }
            }

            _logger.LogWarning("Could not determine encoding for {FilePath}, using CP1251", filePath);
            return Encoding.GetEncoding("windows-1251");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting encoding for file: {FilePath}", filePath);
            return Encoding.UTF8;
        }
    }

    /// <summary>
    /// Читает текст файла с автоматическим определением кодировки
    /// </summary>
    public async Task<string> ReadTextWithEncodingDetectionAsync(string filePath)
    {
        try
        {
            var encoding = await DetectEncodingAsync(filePath);
            var text = await File.ReadAllTextAsync(filePath, encoding);
            
            // Дополнительная проверка на ошибки декодирования
            if (HasDecodingErrors(text))
            {
                _logger.LogWarning("Decoding errors detected, trying fallback encodings for {FilePath}", filePath);
                
                // Пробуем другие кодировки
                foreach (var fallbackEncoding in _supportedEncodings)
                {
                    try
                    {
                        var fallbackText = await File.ReadAllTextAsync(filePath, fallbackEncoding);
                        if (!HasDecodingErrors(fallbackText))
                        {
                            _logger.LogInformation("Successfully read {FilePath} with {Encoding}", 
                                filePath, fallbackEncoding.EncodingName);
                            return fallbackText;
                        }
                    }
                    catch
                    {
                        // Продолжаем с следующей кодировкой
                    }
                }
            }

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Получает кодировку по имени
    /// </summary>
    public static Encoding GetEncoding(string encodingName)
    {
        return encodingName.ToLowerInvariant() switch
        {
            "cp1251" or "windows-1251" => Encoding.GetEncoding("windows-1251"),
            "utf-8" or "utf8" => Encoding.UTF8,
            "ascii" => Encoding.ASCII,
            _ => Encoding.UTF8
        };
    }

    /// <summary>
    /// Проверяет наличие ошибок декодирования
    /// </summary>
    private static bool HasDecodingErrors(string text)
    {
        return text.Contains('\uFFFD') || text.Contains("?");
    }
}

/// <summary>
/// Нормализатор текста (единственная версия)
/// Соответствует Python normalize_text и safe_* функциям
/// </summary>
public class TextNormalizer
{
    private readonly ILogger<TextNormalizer> _logger;

    public TextNormalizer(ILogger<TextNormalizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Нормализует текст (удаляет лишние пробелы, приводит к стандартному виду)
    /// </summary>
    public string NormalizeText(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        try
        {
            // Удаляем HTML теги если есть
            var text = Regex.Replace(input, @"<[^>]+>", string.Empty);
            
            // Заменяем множественные пробелы на одинарные
            text = Regex.Replace(text, @"\s+", " ");
            
            // Убираем пробелы в начале и конце
            text = text.Trim();
            
            // Заменяем неразрывные пробелы на обычные
            text = text.Replace('\u00A0', ' ');
            text = text.Replace('\u2009', ' ');
            
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error normalizing text: {Input}", input.Substring(0, Math.Min(50, input.Length)));
            return input.Trim();
        }
    }

    /// <summary>
    /// Безопасное извлечение decimal значения (аналог Python safe_decimal)
    /// </summary>
    public decimal SafeDecimal(object? value, decimal defaultValue = 0)
    {
        if (value == null)
            return defaultValue;

        try
        {
            var stringValue = NormalizeText(value.ToString() ?? string.Empty);
            
            if (string.IsNullOrWhiteSpace(stringValue))
                return defaultValue;

            // Заменяем запятые на точки для десятичных разделителей
            stringValue = stringValue.Replace(',', '.');
            
            // Удаляем все символы кроме цифр, точки и минуса
            stringValue = Regex.Replace(stringValue, @"[^\d\.\-]", string.Empty);
            
            if (string.IsNullOrWhiteSpace(stringValue))
                return defaultValue;

            if (decimal.TryParse(stringValue, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            _logger.LogWarning("Could not parse decimal value: {Value}, using default: {Default}", 
                value, defaultValue);
            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing decimal value: {Value}", value);
            return defaultValue;
        }
    }

    /// <summary>
    /// Безопасное извлечение integer значения (аналог Python safe_int)
    /// </summary>
    public int SafeInt(object? value, int defaultValue = 0)
    {
        if (value == null)
            return defaultValue;

        try
        {
            var decimalValue = SafeDecimal(value, defaultValue);
            return (int)Math.Round(decimalValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing int value: {Value}", value);
            return defaultValue;
        }
    }

    /// <summary>
    /// Безопасное извлечение строкового значения
    /// </summary>
    public string SafeString(object? value, string defaultValue = "")
    {
        if (value == null)
            return defaultValue;

        try
        {
            return NormalizeText(value.ToString() ?? defaultValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting to string: {Value}", value);
            return defaultValue;
        }
    }

    /// <summary>
    /// Безопасное извлечение DateTime значения
    /// </summary>
    public DateTime SafeDateTime(object? value, DateTime defaultValue = default)
    {
        if (value == null)
            return defaultValue == default ? DateTime.Now : defaultValue;

        try
        {
            var stringValue = SafeString(value);
            
            if (string.IsNullOrWhiteSpace(stringValue))
                return defaultValue == default ? DateTime.Now : defaultValue;

            // Пробуем различные форматы дат
            var formats = new[]
            {
                "dd.MM.yyyy",
                "dd/MM/yyyy", 
                "yyyy-MM-dd",
                "MM/dd/yyyy",
                "dd.MM.yyyy HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss"
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(stringValue, format, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    System.Globalization.DateTimeStyles.None, out var result))
                {
                    return result;
                }
            }

            if (DateTime.TryParse(stringValue, out var genericResult))
            {
                return genericResult;
            }

            _logger.LogWarning("Could not parse DateTime value: {Value}", value);
            return defaultValue == default ? DateTime.Now : defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing DateTime value: {Value}", value);
            return defaultValue == default ? DateTime.Now : defaultValue;
        }
    }
}

/// <summary>
/// Константы приложения
/// </summary>
public static class Constants
{
    // Пороги отклонений (соответствует Python StatusClassifier)
    public const decimal ECONOMY_STRONG_THRESHOLD = -15m;
    public const decimal ECONOMY_MEDIUM_THRESHOLD = -10m;
    public const decimal ECONOMY_WEAK_THRESHOLD = -5m;
    public const decimal NORMAL_LOWER_THRESHOLD = -5m;
    public const decimal NORMAL_UPPER_THRESHOLD = 5m;
    public const decimal OVERRUN_WEAK_THRESHOLD = 10m;
    public const decimal OVERRUN_MEDIUM_THRESHOLD = 15m;

    // Форматы файлов
    public static readonly string[] SUPPORTED_HTML_EXTENSIONS = { ".html", ".htm" };
    public static readonly string[] SUPPORTED_EXCEL_EXTENSIONS = { ".xlsx", ".xls" };

    // Настройки базы данных
    public const string DEFAULT_DATABASE_NAME = "analysis_norm.db";
    public const int DEFAULT_CACHE_EXPIRATION_HOURS = 24;
    public const int DEFAULT_MAX_ROUTES_PER_ANALYSIS = 50000;

    // Настройки интерполяции
    public const double MIN_INTERPOLATION_POINTS = 3;
    public const double MAX_INTERPOLATION_RANGE = 1000.0;
    
    // Настройки экспорта
    public const int DEFAULT_CHART_WIDTH = 1200;
    public const int DEFAULT_CHART_HEIGHT = 800;
    public const string DEFAULT_EXPORT_DATE_FORMAT = "dd.MM.yyyy";
}