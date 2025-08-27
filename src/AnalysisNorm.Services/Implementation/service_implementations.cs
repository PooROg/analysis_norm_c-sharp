using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Реализация детектора кодировки файлов
/// Соответствует read_text из Python html_route_processor.py
/// </summary>
public class FileEncodingDetector : IFileEncodingDetector
{
    private readonly ILogger<FileEncodingDetector> _logger;
    private readonly ApplicationSettings _settings;

    public FileEncodingDetector(ILogger<FileEncodingDetector> logger, IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
        
        // Регистрируем дополнительные кодировки (включая cp1251)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Определяет кодировку файла методом проб
    /// Приоритет: cp1251 -> utf-8 -> utf-8-sig (как в Python)
    /// </summary>
    public async Task<string> DetectEncodingAsync(string filePath)
    {
        foreach (var encodingName in _settings.SupportedEncodings)
        {
            try
            {
                var encoding = GetEncodingByName(encodingName);
                using var reader = new StreamReader(filePath, encoding);
                
                // Читаем первые 1000 символов для проверки
                var buffer = new char[1000];
                var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                
                // Проверяем что декодирование прошло успешно
                var text = new string(buffer, 0, charsRead);
                if (!HasDecodingErrors(text))
                {
                    _logger.LogDebug("Файл {FilePath} успешно декодирован с кодировкой {Encoding}", 
                        filePath, encodingName);
                    return encodingName;
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
        var encoding = GetEncodingByName(detectedEncoding);
        
        try
        {
            var content = await File.ReadAllTextAsync(filePath, encoding);
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
    private static Encoding GetEncodingByName(string encodingName)
    {
        return encodingName.ToLower() switch
        {
            "cp1251" => Encoding.GetEncoding("windows-1251"),
            "utf-8" => new UTF8Encoding(false),
            "utf-8-sig" => new UTF8Encoding(true),
            _ => Encoding.UTF8
        };
    }

    /// <summary>
    /// Проверяет есть ли ошибки декодирования в тексте
    /// </summary>
    private static bool HasDecodingErrors(string text)
    {
        // Ищем типичные признаки неправильного декодирования
        var errorIndicators = new[]
        {
            "�", // Replacement character
            "Ã", "Â", "Ñ", // Типичные артефакты неправильного декодирования cp1251
            "\uFFFD" // Unicode replacement character
        };

        return errorIndicators.Any(indicator => text.Contains(indicator));
    }
}

/// <summary>
/// Нормализатор текста
/// Соответствует normalize_text + safe_* функциям из Python utils.py
/// </summary>
public class TextNormalizer : ITextNormalizer
{
    private readonly ILogger<TextNormalizer> _logger;
    
    // Compiled regex patterns for performance
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex NonBreakingSpacePattern = new(@"[\xa0\u00a0\u2007\u202f]", RegexOptions.Compiled);
    
    public TextNormalizer(ILogger<TextNormalizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Нормализует текст: убирает лишние пробелы, nbsp и т.д.
    /// Соответствует normalize_text из Python
    /// </summary>
    public string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Заменяем различные виды неразрывных пробелов обычными пробелами
        text = NonBreakingSpacePattern.Replace(text, " ");
        
        // Убираем HTML entities
        text = text.Replace("&nbsp;", " ")
                  .Replace("&lt;", "<")
                  .Replace("&gt;", ">")
                  .Replace("&amp;", "&")
                  .Replace("&quot;", "\"");
        
        // Нормализуем пробелы (множественные пробелы заменяем одним)
        text = WhitespacePattern.Replace(text, " ");
        
        return text.Trim();
    }

    /// <summary>
    /// Безопасно конвертирует в decimal
    /// Соответствует safe_float из Python
    /// </summary>
    public decimal SafeDecimal(object? value, decimal defaultValue = 0m)
    {
        if (value == null) 
            return defaultValue;

        try
        {
            var stringValue = value.ToString()?.Trim();
            if (string.IsNullOrEmpty(stringValue)) 
                return defaultValue;

            // Убираем лишние пробелы и символы
            stringValue = NormalizeText(stringValue);
            
            // Заменяем запятые точками для decimal парсинга
            stringValue = stringValue.Replace(',', '.');
            
            // Убираем лишние точки в конце
            if (stringValue.EndsWith('.'))
                stringValue = stringValue[..^1];

            // Пытаемся парсить как decimal
            if (decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            // Если не получилось, пытаемся извлечь первое число из строки
            var numberMatch = Regex.Match(stringValue, @"(\d+(?:\.\d+)?)");
            if (numberMatch.Success && 
                decimal.TryParse(numberMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var extractedResult))
            {
                return extractedResult;
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
    /// Безопасно конвертирует в int
    /// Соответствует safe_int из Python
    /// </summary>
    public int SafeInt(object? value, int defaultValue = 0)
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
}

/// <summary>
/// Базовый сервис анализа данных
/// Соответствует InteractiveNormsAnalyzer из Python analyzer.py
/// </summary>
public class DataAnalysisService : IDataAnalysisService
{
    private readonly ILogger<DataAnalysisService> _logger;
    private readonly INormStorageService _normStorage;
    private readonly INormInterpolationService _interpolationService;
    private readonly IAnalysisCacheService _cacheService;

    public DataAnalysisService(
        ILogger<DataAnalysisService> logger,
        INormStorageService normStorage,
        INormInterpolationService interpolationService,
        IAnalysisCacheService cacheService)
    {
        _logger = logger;
        _normStorage = normStorage;
        _interpolationService = interpolationService;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Анализирует участок с построением результатов для визуализации
    /// </summary>
    public async Task<AnalysisResult> AnalyzeSectionAsync(
        string sectionName,
        string? normId = null,
        bool singleSectionOnly = false,
        AnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Начинаем анализ участка {SectionName} (норма: {NormId}, только участок: {SingleSection})", 
            sectionName, normId ?? "все", singleSectionOnly);

        var analysisResult = new AnalysisResult
        {
            SectionName = sectionName,
            NormId = normId,
            SingleSectionOnly = singleSectionOnly,
            UseCoefficients = options?.UseCoefficients ?? false,
            CreatedAt = DateTime.UtcNow
        };

        // Генерируем хэш для кэширования
        analysisResult.GenerateAnalysisHash();

        try
        {
            // Проверяем кэш
            var cachedResult = await _cacheService.GetCachedAnalysisAsync(analysisResult.AnalysisHash!);
            if (cachedResult != null)
            {
                _logger.LogDebug("Найден кэшированный результат анализа для {SectionName}", sectionName);
                return cachedResult;
            }

            // Выполняем анализ
            await PerformAnalysisAsync(analysisResult, options, cancellationToken);

            // Сохраняем в кэш
            await _cacheService.SaveAnalysisToCacheAsync(analysisResult);

            _logger.LogInformation("Анализ участка {SectionName} завершен успешно", sectionName);
            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка анализа участка {SectionName}", sectionName);
            analysisResult.ErrorMessage = ex.Message;
            return analysisResult;
        }
    }

    /// <summary>
    /// Выполняет основную логику анализа
    /// </summary>
    private async Task PerformAnalysisAsync(AnalysisResult analysisResult, AnalysisOptions? options, CancellationToken cancellationToken)
    {
        // Здесь будет реализована основная логика анализа
        // В рамках данного чата создаем заглушку с базовой структурой
        
        analysisResult.TotalRoutes = 0;
        analysisResult.AnalyzedRoutes = 0;
        analysisResult.AverageDeviation = 0;
        analysisResult.CompletedAt = DateTime.UtcNow;
        
        _logger.LogDebug("Анализ {SectionName} выполнен (заглушка)", analysisResult.SectionName);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Получает список участков
    /// </summary>
    public async Task<IEnumerable<string>> GetSectionsListAsync()
    {
        // Заглушка - в следующих чатах будет реализована работа с базой данных
        await Task.CompletedTask;
        return new[] { "Участок 1", "Участок 2", "Участок 3" };
    }

    /// <summary>
    /// Получает нормы для участка с количествами маршрутов
    /// </summary>
    public async Task<IEnumerable<NormWithCount>> GetNormsWithCountsForSectionAsync(
        string sectionName, 
        bool singleSectionOnly = false)
    {
        // Заглушка
        await Task.CompletedTask;
        return new[]
        {
            new NormWithCount("1.1", 15),
            new NormWithCount("1.2", 8),
            new NormWithCount("2.1", 22)
        };
    }
}

/// <summary>
/// Сервис интерполяции норм
/// Соответствует функциональности norm_storage.py
/// </summary>
public class NormInterpolationService : INormInterpolationService
{
    private readonly ILogger<NormInterpolationService> _logger;

    public NormInterpolationService(ILogger<NormInterpolationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Интерполирует значение нормы для заданной нагрузки
    /// </summary>
    public async Task<decimal?> InterpolateNormValueAsync(string normId, decimal loadValue)
    {
        _logger.LogTrace("Интерполируем норму {NormId} для нагрузки {Load}", normId, loadValue);
        
        // Заглушка - будет реализовано в следующих чатах
        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Создает функцию интерполяции для нормы
    /// </summary>
    public async Task<InterpolationFunction?> CreateInterpolationFunctionAsync(string normId)
    {
        _logger.LogTrace("Создаем функцию интерполяции для нормы {NormId}", normId);
        
        // Заглушка
        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Валидирует нормы в хранилище
    /// </summary>
    public async Task<ValidationResults> ValidateNormsAsync()
    {
        _logger.LogDebug("Выполняем валидацию норм");
        
        // Заглушка
        await Task.CompletedTask;
        return new ValidationResults
        {
            ValidNorms = new[] { "1.1", "1.2", "2.1" },
            InvalidNorms = Array.Empty<string>(),
            Warnings = Array.Empty<string>()
        };
    }
}

/// <summary>
/// Сервис кэширования результатов анализа
/// </summary>
public class AnalysisCacheService : IAnalysisCacheService
{
    private readonly ILogger<AnalysisCacheService> _logger;

    public AnalysisCacheService(ILogger<AnalysisCacheService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Получает результат анализа из кэша
    /// </summary>
    public async Task<AnalysisResult?> GetCachedAnalysisAsync(string analysisHash)
    {
        _logger.LogTrace("Ищем кэшированный анализ {AnalysisHash}", analysisHash);
        
        // Заглушка - будет реализовано с базой данных в следующих чатах
        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Сохраняет результат анализа в кэш
    /// </summary>
    public async Task SaveAnalysisToCacheAsync(AnalysisResult analysisResult)
    {
        _logger.LogTrace("Сохраняем анализ в кэш {AnalysisHash}", analysisResult.AnalysisHash);
        
        // Заглушка
        await Task.CompletedTask;
    }

    /// <summary>
    /// Очищает устаревший кэш
    /// </summary>
    public async Task CleanupOldCacheAsync(TimeSpan maxAge)
    {
        _logger.LogDebug("Очищаем кэш старше {MaxAge}", maxAge);
        
        // Заглушка
        await Task.CompletedTask;
    }
}

/// <summary>
/// Сервис хранения норм
/// Соответствует NormStorage из Python с улучшениями
/// </summary>
public class NormStorageService : INormStorageService
{
    private readonly ILogger<NormStorageService> _logger;

    public NormStorageService(ILogger<NormStorageService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Добавляет или обновляет нормы
    /// </summary>
    public async Task<Dictionary<string, string>> AddOrUpdateNormsAsync(IEnumerable<Norm> norms)
    {
        var results = new Dictionary<string, string>();
        var normsList = norms.ToList();
        
        _logger.LogInformation("Добавляем/обновляем {Count} норм", normsList.Count);
        
        foreach (var norm in normsList)
        {
            try
            {
                // Заглушка - будет реализовано с базой данных
                results[norm.NormId!] = "added";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка добавления нормы {NormId}", norm.NormId);
                results[norm.NormId!] = $"error: {ex.Message}";
            }
        }
        
        await Task.CompletedTask;
        return results;
    }

    /// <summary>
    /// Получает норму по ID
    /// </summary>
    public async Task<Norm?> GetNormAsync(string normId)
    {
        _logger.LogTrace("Получаем норму {NormId}", normId);
        
        // Заглушка
        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Получает все нормы
    /// </summary>
    public async Task<IEnumerable<Norm>> GetAllNormsAsync()
    {
        _logger.LogDebug("Получаем все нормы");
        
        // Заглушка
        await Task.CompletedTask;
        return new List<Norm>();
    }

    /// <summary>
    /// Получает информацию о хранилище
    /// </summary>
    public async Task<StorageInfo> GetStorageInfoAsync()
    {
        _logger.LogTrace("Получаем информацию о хранилище");
        
        await Task.CompletedTask;
        return new StorageInfo
        {
            TotalNorms = 0,
            TotalPoints = 0,
            NormsByType = new Dictionary<string, int>(),
            LastUpdated = DateTime.UtcNow
        };
    }
}