using MathNet.Numerics;
using MathNet.Numerics.Interpolation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Data;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Полная реализация сервиса интерполяции норм с Math.NET Numerics
/// Соответствует NormStorage из Python core/norm_storage.py + scipy.interpolate
/// </summary>
public class NormInterpolationService : INormInterpolationService
{
    private readonly ILogger<NormInterpolationService> _logger;
    private readonly AnalysisNormDbContext _context;
    private readonly ApplicationSettings _settings;
    
    // Кэш интерполяционных функций в памяти для быстрого доступа
    private readonly Dictionary<string, CachedInterpolation> _interpolationCache = new();
    private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);
    
    // Статистика использования для мониторинга
    private readonly Dictionary<string, InterpolationStatistics> _usageStats = new();

    public NormInterpolationService(
        ILogger<NormInterpolationService> logger,
        AnalysisNormDbContext context,
        IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _context = context;
        _settings = settings.Value;
    }

    /// <summary>
    /// Интерполирует значение нормы для заданной нагрузки
    /// Соответствует get_interpolated_value из Python NormStorage
    /// </summary>
    public async Task<decimal?> InterpolateNormValueAsync(string normId, decimal loadValue)
    {
        if (string.IsNullOrEmpty(normId) || loadValue <= 0)
        {
            _logger.LogWarning("Некорректные параметры интерполяции: NormId={NormId}, Load={Load}", 
                normId, loadValue);
            return null;
        }

        _logger.LogTrace("Интерполируем норму {NormId} для нагрузки {Load}", normId, loadValue);

        try
        {
            // Проверяем кэш базы данных сначала
            var cachedValue = await GetFromDatabaseCacheAsync(normId, loadValue);
            if (cachedValue.HasValue)
            {
                UpdateUsageStatistics(normId, isCacheHit: true);
                return cachedValue;
            }

            // Получаем или создаем интерполяционную функцию
            var interpolationFunc = await GetOrCreateInterpolationFunctionAsync(normId);
            if (interpolationFunc == null)
            {
                _logger.LogWarning("Не удалось создать функцию интерполяции для нормы {NormId}", normId);
                return null;
            }

            // Выполняем интерполяцию
            var interpolatedValue = PerformInterpolation(interpolationFunc, loadValue);
            if (interpolatedValue.HasValue)
            {
                // Сохраняем в кэш базы данных для будущих запросов
                await SaveToDatabaseCacheAsync(normId, loadValue, interpolatedValue.Value);
                UpdateUsageStatistics(normId, isCacheHit: false);
                
                _logger.LogTrace("Интерполяция завершена: {NormId}, Load={Load}, Result={Result}", 
                    normId, loadValue, interpolatedValue);
            }

            return interpolatedValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка интерполяции нормы {NormId} для нагрузки {Load}", normId, loadValue);
            return null;
        }
    }

    /// <summary>
    /// Создает функцию интерполяции для нормы
    /// Соответствует create_interpolation_function из Python
    /// </summary>
    public async Task<InterpolationFunction?> CreateInterpolationFunctionAsync(string normId)
    {
        if (string.IsNullOrEmpty(normId))
            return null;

        _logger.LogDebug("Создаем функцию интерполяции для нормы {NormId}", normId);

        try
        {
            // Получаем норму с точками из базы данных
            var norm = await _context.Norms
                .Include(n => n.Points)
                .FirstOrDefaultAsync(n => n.NormId == normId);

            if (norm == null)
            {
                _logger.LogWarning("Норма {NormId} не найдена в базе данных", normId);
                return null;
            }

            if (!norm.CanInterpolate())
            {
                _logger.LogWarning("Норма {NormId} не может использоваться для интерполяции: недостаточно точек", normId);
                return null;
            }

            // Подготавливаем данные для интерполяции
            var points = norm.Points
                .OrderBy(p => p.Load)
                .Where(p => IsValidPoint(p))
                .ToList();

            if (points.Count < 2)
            {
                _logger.LogWarning("Недостаточно валидных точек для интерполяции нормы {NormId}: {Count}", 
                    normId, points.Count);
                return null;
            }

            var xValues = points.Select(p => (double)p.Load).ToArray();
            var yValues = points.Select(p => (double)p.Consumption).ToArray();

            // Проверяем монотонность и уникальность X значений
            if (!AreXValuesValid(xValues))
            {
                _logger.LogWarning("Некорректные X значения для интерполяции нормы {NormId}", normId);
                return null;
            }

            var interpolationFunction = new InterpolationFunction(
                NormId: normId,
                NormType: norm.NormType ?? "Нажатие",
                XValues: xValues.Select(x => (decimal)x).ToArray(),
                YValues: yValues.Select(y => (decimal)y).ToArray()
            );

            _logger.LogDebug("Создана функция интерполяции для {NormId}: {PointCount} точек, диапазон [{Min:F1}-{Max:F1}] т/ось", 
                normId, points.Count, xValues.Min(), xValues.Max());

            return interpolationFunction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания функции интерполяции для нормы {NormId}", normId);
            return null;
        }
    }

    /// <summary>
    /// Валидирует нормы в хранилище
    /// Соответствует validate_norms из Python NormStorage
    /// </summary>
    public async Task<ValidationResults> ValidateNormsAsync()
    {
        _logger.LogInformation("Начинаем валидацию всех норм в хранилище");

        var validNorms = new List<string>();
        var invalidNorms = new List<string>();
        var warnings = new List<string>();

        try
        {
            var allNorms = await _context.Norms
                .Include(n => n.Points)
                .ToListAsync();

            _logger.LogDebug("Валидируем {Count} норм", allNorms.Count);

            foreach (var norm in allNorms)
            {
                try
                {
                    var validationResult = ValidateNorm(norm);
                    
                    if (validationResult.IsValid)
                    {
                        validNorms.Add(norm.NormId!);
                        if (validationResult.Warnings.Any())
                        {
                            warnings.AddRange(validationResult.Warnings.Select(w => $"{norm.NormId}: {w}"));
                        }
                    }
                    else
                    {
                        invalidNorms.Add(norm.NormId!);
                        warnings.AddRange(validationResult.Errors.Select(e => $"{norm.NormId}: {e}"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка валидации нормы {NormId}", norm.NormId);
                    invalidNorms.Add(norm.NormId!);
                    warnings.Add($"{norm.NormId}: Критическая ошибка валидации");
                }
            }

            _logger.LogInformation("Валидация завершена: {ValidCount} валидных, {InvalidCount} невалидных, {WarningCount} предупреждений", 
                validNorms.Count, invalidNorms.Count, warnings.Count);

            return new ValidationResults
            {
                ValidNorms = validNorms,
                InvalidNorms = invalidNorms,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка валидации норм");
            warnings.Add($"Критическая ошибка: {ex.Message}");
            
            return new ValidationResults
            {
                ValidNorms = validNorms,
                InvalidNorms = invalidNorms,
                Warnings = warnings
            };
        }
    }

    /// <summary>
    /// Получает или создает функцию интерполяции с кэшированием
    /// </summary>
    private async Task<CachedInterpolation?> GetOrCreateInterpolationFunctionAsync(string normId)
    {
        await _cacheSemaphore.WaitAsync();
        try
        {
            // Проверяем кэш в памяти
            if (_interpolationCache.TryGetValue(normId, out var cached))
            {
                if (DateTime.UtcNow - cached.CreatedAt < TimeSpan.FromHours(1)) // Кэш действует 1 час
                {
                    cached.LastUsed = DateTime.UtcNow;
                    return cached;
                }
                else
                {
                    _interpolationCache.Remove(normId);
                }
            }

            // Создаем новую функцию интерполяции
            var functionData = await CreateInterpolationFunctionAsync(normId);
            if (functionData == null)
                return null;

            // Создаем Math.NET интерполятор
            IInterpolation interpolator;
            try
            {
                var xValues = functionData.XValues.Select(x => (double)x).ToArray();
                var yValues = functionData.YValues.Select(y => (double)y).ToArray();

                // Выбираем тип интерполяции в зависимости от количества точек
                if (xValues.Length >= 4)
                {
                    // Cubic spline для 4+ точек (как scipy.interpolate.CubicSpline)
                    interpolator = CubicSpline.InterpolateAkimaSorted(xValues, yValues);
                    _logger.LogTrace("Создан Cubic Spline интерполятор для {NormId}", normId);
                }
                else
                {
                    // Linear interpolation для меньшего количества точек
                    interpolator = LinearSpline.InterpolateSorted(xValues, yValues);
                    _logger.LogTrace("Создан Linear интерполятор для {NormId}", normId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка создания Math.NET интерполятора для {NormId}", normId);
                return null;
            }

            var cachedInterpolation = new CachedInterpolation
            {
                NormId = normId,
                Function = functionData,
                Interpolator = interpolator,
                CreatedAt = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };

            _interpolationCache[normId] = cachedInterpolation;
            
            // Очищаем старые записи из кэша
            CleanupMemoryCache();
            
            return cachedInterpolation;
        }
        finally
        {
            _cacheSemaphore.Release();
        }
    }

    /// <summary>
    /// Выполняет интерполяцию значения
    /// </summary>
    private decimal? PerformInterpolation(CachedInterpolation cachedInterpolation, decimal loadValue)
    {
        try
        {
            var function = cachedInterpolation.Function;
            var interpolator = cachedInterpolation.Interpolator;
            
            var loadDouble = (double)loadValue;
            var minLoad = (double)function.XValues.Min();
            var maxLoad = (double)function.XValues.Max();

            // Проверяем диапазон (с небольшим допуском)
            const double tolerance = 0.01;
            if (loadDouble < minLoad - tolerance || loadDouble > maxLoad + tolerance)
            {
                _logger.LogTrace("Значение нагрузки {Load} вне диапазона [{Min:F2}-{Max:F2}] для нормы {NormId}", 
                    loadValue, minLoad, maxLoad, function.NormId);
                    
                // Экстраполяция - используем ближайшее значение с предупреждением
                if (loadDouble < minLoad)
                {
                    var result = interpolator.Interpolate(minLoad);
                    _logger.LogDebug("Экстраполяция для {NormId}: используем минимальное значение {MinValue:F2} вместо {RequestedValue:F2}", 
                        function.NormId, minLoad, loadValue);
                    return (decimal)result;
                }
                else
                {
                    var result = interpolator.Interpolate(maxLoad);
                    _logger.LogDebug("Экстраполяция для {NormId}: используем максимальное значение {MaxValue:F2} вместо {RequestedValue:F2}", 
                        function.NormId, maxLoad, loadValue);
                    return (decimal)result;
                }
            }

            // Выполняем интерполяцию
            var interpolatedValue = interpolator.Interpolate(loadDouble);
            
            // Проверяем результат на разумность
            if (double.IsNaN(interpolatedValue) || double.IsInfinity(interpolatedValue) || interpolatedValue < 0)
            {
                _logger.LogWarning("Некорректный результат интерполяции для {NormId}, Load={Load}: {Result}", 
                    function.NormId, loadValue, interpolatedValue);
                return null;
            }

            return (decimal)interpolatedValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка выполнения интерполяции для {NormId}, Load={Load}", 
                cachedInterpolation.NormId, loadValue);
            return null;
        }
    }

    /// <summary>
    /// Получает значение из кэша базы данных
    /// </summary>
    private async Task<decimal?> GetFromDatabaseCacheAsync(string normId, decimal loadValue)
    {
        try
        {
            // Ищем точное совпадение или близкое значение (в пределах tolerance)
            const decimal tolerance = 0.01m;
            
            var cachedEntry = await _context.NormInterpolationCache
                .Where(c => c.NormId == normId && 
                           Math.Abs(c.ParameterValue - loadValue) <= tolerance)
                .OrderBy(c => Math.Abs(c.ParameterValue - loadValue))
                .FirstOrDefaultAsync();

            if (cachedEntry != null)
            {
                // Обновляем время последнего использования
                cachedEntry.LastUsed = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                _logger.LogTrace("Найдено кэшированное значение для {NormId}, Load={Load}: {Value}", 
                    normId, loadValue, cachedEntry.InterpolatedValue);
                
                return cachedEntry.InterpolatedValue;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения из кэша базы данных: {NormId}, Load={Load}", normId, loadValue);
            return null;
        }
    }

    /// <summary>
    /// Сохраняет значение в кэш базы данных
    /// </summary>
    private async Task SaveToDatabaseCacheAsync(string normId, decimal loadValue, decimal interpolatedValue)
    {
        try
        {
            var cacheEntry = new NormInterpolationCache
            {
                NormId = normId,
                ParameterValue = loadValue,
                InterpolatedValue = interpolatedValue,
                LastUsed = DateTime.UtcNow
            };

            await _context.NormInterpolationCache.AddAsync(cacheEntry);
            await _context.SaveChangesAsync();
            
            _logger.LogTrace("Сохранено в кэш: {NormId}, Load={Load}, Value={Value}", 
                normId, loadValue, interpolatedValue);
        }
        catch (Exception ex)
        {
            // Не критично - просто логируем ошибку
            _logger.LogWarning(ex, "Не удалось сохранить в кэш: {NormId}, Load={Load}", normId, loadValue);
        }
    }

    /// <summary>
    /// Валидирует отдельную норму
    /// </summary>
    private NormValidationResult ValidateNorm(Norm norm)
    {
        var result = new NormValidationResult { IsValid = true };

        if (string.IsNullOrEmpty(norm.NormId))
        {
            result.IsValid = false;
            result.Errors.Add("Отсутствует идентификатор нормы");
        }

        if (!norm.Points.Any())
        {
            result.IsValid = false;
            result.Errors.Add("Отсутствуют точки нормы");
            return result;
        }

        var validPoints = norm.Points.Where(IsValidPoint).ToList();
        
        if (validPoints.Count < 2)
        {
            result.IsValid = false;
            result.Errors.Add($"Недостаточно валидных точек: {validPoints.Count} (требуется минимум 2)");
        }

        // Проверяем монотонность нагрузок
        var loads = validPoints.Select(p => p.Load).OrderBy(x => x).ToList();
        var hasDuplicates = loads.GroupBy(x => x).Any(g => g.Count() > 1);
        if (hasDuplicates)
        {
            result.Warnings.Add("Найдены дублирующиеся значения нагрузки");
        }

        // Проверяем разумность диапазона
        if (loads.Any())
        {
            var minLoad = loads.Min();
            var maxLoad = loads.Max();
            
            if (maxLoad - minLoad < 5m) // Диапазон менее 5 т/ось
            {
                result.Warnings.Add($"Узкий диапазон нагрузок: {minLoad:F1}-{maxLoad:F1} т/ось");
            }
        }

        return result;
    }

    /// <summary>
    /// Проверяет валидность точки нормы
    /// </summary>
    private static bool IsValidPoint(NormPoint point)
    {
        return point.Load >= AnalysisConstants.MinValidLoad && 
               point.Load <= AnalysisConstants.MaxValidLoad &&
               point.Consumption >= AnalysisConstants.MinValidConsumption && 
               point.Consumption <= AnalysisConstants.MaxValidConsumption;
    }

    /// <summary>
    /// Проверяет валидность массива X значений для интерполяции
    /// </summary>
    private bool AreXValuesValid(double[] xValues)
    {
        if (xValues.Length < 2)
            return false;

        // Проверяем уникальность
        var uniqueCount = xValues.Distinct().Count();
        if (uniqueCount != xValues.Length)
        {
            _logger.LogTrace("Найдены дублирующиеся X значения: {UniqueCount}/{TotalCount}", uniqueCount, xValues.Length);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Очищает устаревшие записи из кэша в памяти
    /// </summary>
    private void CleanupMemoryCache()
    {
        const int maxCacheSize = 100; // Максимум 100 функций в кэше
        const int hoursToKeep = 2; // Держим функции 2 часа
        
        if (_interpolationCache.Count <= maxCacheSize)
            return;

        var cutoffTime = DateTime.UtcNow.AddHours(-hoursToKeep);
        var toRemove = _interpolationCache
            .Where(kvp => kvp.Value.LastUsed < cutoffTime)
            .OrderBy(kvp => kvp.Value.LastUsed)
            .Take(_interpolationCache.Count - maxCacheSize + 10) // Удаляем с запасом
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _interpolationCache.Remove(key);
        }

        if (toRemove.Any())
        {
            _logger.LogDebug("Очищено {Count} функций из кэша интерполяции", toRemove.Count);
        }
    }

    /// <summary>
    /// Обновляет статистику использования
    /// </summary>
    private void UpdateUsageStatistics(string normId, bool isCacheHit)
    {
        if (!_usageStats.TryGetValue(normId, out var stats))
        {
            stats = new InterpolationStatistics { NormId = normId };
            _usageStats[normId] = stats;
        }

        stats.TotalRequests++;
        if (isCacheHit)
            stats.CacheHits++;
        stats.LastUsed = DateTime.UtcNow;
    }

    /// <summary>
    /// Получает статистику использования интерполяции
    /// </summary>
    public async Task<Dictionary<string, InterpolationStatistics>> GetUsageStatisticsAsync()
    {
        await Task.CompletedTask;
        return _usageStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public void Dispose()
    {
        _cacheSemaphore?.Dispose();
    }
}

/// <summary>
/// Кэшированная интерполяционная функция
/// </summary>
public class CachedInterpolation
{
    public string NormId { get; set; } = string.Empty;
    public InterpolationFunction Function { get; set; } = null!;
    public IInterpolation Interpolator { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsed { get; set; }
}

/// <summary>
/// Результат валидации нормы
/// </summary>
public class NormValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Статистика использования интерполяции
/// </summary>
public class InterpolationStatistics
{
    public string NormId { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int CacheHits { get; set; }
    public decimal CacheHitRatio => TotalRequests > 0 ? (decimal)CacheHits / TotalRequests : 0m;
    public DateTime LastUsed { get; set; }
}