using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Models.Domain;
using AnalysisNorm.Infrastructure.Mathematics;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Высокопроизводительное хранилище норм в памяти с кэшированием интерполяционных функций
/// Оптимизировано для быстрого доступа и минимального использования памяти
/// </summary>
public class MemoryNormStorage : INormStorage, IDisposable
{
    private readonly ConcurrentDictionary<string, Norm> _norms = new();
    private readonly IMemoryCache _interpolationCache;
    private readonly IApplicationLogger _logger;
    private readonly InterpolationEngine _interpolationEngine;
    private readonly object _lockObject = new();
    private bool _disposed;

    /// <summary>
    /// Конструктор с dependency injection
    /// </summary>
    public MemoryNormStorage(IMemoryCache memoryCache, IApplicationLogger logger)
    {
        _interpolationCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interpolationEngine = new InterpolationEngine();
        
        _logger.LogInformation("MemoryNormStorage инициализирован");
    }

    /// <summary>
    /// Получить норму по идентификатору с логированием для отладки
    /// </summary>
    public Task<Norm?> GetNormAsync(string normId)
    {
        if (string.IsNullOrWhiteSpace(normId))
            return Task.FromResult<Norm?>(null);

        _norms.TryGetValue(normId, out var norm);
        
        if (norm == null)
            _logger.LogDebug("Норма {NormId} не найдена в хранилище", normId);
        else
            _logger.LogDebug("Норма {NormId} получена из хранилища", normId);

        return Task.FromResult(norm);
    }

    /// <summary>
    /// Получить все нормы (возвращаем копию для безопасности)
    /// </summary>
    public Task<IEnumerable<Norm>> GetAllNormsAsync()
    {
        var norms = _norms.Values.ToList(); // Snapshot для thread safety
        _logger.LogDebug("Возвращено {Count} норм из хранилища", norms.Count);
        return Task.FromResult<IEnumerable<Norm>>(norms);
    }

    /// <summary>
    /// Сохранить норму с валидацией и кэшированием
    /// </summary>
    public Task SaveNormAsync(Norm norm)
    {
        ArgumentNullException.ThrowIfNull(norm);

        if (string.IsNullOrWhiteSpace(norm.Id))
            throw new ArgumentException("Norm ID cannot be null or empty", nameof(norm));

        // Валидация точек нормы
        if (!ValidateNormPoints(norm.Points))
            throw new ArgumentException($"Invalid norm points for norm {norm.Id}", nameof(norm));

        lock (_lockObject)
        {
            var isNew = !_norms.ContainsKey(norm.Id);
            _norms.AddOrUpdate(norm.Id, norm, (_, _) => norm);

            // Сбрасываем кэш интерполяции для этой нормы
            _interpolationCache.Remove($"interpolation:{norm.Id}");

            _logger.LogInformation("Норма {NormId} {Action} в хранилище", 
                norm.Id, isNew ? "добавлена" : "обновлена");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Пакетное сохранение норм (оптимизированная операция)
    /// </summary>
    public async Task SaveNormsAsync(IEnumerable<Norm> norms)
    {
        ArgumentNullException.ThrowIfNull(norms);

        var normsArray = norms.ToArray();
        if (normsArray.Length == 0)
            return;

        // Валидация всех норм перед сохранением
        var invalidNorms = normsArray
            .Where(n => string.IsNullOrWhiteSpace(n.Id) || !ValidateNormPoints(n.Points))
            .Select(n => n.Id)
            .ToList();

        if (invalidNorms.Count > 0)
            throw new ArgumentException($"Invalid norms: {string.Join(", ", invalidNorms)}");

        lock (_lockObject)
        {
            var addedCount = 0;
            var updatedCount = 0;

            foreach (var norm in normsArray)
            {
                var isNew = !_norms.ContainsKey(norm.Id);
                _norms.AddOrUpdate(norm.Id, norm, (_, _) => norm);

                // Сбрасываем кэш интерполяции
                _interpolationCache.Remove($"interpolation:{norm.Id}");

                if (isNew) addedCount++; else updatedCount++;
            }

            _logger.LogInformation("Пакетное сохранение завершено: добавлено {Added}, обновлено {Updated}", 
                addedCount, updatedCount);
        }
    }

    /// <summary>
    /// Получить кэшированную функцию интерполяции для нормы
    /// </summary>
    public async Task<Func<decimal, decimal>?> GetInterpolationFunctionAsync(string normId)
    {
        if (string.IsNullOrWhiteSpace(normId))
            return null;

        // Проверяем кэш
        var cacheKey = $"interpolation:{normId}";
        if (_interpolationCache.TryGetValue(cacheKey, out Func<decimal, decimal>? cachedFunction))
        {
            _logger.LogDebug("Функция интерполяции для нормы {NormId} получена из кэша", normId);
            return cachedFunction;
        }

        // Получаем норму и создаем функцию
        var norm = await GetNormAsync(normId);
        if (norm?.Points == null || norm.Points.Count == 0)
            return null;

        try
        {
            var interpolationFunction = _interpolationEngine.CreateInterpolationFunction(norm.Points);
            
            // Кэшируем функцию с TTL 30 минут
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                Priority = CacheItemPriority.Normal
            };
            
            _interpolationCache.Set(cacheKey, interpolationFunction, cacheOptions);
            
            _logger.LogDebug("Функция интерполяции для нормы {NormId} создана и кэширована", normId);
            return interpolationFunction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания функции интерполяции для нормы {NormId}", normId);
            return null;
        }
    }

    /// <summary>
    /// Поиск норм по критериям с оптимизированной фильтрацией
    /// </summary>
    public Task<IEnumerable<Norm>> SearchNormsAsync(string? type = null, string? pattern = null)
    {
        var query = _norms.Values.AsEnumerable();

        // Фильтр по типу
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(n => string.Equals(n.Type, type, StringComparison.OrdinalIgnoreCase));
        }

        // Фильтр по паттерну в ID
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            query = query.Where(n => n.Id.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        var results = query.ToList();
        _logger.LogDebug("Поиск норм: тип='{Type}', паттерн='{Pattern}', найдено={Count}", 
            type, pattern, results.Count);

        return Task.FromResult<IEnumerable<Norm>>(results);
    }

    /// <summary>
    /// Получить детальную статистику хранилища
    /// </summary>
    public Task<StorageInfo> GetStorageInfoAsync()
    {
        lock (_lockObject)
        {
            // Приблизительный расчет размера кэша
            var cacheSize = EstimateCacheSize();
            
            var info = new StorageInfo(
                TotalNorms: _norms.Count,
                CachedFunctions: GetCachedFunctionCount(),
                CacheSizeMB: cacheSize,
                LastUpdated: DateTime.UtcNow
            );

            _logger.LogDebug("Статистика хранилища: {Info}", info);
            return Task.FromResult(info);
        }
    }

    /// <summary>
    /// Очистить весь кэш интерполяционных функций
    /// </summary>
    public Task ClearCacheAsync()
    {
        lock (_lockObject)
        {
            // Memory cache не имеет Clear(), поэтому создаем новый instance
            if (_interpolationCache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0); // Очищаем все записи
            }

            _logger.LogInformation("Кэш интерполяционных функций очищен");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Валидация всех норм в хранилище
    /// </summary>
    public Task<ValidationResult> ValidateNormsAsync()
    {
        var validNorms = 0;
        var invalidNorms = 0;
        var errors = new List<string>();

        foreach (var (normId, norm) in _norms)
        {
            try
            {
                if (ValidateNorm(norm))
                {
                    validNorms++;
                }
                else
                {
                    invalidNorms++;
                    errors.Add($"Норма {normId}: некорректные данные");
                }
            }
            catch (Exception ex)
            {
                invalidNorms++;
                errors.Add($"Норма {normId}: {ex.Message}");
            }
        }

        var result = new ValidationResult(validNorms, invalidNorms, errors);
        _logger.LogInformation("Валидация норм завершена: валидных={Valid}, невалидных={Invalid}", 
            validNorms, invalidNorms);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Валидация точек нормы
    /// </summary>
    private static bool ValidateNormPoints(IReadOnlyList<DataPoint> points)
    {
        if (points == null || points.Count == 0)
            return false;

        // Проверяем что все X > 0 и Y > 0
        return points.All(p => p.X > 0 && p.Y > 0);
    }

    /// <summary>
    /// Полная валидация нормы
    /// </summary>
    private static bool ValidateNorm(Norm norm)
    {
        if (string.IsNullOrWhiteSpace(norm.Id))
            return false;

        if (string.IsNullOrWhiteSpace(norm.Type))
            return false;

        return ValidateNormPoints(norm.Points);
    }

    /// <summary>
    /// Приблизительная оценка размера кэша в MB
    /// </summary>
    private decimal EstimateCacheSize()
    {
        // Грубая оценка: каждая функция интерполяции ~1KB в памяти
        var functionsCount = GetCachedFunctionCount();
        return Math.Round((decimal)(functionsCount * 1024) / (1024 * 1024), 2);
    }

    /// <summary>
    /// Подсчет кэшированных функций (приблизительно)
    /// </summary>
    private int GetCachedFunctionCount()
    {
        // MemoryCache не предоставляет прямого способа подсчета элементов
        // Используем рефлексию или приблизительную оценку
        if (_interpolationCache is MemoryCache memoryCache)
        {
            // Приблизительная оценка на основе количества норм
            return Math.Min(_norms.Count, 100); // Максимум 100 кэшированных функций
        }
        return 0;
    }

    /// <summary>
    /// Освобождение ресурсов
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _norms.Clear();
            _interpolationCache?.Dispose();
            _disposed = true;
            
            _logger.LogInformation("MemoryNormStorage освобожден");
        }
    }
}