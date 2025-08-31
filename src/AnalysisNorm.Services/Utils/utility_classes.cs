// === AnalysisNorm.Services/Utils/utility_classes.cs ===
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
/// ИСПРАВЛЕНО: Устранено дублирование поля _logger
/// Соответствует Python read_text функциональности с .NET 9 оптимизациями
/// </summary>
public class FileEncodingDetector : IFileEncodingDetector
{
    private readonly ILogger<FileEncodingDetector> _encodingDetectorLogger; // Уникальное имя для предотвращения конфликтов
    private readonly string[] _supportedEncodings;

    // Compiled patterns for performance-critical operations
    private static readonly Regex ReplacementCharPattern = new(@"[\uFFFD�]", RegexOptions.Compiled);

    public FileEncodingDetector(ILogger<FileEncodingDetector> logger, IOptions<ApplicationSettings>? settings = null)
    {
        _encodingDetectorLogger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Регистрируем кодировки для поддержки cp1251
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Fallback to defaults if settings unavailable (defensive programming)
        _supportedEncodings = settings?.Value?.SupportedEncodings ??
                             new[] { "utf-8", "windows-1251", "koi8-r", "ascii", "iso-8859-1" };
    }

    /// <summary>
    /// Определяет кодировку файла методом последовательных проб
    /// Enhanced with statistical analysis для повышения точности
    /// </summary>
    public async Task<string> DetectEncodingAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _encodingDetectorLogger.LogWarning("Файл не найден для детекции кодировки: {FilePath}", filePath);
            return "utf-8"; // Default fallback
        }

        try
        {
            // Читаем первые 8KB для анализа (достаточно для большинства случаев)
            const int sampleSize = 8192;
            var buffer = new byte[sampleSize];

            await using var fileStream = File.OpenRead(filePath);
            var bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, sampleSize));
            var sampleBytes = buffer.AsSpan(0, bytesRead);

            // Пробуем кодировки в порядке вероятности для русских текстов
            foreach (var encodingName in _supportedEncodings)
            {
                try
                {
                    var encoding = GetEncoding(encodingName);
                    var testContent = encoding.GetString(sampleBytes);

                    if (!HasDecodingErrors(testContent))
                    {
                        _encodingDetectorLogger.LogTrace("Детектирована кодировка {Encoding} для файла {FilePath}",
                            encodingName, filePath);
                        return encodingName;
                    }
                }
                catch (Exception ex)
                {
                    _encodingDetectorLogger.LogTrace(ex, "Ошибка при проверке кодировки {Encoding}", encodingName);
                    continue;
                }
            }

            _encodingDetectorLogger.LogWarning("Не удалось определить кодировку для {FilePath}, используется UTF-8", filePath);
            return "utf-8";
        }
        catch (Exception ex)
        {
            _encodingDetectorLogger.LogError(ex, "Критическая ошибка детекции кодировки для файла {FilePath}", filePath);
            return "utf-8";
        }
    }

    /// <summary>
    /// Читает файл с автоматическим определением кодировки
    /// Optimized single-pass reading для производительности
    /// </summary>
    public async Task<string> ReadTextWithEncodingDetectionAsync(string filePath)
    {
        try
        {
            var encodingName = await DetectEncodingAsync(filePath);
            var encoding = GetEncoding(encodingName);
            var content = await File.ReadAllTextAsync(filePath, encoding);

            _encodingDetectorLogger.LogTrace("Файл {FilePath} успешно прочитан с кодировкой {Encoding}",
                filePath, encodingName);

            return content;
        }
        catch (Exception ex)
        {
            _encodingDetectorLogger.LogError(ex, "Ошибка чтения файла {FilePath}", filePath);
            return string.Empty;
        }
    }

    /// <summary>
    /// Получает Encoding по имени с поддержкой всех необходимых кодировок
    /// Includes comprehensive encoding support для российских данных
    /// </summary>
    public Encoding GetEncoding(string encodingName)
    {
        try
        {
            return encodingName.ToLowerInvariant() switch
            {
                "utf-8" or "utf8" => Encoding.UTF8,
                "windows-1251" or "cp1251" or "1251" => Encoding.GetEncoding("windows-1251"),
                "koi8-r" or "koi8r" => Encoding.GetEncoding("koi8-r"),
                "windows-1252" or "cp1252" or "1252" => Encoding.GetEncoding("windows-1252"),
                "ascii" => Encoding.ASCII,
                _ => throw new NotSupportedException($"Неподдерживаемая кодировка: {encodingName}")
            };
        }
        catch (Exception ex)
        {
            _encodingDetectorLogger.LogError(ex, "Ошибка создания кодировки {Encoding}", encodingName);
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
/// ИСПРАВЛЕНО: Устранено дублирование поля _logger
/// Соответствует normalize_text и safe_* функциям из Python utils.py
/// </summary>
public class TextNormalizer : ITextNormalizer
{
    private readonly ILogger<TextNormalizer> _textNormalizerLogger; // Уникальное имя для предотвращения конфликтов

    // Compiled regex patterns for maximum performance
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex HtmlTagsPattern = new(@"<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex NonBreakingSpacePattern = new(@"[\u00A0\u2007\u202F]", RegexOptions.Compiled);
    private static readonly Regex LettersAndDigitsRegex = new(@"[^А-ЯA-Z0-9]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumericPattern = new(@"[\d\s,.\-+]+", RegexOptions.Compiled);
    private static readonly Regex LocomotiveSeriesPattern = new(@"[^А-ЯA-Z0-9\-]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public TextNormalizer(ILogger<TextNormalizer> logger)
    {
        _textNormalizerLogger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            _textNormalizerLogger.LogWarning(ex, "Ошибка нормализации текста, возвращаем исходный: {Text}",
                text?.Substring(0, Math.Min(50, text?.Length ?? 0)));
            return text?.Trim() ?? string.Empty;
        }
    }

    /// <summary>
    /// Безопасное преобразование к decimal с расширенной логикой парсинга
    /// ИСПРАВЛЕНО: Убран некорректный оператор ?? с non-nullable типами
    /// </summary>
    public decimal SafeDecimal(object? input)
    {
        if (input == null) return 0m;

        try
        {
            // Direct decimal
            if (input is decimal dec) return dec;

            // String parsing with enhanced logic
            if (input is string str)
            {
                if (string.IsNullOrWhiteSpace(str)) return 0m;

                // Remove HTML entities and normalize
                var cleaned = NormalizeText(str);

                // Remove non-numeric characters except comma, dot, minus, plus
                var numericOnly = NumericPattern.Match(cleaned);
                if (!numericOnly.Success) return 0m;

                var numericStr = numericOnly.Value.Trim();

                // Handle Russian decimal separator (comma)
                numericStr = numericStr.Replace(',', '.');

                // Parse using invariant culture for consistency
                if (decimal.TryParse(numericStr, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                                   CultureInfo.InvariantCulture, out decimal result))
                {
                    return result;
                }

                return 0m;
            }

            // Numeric types conversion
            return input switch
            {
                double d => (decimal)d,
                float f => (decimal)f,
                int i => i,
                long l => l,
                _ => 0m
            };
        }
        catch (Exception ex)
        {
            _textNormalizerLogger.LogTrace(ex, "Не удалось преобразовать в decimal: {Input}", input);
            return 0m;
        }
    }

    /// <summary>
    /// Безопасное преобразование к int с улучшенной логикой
    /// ИСПРАВЛЕНО: Убрана ошибка CS0019 - правильная обработка nullable значений
    /// </summary>
    public int SafeInt(object? input)
    {
        if (input == null) return 0;

        try
        {
            if (input is int integer) return integer;
            if (input is string str)
            {
                var cleaned = NormalizeText(str);
                var numericOnly = NumericPattern.Match(cleaned);
                if (!numericOnly.Success) return 0;

                // ИСПРАВЛЕНО: убран некорректный оператор ?? между int и int
                var parts = numericOnly.Value.Replace(',', '.').Split('.');
                var integerPart = parts.Length > 0 ? parts[0].Trim() : string.Empty;

                if (string.IsNullOrEmpty(integerPart)) return 0;

                return int.TryParse(integerPart, out int result) ? result : 0;
            }

            return Convert.ToInt32(input);
        }
        catch (Exception ex)
        {
            _textNormalizerLogger.LogTrace(ex, "Не удалось преобразовать в int: {Input}", input);
            return 0;
        }
    }

    /// <summary>
    /// Безопасное преобразование к DateTime
    /// Supports multiple Russian date formats
    /// </summary>
    public DateTime? SafeDateTime(object? input)
    {
        if (input == null) return null;

        try
        {
            if (input is DateTime dateTime) return dateTime;
            if (input is string str)
            {
                var cleaned = NormalizeText(str);
                if (string.IsNullOrEmpty(cleaned)) return null;

                // Try common Russian formats
                var formats = new[] {
                    "dd.MM.yyyy", "dd/MM/yyyy", "yyyy-MM-dd",
                    "dd.MM.yyyy HH:mm", "dd/MM/yyyy HH:mm", "yyyy-MM-dd HH:mm:ss"
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(cleaned, format, CultureInfo.InvariantCulture,
                                             DateTimeStyles.None, out DateTime result))
                    {
                        return result;
                    }
                }

                // Fallback to general parsing
                return DateTime.TryParse(cleaned, out DateTime generalResult) ? generalResult : null;
            }

            return Convert.ToDateTime(input);
        }
        catch (Exception ex)
        {
            _textNormalizerLogger.LogTrace(ex, "Не удалось преобразовать в DateTime: {Input}", input);
            return null;
        }
    }

    /// <summary>
    /// Удаляет HTML теги из текста
    /// High-performance implementation using compiled regex
    /// </summary>
    public string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        try
        {
            return HtmlTagsPattern.Replace(html, " ").Trim();
        }
        catch (Exception ex)
        {
            _textNormalizerLogger.LogWarning(ex, "Ошибка удаления HTML тегов");
            return html;
        }
    }

    /// <summary>
    /// Нормализует пробелы в тексте
    /// Enhanced version handling all Unicode whitespace variants
    /// </summary>
    public string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        try
        {
            var normalized = text;

            // Replace various types of spaces
            normalized = normalized.Replace('\u00A0', ' ')  // Non-breaking space
                                  .Replace('\u2007', ' ')  // Figure space
                                  .Replace('\u202F', ' ')  // Narrow no-break space
                                  .Replace('\t', ' ')      // Tab
                                  .Replace('\r', ' ')      // Carriage return
                                  .Replace('\n', ' ');     // Line feed

            // Collapse multiple spaces
            normalized = MultipleSpacesRegex.Replace(normalized, " ");

            return normalized.Trim();
        }
        catch (Exception ex)
        {
            _textNormalizerLogger.LogWarning(ex, "Ошибка нормализации пробелов");
            return text?.Trim() ?? string.Empty;
        }
    }
}

#endregion

#region Application Settings

/// <summary>
/// Настройки приложения с полным набором конфигурационных параметров
/// ИСПРАВЛЕНО: Добавлены недостающие свойства для устранения ошибок компиляции
/// </summary>
public class ApplicationSettings
{
    /// <summary>
    /// Поддерживаемые кодировки файлов в порядке приоритета
    /// </summary>
    public string[] SupportedEncodings { get; set; } =
        { "utf-8", "windows-1251", "koi8-r", "ascii", "iso-8859-1" };

    /// <summary>
    /// Время истечения кэша в часах
    /// </summary>
    public int CacheExpirationHours { get; set; } = 24;

    /// <summary>
    /// Время истечения кэша в днях
    /// </summary>
    public int CacheExpirationDays { get; set; } = 7;

    /// <summary>
    /// Максимальный размер кэша в мегабайтах
    /// </summary>
    public int MaxCacheSizeMb { get; set; } = 1024;

    /// <summary>
    /// Строка подключения к базе данных
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Уровень логирования
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Временная папка для файлов
    /// </summary>
    public string TempDirectory { get; set; } = Path.GetTempPath();

    /// <summary>
    /// Максимальный размер загружаемых файлов в мегабайтах
    /// </summary>
    public int MaxFileSizeMb { get; set; } = 100;

    /// <summary>
    /// Таймаут для операций в секундах
    /// </summary>
    public int OperationTimeoutSeconds { get; set; } = 300;
}

#endregion