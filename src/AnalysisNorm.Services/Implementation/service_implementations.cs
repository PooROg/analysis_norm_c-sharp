using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Утилитарные сервисы, которые не имеют отдельных файлов реализации
/// УДАЛЕНЫ все дублирующиеся классы: DataAnalysisService, NormInterpolationService, 
/// AnalysisCacheService, NormStorageService - они уже реализованы в отдельных файлах
/// </summary>

/// <summary>
/// Нормализатор текста и утилиты для безопасного преобразования
/// Соответствует normalize_text и safe_* функциям из Python html_route_processor.py
/// </summary>
public class TextNormalizer : ITextNormalizer
{
    private readonly ILogger<TextNormalizer> _logger;
    private readonly ApplicationSettings _settings;

    // Compiled regex patterns for performance
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex NonBreakingSpacePattern = new(@"[\u00A0\u2007\u202F]", RegexOptions.Compiled);
    private static readonly Regex NumberPattern = new(@"[\d,.-]+", RegexOptions.Compiled);
    private static readonly Regex HtmlTagsPattern = new(@"<[^>]*>", RegexOptions.Compiled);

    public TextNormalizer(ILogger<TextNormalizer> logger, IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    /// <summary>
    /// Нормализует текст - убирает HTML теги, лишние пробелы, неразрывные пробелы
    /// Полное соответствие normalize_text из Python
    /// </summary>
    public string NormalizeText(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        try
        {
            var text = input;

            // Убираем HTML теги
            text = HtmlTagsPattern.Replace(text, " ");

            // Заменяем неразрывные пробелы на обычные
            text = NonBreakingSpacePattern.Replace(text, " ");

            // Нормализуем множественные пробелы
            text = WhitespacePattern.Replace(text, " ");

            // Trim
            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка нормализации текста");
            return input; // Возвращаем исходный текст в случае ошибки
        }
    }

    /// <summary>
    /// Безопасное преобразование значения в decimal (аналог safe_float из Python)
    /// </summary>
    public decimal SafeDecimal(object? value, decimal defaultValue = 0)
    {
        if (value == null) 
            return defaultValue;

        try
        {
            var stringValue = value.ToString()?.Trim();
            if (string.IsNullOrEmpty(stringValue)) 
                return defaultValue;

            // Нормализуем текст
            stringValue = NormalizeText(stringValue);
            
            // Заменяем запятую на точку, убираем пробелы
            stringValue = stringValue.Replace(",", ".").Replace(" ", "");

            // Извлекаем числовое значение
            var match = NumberPattern.Match(stringValue);
            if (match.Success && decimal.TryParse(match.Value, NumberStyles.Float, 
                CultureInfo.InvariantCulture, out var result))
            {
                return Math.Abs(result); // Только положительные значения как в Python
            }

            _logger.LogTrace("Не удалось преобразовать '{Value}' в decimal, используем значение по умолчанию {Default}", 
                stringValue, defaultValue);
            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка преобразования '{Value}' в decimal", value);
            return defaultValue;
        }
    }

    /// <summary>
    /// Безопасное преобразование значения в int (аналог safe_int из Python)
    /// </summary>
    public int SafeInteger(object? value, int defaultValue = 0)
    {
        if (value == null) 
            return defaultValue;

        try
        {
            var stringValue = value.ToString()?.Trim();
            if (string.IsNullOrEmpty(stringValue)) 
                return defaultValue;

            // Нормализуем текст
            stringValue = NormalizeText(stringValue);
            
            // Убираем нечисловые символы в начале и конце
            var numberMatch = Regex.Match(stringValue, @"\d+");
            if (numberMatch.Success && int.TryParse(numberMatch.Value, out var result))
            {
                return result;
            }

            _logger.LogTrace("Не удалось преобразовать '{Value}' в int, используем значение по умолчанию {Default}", 
                stringValue, defaultValue);
            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Ошибка преобразования '{Value}' в int", value);
            return defaultValue;
        }
    }

    /// <summary>
    /// Проверяет, является ли значение "пустым" (аналог проверок на None/NaN в Python)
    /// </summary>
    public bool IsEmptyValue(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return true;

        var normalized = input.Trim().ToLower();
        var emptyValues = new[] { "-", "—", "н/д", "n/a", "nan", "none", "null" };
        
        return emptyValues.Contains(normalized);
    }
}

/// <summary>
/// Детектор кодировки файлов
/// Соответствует read_text с fallback кодировками из Python
/// </summary>
public class FileEncodingDetector : IFileEncodingDetector
{
    private readonly ILogger<FileEncodingDetector> _logger;
    private readonly string[] _supportedEncodings;

    public FileEncodingDetector(ILogger<FileEncodingDetector> logger, IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _supportedEncodings = settings.Value.SupportedEncodings ?? new[] { "cp1251", "utf-8", "utf-8-sig" };
        
        // Регистрируем дополнительные кодировки
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
                
                // Читаем первые 1000 символов для анализа
                var buffer = new char[1000];
                var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                
                if (charsRead > 0)
                {
                    var sample = new string(buffer, 0, charsRead);
                    
                    // Проверяем что декодирование прошло успешно
                    if (!HasDecodingErrors(sample))
                    {
                        _logger.LogDebug("Файл {FilePath} успешно декодирован с кодировкой {Encoding}", 
                            filePath, encodingName);
                        return encodingName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Не удалось декодировать файл {FilePath} с кодировкой {Encoding}", 
                    filePath, encodingName);
            }
        }

        _logger.LogWarning("Не удалось определить кодировку файла {FilePath}, используем UTF-8", filePath);
        return "utf-8";
    }

    /// <summary>
    /// Читает файл с автоматическим определением кодировки
    /// </summary>
    public async Task<string> ReadTextWithEncodingDetectionAsync(string filePath)
    {
        var detectedEncoding = await DetectEncodingAsync(filePath);
        var encoding = GetEncoding(detectedEncoding);
        
        try
        {
            var content = await File.ReadAllTextAsync(filePath, encoding!);
            _logger.LogTrace("Файл {FilePath} прочитан успешно ({Size:N0} символов, кодировка: {Encoding})", 
                filePath, content.Length, detectedEncoding);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка чтения файла {FilePath} с кодировкой {Encoding}", filePath, detectedEncoding);
            throw;
        }
    }

    /// <summary>
    /// Получает объект Encoding по имени
    /// </summary>
    private Encoding? GetEncoding(string encodingName)
    {
        try
        {
            return encodingName.ToLower() switch
            {
                "cp1251" or "windows-1251" => Encoding.GetEncoding("windows-1251"),
                "utf-8" => new UTF8Encoding(false),
                "utf-8-sig" => new UTF8Encoding(true),
                "windows-1252" => Encoding.GetEncoding("windows-1252"),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания кодировки {Encoding}", encodingName);
            return null;
        }
    }

    /// <summary>
    /// Проверяет наличие ошибок декодирования
    /// </summary>
    private bool HasDecodingErrors(string content)
    {
        if (string.IsNullOrEmpty(content)) return true;

        // Проверяем на replacement characters
        if (content.Contains('�') || content.Contains('\uFFFD'))
            return true;

        // Проверяем количество управляющих символов
        var controlCharCount = content.Count(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');
        return controlCharCount > content.Length * 0.1; // Больше 10% управляющих символов - плохо
    }
}

/// <summary>
/// Вспомогательные классы для конфигурации
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