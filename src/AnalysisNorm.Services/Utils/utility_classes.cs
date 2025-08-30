using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

#region File Encoding Detector

/// <summary>
/// Детектор кодировки файлов с улучшенной логикой распознавания
/// Комбинирует лучшие части из всех версий + enterprise-уровень обработки ошибок
/// Соответствует Python read_text функциональности с .NET 9 оптимизациями
/// </summary>
public class FileEncodingDetector : IFileEncodingDetector
{
    private readonly ILogger<FileEncodingDetector> _logger;
    private readonly string[] _supportedEncodings;

    // Compiled patterns for performance-critical operations
    private static readonly Regex ReplacementCharPattern = new(@"[\uFFFD�]", RegexOptions.Compiled);

    public FileEncodingDetector(ILogger<FileEncodingDetector> logger, IOptions<ApplicationSettings>? settings = null)
    {
        _logger = logger;

        // Fallback to defaults if settings unavailable (defensive programming)
        _supportedEncodings = settings?.Value?.SupportedEncodings ??
                             ["cp1251", "utf-8", "utf-8-sig"];

        // Register code page provider for CP1251 support (critical for Russian content)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Определяет кодировку файла методом проб с улучшенной эвристикой
    /// Приоритет: cp1251 -> utf-8 -> utf-8-sig (оптимален для российских данных)
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

                // Use larger buffer for better detection accuracy (1000 chars vs typical 256)
                using var reader = new StreamReader(filePath, encoding);
                var buffer = new char[1000];
                var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);

                if (charsRead > 0)
                {
                    var sample = new string(buffer, 0, charsRead);

                    // Enhanced decoding error detection with multiple heuristics
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

        // Graceful fallback with logging
        _logger.LogWarning("Не удалось определить кодировку файла {FilePath}, используем UTF-8",
            Path.GetFileName(filePath));
        return "utf-8";
    }

    /// <summary>
    /// Читает файл с автоматическим определением кодировки и error recovery
    /// Полный аналог read_text из Python с enterprise error handling
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

            // Last-resort fallback: UTF-8 with error tolerance
            _logger.LogWarning("Принудительное чтение файла {FilePath} с UTF-8", Path.GetFileName(filePath));
            return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Получает объект Encoding по имени с расширенной поддержкой кодировок
    /// Improved version with better error handling and encoding variants
    /// </summary>
    public Encoding GetEncoding(string encodingName)
    {
        try
        {
            return encodingName.ToLowerInvariant() switch
            {
                "cp1251" or "windows-1251" or "1251" => Encoding.GetEncoding("windows-1251"),
                "utf-8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
                "utf-8-sig" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: false),
                "windows-1252" or "1252" => Encoding.GetEncoding("windows-1252"),
                "ascii" => Encoding.ASCII,
                _ => throw new NotSupportedException($"Неподдерживаемая кодировка: {encodingName}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания кодировки {Encoding}", encodingName);
            throw;
        }
    }

    /// <summary>
    /// Проверяет наличие ошибок декодирования с использованием множественных эвристик
    /// Enhanced detection with statistical analysis and pattern matching
    /// </summary>
    public bool HasDecodingErrors(string content)
    {
        if (string.IsNullOrEmpty(content)) return true;

        // Check for Unicode replacement characters (primary indicator)
        if (ReplacementCharPattern.IsMatch(content))
            return true;

        // Statistical analysis: excessive control characters indicate wrong encoding
        var controlCharCount = content.Count(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');
        if (controlCharCount > content.Length * 0.05) // More than 5% control chars is suspicious
            return true;

        // Check for invalid UTF-8 sequences in supposedly decoded text
        var invalidSequences = content.Count(c => c == '\uFFFD');
        if (invalidSequences > 0)
            return true;

        // All heuristics passed
        return false;
    }
}

#endregion

#region Text Normalizer

/// <summary>
/// Нормализатор текста с полным набором utility функций
/// Объединяет лучшие части из всех версий + complete interface implementation
/// Соответствует normalize_text и safe_* функциям из Python utils.py
/// </summary>
public class TextNormalizer : ITextNormalizer
{
    private readonly ILogger<TextNormalizer> _logger;

    // Compiled regex patterns for maximum performance
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex HtmlTagsPattern = new(@"<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex NonBreakingSpacePattern = new(@"[\u00A0\u2007\u202F]", RegexOptions.Compiled);
    private static readonly Regex LettersAndDigitsRegex = new(@"[^А-ЯA-Z0-9]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumericPattern = new(@"[\d\s,.\-+]+", RegexOptions.Compiled);
    private static readonly Regex LocomotiveSeriesPattern = new(@"[^А-ЯA-Z0-9\-]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public TextNormalizer(ILogger<TextNormalizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Нормализует текст - убирает HTML теги, лишние пробелы, HTML entities
    /// Combines best practices from all implementations
    /// </summary>
    public string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        try
        {
            var normalized = text;

            // Remove HTML tags (from Implementation version)
            normalized = HtmlTagsPattern.Replace(normalized, " ");

            // Replace non-breaking spaces and special characters (from Utils version)
            normalized = normalized.Replace('\u00A0', ' ')       // &nbsp;
                                  .Replace("&nbsp;", " ")
                                  .Replace('\u00AD', ' ')        // soft hyphen
                                  .Replace('\t', ' ')            // tabs
                                  .Replace('\r', ' ')            // carriage return
                                  .Replace('\n', ' ');           // line feed

            // Additional non-breaking space variants
            normalized = NonBreakingSpacePattern.Replace(normalized, " ");

            // Normalize multiple spaces to single space
            normalized = MultipleSpacesRegex.Replace(normalized, " ");

            return normalized.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка нормализации текста, возвращаем исходный: {Text}",
                text?.Substring(0, Math.Min(50, text.Length ?? 0)));
            return text?.Trim() ?? string.Empty;
        }
    }

    /// <summary>
    /// Безопасное преобразование к decimal с расширенной логикой парсинга
    /// Enhanced version supporting both comma and dot decimal separators
    /// </summary>
    public decimal SafeDecimal(object? input, decimal defaultValue = 0)
    {
        if (input == null) return defaultValue;

        try
        {
            return input switch
            {
                decimal d => d,
                double db when !double.IsNaN(db) && !double.IsInfinity(db) => (decimal)db,
                float f when !float.IsNaN(f) && !float.IsInfinity(f) => (decimal)f,
                int i => i,
                long l => l,
                byte b => b,
                short s => s,
                string str => ParseDecimalString(str, defaultValue),
                _ => ConvertToDecimal(input, defaultValue)
            };
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Не удалось преобразовать '{Input}' в decimal, используем значение по умолчанию {Default}",
                input?.ToString()?.Substring(0, Math.Min(50, input.ToString()?.Length ?? 0)), defaultValue);
            return defaultValue;
        }
    }

    /// <summary>
    /// Безопасное преобразование к int с проверкой диапазона
    /// </summary>
    public int SafeInt(object? input, int defaultValue = 0)
    {
        if (input == null) return defaultValue;

        try
        {
            return input switch
            {
                int i => i,
                decimal d when d >= int.MinValue && d <= int.MaxValue => (int)d,
                double db when !double.IsNaN(db) && !double.IsInfinity(db) &&
                               db >= int.MinValue && db <= int.MaxValue => (int)db,
                float f when !float.IsNaN(f) && !float.IsInfinity(f) &&
                             f >= int.MinValue && f <= int.MaxValue => (int)f,
                long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
                byte b => b,
                short s => s,
                string str when int.TryParse(str.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) => result,
                _ => defaultValue
            };
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Не удалось преобразовать '{Input}' в int", input);
            return defaultValue;
        }
    }

    /// <summary>
    /// Безопасное преобразование к DateTime с множественными форматами
    /// </summary>
    public DateTime SafeDateTime(object? input, DateTime defaultValue = default)
    {
        if (input == null) return defaultValue;
        if (input is DateTime dt) return dt;

        try
        {
            var formats = new[]
            {
                "dd.MM.yyyy", "dd.MM.yyyy HH:mm:ss", "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss",
                "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd"
            };

            if (input is string str && !string.IsNullOrWhiteSpace(str))
            {
                if (DateTime.TryParseExact(str.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                    return result;

                if (DateTime.TryParse(str.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                    return result;
            }

            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Не удалось преобразовать '{Input}' в DateTime", input);
            return defaultValue;
        }
    }

    /// <summary>
    /// Проверяет является ли значение пустым с расширенным списком пустых значений
    /// </summary>
    public bool IsEmpty(object? input)
    {
        if (input == null) return true;

        if (input is string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return true;

            var normalized = str.Trim().ToLowerInvariant();
            var emptyValues = new[] { "-", "—", "н/д", "n/a", "nan", "none", "null", "n.a.", "не указано", "не определено" };

            return emptyValues.Contains(normalized);
        }

        // Check numeric types for zero/NaN
        return input switch
        {
            decimal d => d == 0,
            double db => double.IsNaN(db) || db == 0,
            float f => float.IsNaN(f) || f == 0,
            int i => i == 0,
            long l => l == 0,
            _ => false
        };
    }

    /// <summary>
    /// Форматирует decimal с заданной точностью и fallback значением
    /// </summary>
    public string FormatDecimal(object? value, int decimals = 1, string fallback = "N/A")
    {
        if (IsEmpty(value)) return fallback;

        try
        {
            var decimalValue = SafeDecimal(value, 0);
            return decimalValue.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Не удалось отформатировать значение '{Value}'", value);
            return fallback;
        }
    }

    /// <summary>
    /// Нормализует название серии локомотива (удаляет спецсимволы, приводит к верхнему регистру)
    /// </summary>
    public string NormalizeLocomotiveSeries(string series)
    {
        if (string.IsNullOrWhiteSpace(series))
            return string.Empty;

        try
        {
            // Remove special characters, keep only letters, digits, and hyphens
            var normalized = LocomotiveSeriesPattern.Replace(series.Trim(), "").ToUpperInvariant();

            // Normalize common variants
            return normalized switch
            {
                var s when s.StartsWith("ВЛ") => s.Replace("ВЛ", "ВЛ-"),
                var s when s.StartsWith("ТЭ") => s.Replace("ТЭ", "ТЭ-"),
                var s when s.StartsWith("ЭП") => s.Replace("ЭП", "ЭП-"),
                _ => normalized
            };
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Не удалось нормализовать серию локомотива '{Series}'", series);
            return series.Trim().ToUpperInvariant();
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Парсит строку в decimal с поддержкой различных форматов
    /// </summary>
    private decimal ParseDecimalString(string str, decimal defaultValue)
    {
        if (string.IsNullOrWhiteSpace(str)) return defaultValue;

        var trimmed = str.Trim();

        // Try parsing with comma as decimal separator (Russian format)
        if (decimal.TryParse(trimmed.Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;

        // Try parsing with dot as decimal separator
        if (decimal.TryParse(trimmed.Replace('.', ','),
            NumberStyles.Float, CultureInfo.GetCultureInfo("ru-RU"), out result))
            return result;

        // Try direct parsing
        if (decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            return result;

        return defaultValue;
    }

    /// <summary>
    /// Конвертирует произвольный объект в decimal
    /// </summary>
    private decimal ConvertToDecimal(object input, decimal defaultValue)
    {
        try
        {
            return Convert.ToDecimal(input, CultureInfo.InvariantCulture);
        }
        catch
        {
            return defaultValue;
        }
    }

    #endregion
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
/// Настройки приложения (единственная версия, объединяющая все конфигурации)
/// Removes duplication from service_implementations.cs and ServiceConfiguration.cs
/// </summary>
public class ApplicationSettings
{
    // Directory settings
    public string DataDirectory { get; set; } = "data";
    public string TempDirectory { get; set; } = "temp";
    public string ExportsDirectory { get; set; } = "exports";
    public string LogsDirectory { get; set; } = "logs";

    // File processing settings (аналог Python config.py)
    public string[] SupportedEncodings { get; set; } = ["cp1251", "utf-8", "utf-8-sig"];
    public int MaxFileSizeMB { get; set; } = 100;
    public int MaxTempFiles { get; set; } = 10;

    // Analysis settings
    public double DefaultTolerancePercent { get; set; } = 5.0;
    public double MinWorkThreshold { get; set; } = 200.0;

    // HTML Processing settings 
    public int HtmlProcessingTimeoutSeconds { get; set; } = 30;
    public int MaxConcurrentProcessing { get; set; } = 4;

    // Performance settings
    public bool EnableCaching { get; set; } = true;
    public int CacheExpirationMinutes { get; set; } = 60;
    public int MaxCacheEntries { get; set; } = 1000;

    // Logging settings
    public bool EnableVerboseLogging { get; set; } = false;
    public bool LogProcessingStatistics { get; set; } = true;
}

#endregion