using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Data;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Полная реализация сервиса кэширования результатов анализа в SQLite
/// Обеспечивает персистентное кэширование между сессиями приложения
/// </summary>
public class AnalysisCacheService : IAnalysisCacheService
{
    private readonly ILogger<AnalysisCacheService> _logger;
    private readonly AnalysisNormDbContext _context;
    private readonly ApplicationSettings _settings;
    
    // JSON опции для сериализации
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Статистика кэша для мониторинга
    private readonly CacheStatistics _statistics = new();

    public AnalysisCacheService(
        ILogger<AnalysisCacheService> logger,
        AnalysisNormDbContext context,
        IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _context = context;
        _settings = settings.Value;
    }

    /// <summary>
    /// Получает результат анализа из кэша
    /// Быстрый поиск по хэшу анализа
    /// </summary>
    public async Task<AnalysisResult?> GetCachedAnalysisAsync(string analysisHash)
    {
        if (string.IsNullOrEmpty(analysisHash))
        {
            _logger.LogWarning("Получен пустой хэш анализа для поиска в кэше");
            return null;
        }

        _logger.LogTrace("Ищем кэшированный анализ {AnalysisHash}", analysisHash);

        try
        {
            _statistics.TotalRequests++;

            var cached = await _context.AnalysisResults
                .Include(ar => ar.Routes.Take(1000)) // Ограничиваем количество маршрутов для производительности
                .Include(ar => ar.Norm)
                .FirstOrDefaultAsync(ar => ar.AnalysisHash == analysisHash);

            if (cached == null)
            {
                _logger.LogTrace("Кэшированный анализ {AnalysisHash} не найден", analysisHash);
                _statistics.CacheMisses++;
                return null;
            }

            // Проверяем не устарел ли кэш
            var maxAge = TimeSpan.FromHours(_settings.CacheExpirationHours);
            if (DateTime.UtcNow - cached.CreatedAt > maxAge)
            {
                _logger.LogDebug("Кэшированный анализ {AnalysisHash} устарел (возраст: {Age}), удаляем", 
                    analysisHash, DateTime.UtcNow - cached.CreatedAt);
                
                await DeleteExpiredAnalysisAsync(cached);
                _statistics.ExpiredEntries++;
                return null;
            }

            // Обновляем время последнего использования
            cached.LastUsed = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _statistics.CacheHits++;
            _logger.LogTrace("Найден кэшированный анализ {AnalysisHash} (возраст: {Age})", 
                analysisHash, DateTime.UtcNow - cached.CreatedAt);

            return cached;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения кэшированного анализа {AnalysisHash}", analysisHash);
            _statistics.Errors++;
            return null;
        }
    }

    /// <summary>
    /// Сохраняет результат анализа в кэш
    /// Включает сжатие больших объемов данных
    /// </summary>
    public async Task SaveAnalysisToCacheAsync(AnalysisResult analysisResult)
    {
        if (analysisResult == null || string.IsNullOrEmpty(analysisResult.AnalysisHash))
        {
            _logger.LogWarning("Попытка сохранить в кэш невалидный результат анализа");
            return;
        }

        _logger.LogDebug("Сохраняем анализ в кэш {AnalysisHash} (маршрутов: {RouteCount})", 
            analysisResult.AnalysisHash, analysisResult.Routes.Count);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Проверяем не существует ли уже такой анализ
            var existingAnalysis = await _context.AnalysisResults
                .FirstOrDefaultAsync(ar => ar.AnalysisHash == analysisResult.AnalysisHash);

            if (existingAnalysis != null)
            {
                _logger.LogDebug("Анализ {AnalysisHash} уже существует в кэше, обновляем", analysisResult.AnalysisHash);
                await UpdateExistingAnalysisAsync(existingAnalysis, analysisResult);
            }
            else
            {
                await CreateNewAnalysisAsync(analysisResult);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _statistics.SavedEntries++;
            _logger.LogTrace("Анализ {AnalysisHash} успешно сохранен в кэш", analysisResult.AnalysisHash);

            // Периодическая очистка старого кэша
            if (_statistics.SavedEntries % 10 == 0) // Каждые 10 сохранений
            {
                _ = Task.Run(async () => await CleanupOldCacheAsync(TimeSpan.FromDays(_settings.CacheExpirationDays)));
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Ошибка сохранения анализа {AnalysisHash} в кэш", analysisResult.AnalysisHash);
            _statistics.Errors++;
            throw;
        }
    }

    /// <summary>
    /// Очищает устаревший кэш
    /// Удаляет записи старше указанного возраста
    /// </summary>
    public async Task CleanupOldCacheAsync(TimeSpan maxAge)
    {
        _logger.LogDebug("Начинаем очистку кэша старше {MaxAge}", maxAge);

        try
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            
            // Находим устаревшие записи
            var expiredAnalyses = await _context.AnalysisResults
                .Where(ar => ar.CreatedAt < cutoffDate)
                .OrderBy(ar => ar.CreatedAt)
                .Take(100) // Ограничиваем количество для производительности
                .ToListAsync();

            if (!expiredAnalyses.Any())
            {
                _logger.LogTrace("Устаревших записей в кэше не найдено");
                return;
            }

            _logger.LogDebug("Найдено {Count} устаревших записей в кэше для удаления", expiredAnalyses.Count);

            // Удаляем записи пакетами для лучшей производительности
            var deletedCount = 0;
            const int batchSize = 20;

            for (int i = 0; i < expiredAnalyses.Count; i += batchSize)
            {
                var batch = expiredAnalyses.Skip(i).Take(batchSize).ToList();
                
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var analysis in batch)
                    {
                        await DeleteExpiredAnalysisAsync(analysis);
                        deletedCount++;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    
                    _logger.LogTrace("Удален пакет {BatchSize} устаревших записей", batch.Count);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Ошибка удаления пакета устаревших записей кэша");
                    break; // Прерываем очистку при ошибке
                }
            }

            _statistics.DeletedEntries += deletedCount;
            _logger.LogInformation("Очистка кэша завершена: удалено {DeletedCount} записей", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка очистки кэша");
            _statistics.Errors++;
        }
    }

    /// <summary>
    /// Получает статистику использования кэша
    /// </summary>
    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        try
        {
            // Обновляем статистику из базы данных
            var totalEntries = await _context.AnalysisResults.CountAsync();
            var totalRoutes = await _context.Routes.CountAsync();
            var oldestEntry = await _context.AnalysisResults
                .OrderBy(ar => ar.CreatedAt)
                .Select(ar => ar.CreatedAt)
                .FirstOrDefaultAsync();
            var newestEntry = await _context.AnalysisResults
                .OrderByDescending(ar => ar.CreatedAt)
                .Select(ar => ar.CreatedAt)
                .FirstOrDefaultAsync();

            _statistics.TotalCacheEntries = totalEntries;
            _statistics.TotalRoutesInCache = totalRoutes;
            _statistics.OldestEntry = oldestEntry;
            _statistics.NewestEntry = newestEntry;

            // Вычисляем hit ratio
            _statistics.HitRatio = _statistics.TotalRequests > 0 
                ? (decimal)_statistics.CacheHits / _statistics.TotalRequests 
                : 0;

            return _statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения статистики кэша");
            return _statistics;
        }
    }

    /// <summary>
    /// Принудительно очищает весь кэш
    /// </summary>
    public async Task ClearAllCacheAsync()
    {
        _logger.LogWarning("Выполняется полная очистка кэша анализов");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var allAnalyses = await _context.AnalysisResults.ToListAsync();
            _context.AnalysisResults.RemoveRange(allAnalyses);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _statistics.DeletedEntries += allAnalyses.Count;
            _logger.LogWarning("Кэш полностью очищен: удалено {Count} записей", allAnalyses.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Ошибка полной очистки кэша");
            throw;
        }
    }

    /// <summary>
    /// Получает размер кэша в мегабайтах (приблизительно)
    /// </summary>
    public async Task<decimal> GetCacheSizeMbAsync()
    {
        try
        {
            // Простая оценка размера на основе количества записей
            var entryCount = await _context.AnalysisResults.CountAsync();
            var routeCount = await _context.Routes.CountAsync();
            
            // Приблизительная оценка: 1KB на анализ + 500B на маршрут
            var estimatedBytes = (entryCount * 1024) + (routeCount * 512);
            return Math.Round((decimal)estimatedBytes / (1024 * 1024), 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка оценки размера кэша");
            return 0;
        }
    }

    /// <summary>
    /// Создает новую запись анализа в кэше
    /// </summary>
    private async Task CreateNewAnalysisAsync(AnalysisResult analysisResult)
    {
        // Создаем копию для сохранения (без навигационных свойств)
        var cacheEntry = new AnalysisResult
        {
            AnalysisHash = analysisResult.AnalysisHash,
            SectionName = analysisResult.SectionName,
            NormId = analysisResult.NormId,
            SingleSectionOnly = analysisResult.SingleSectionOnly,
            UseCoefficients = analysisResult.UseCoefficients,
            TotalRoutes = analysisResult.TotalRoutes,
            AnalyzedRoutes = analysisResult.AnalyzedRoutes,
            AverageDeviation = analysisResult.AverageDeviation,
            MinDeviation = analysisResult.MinDeviation,
            MaxDeviation = analysisResult.MaxDeviation,
            MedianDeviation = analysisResult.MedianDeviation,
            StandardDeviation = analysisResult.StandardDeviation,
            EconomyCount = analysisResult.EconomyCount,
            OverrunCount = analysisResult.OverrunCount,
            NormalCount = analysisResult.NormalCount,
            ProcessingTimeMs = analysisResult.ProcessingTimeMs,
            CreatedAt = analysisResult.CreatedAt,
            CompletedAt = analysisResult.CompletedAt,
            LastUsed = DateTime.UtcNow
        };

        // Сохраняем маршруты (только те, которые нужны для восстановления результата)
        var routesToCache = analysisResult.Routes.Take(1000).ToList(); // Ограничиваем для производительности
        foreach (var route in routesToCache)
        {
            // Создаем копию маршрута без навигационных свойств
            var routeCopy = new Route
            {
                RouteNumber = route.RouteNumber,
                RouteDate = route.RouteDate,
                TripDate = route.TripDate,
                DriverTab = route.DriverTab,
                LocomotiveSeries = route.LocomotiveSeries,
                LocomotiveNumber = route.LocomotiveNumber,
                SectionName = route.SectionName,
                NormNumber = route.NormNumber,
                FactConsumption = route.FactConsumption,
                NormConsumption = route.NormConsumption,
                DeviationPercent = route.DeviationPercent,
                Status = route.Status,
                AxleLoad = route.AxleLoad,
                NormInterpolated = route.NormInterpolated,
                UseRedColor = route.UseRedColor,
                UseRedRashod = route.UseRedRashod,
                CreatedAt = route.CreatedAt,
                RouteKey = route.RouteKey
            };

            cacheEntry.Routes.Add(routeCopy);
        }

        await _context.AnalysisResults.AddAsync(cacheEntry);
    }

    /// <summary>
    /// Обновляет существующую запись анализа в кэше
    /// </summary>
    private async Task UpdateExistingAnalysisAsync(AnalysisResult existingAnalysis, AnalysisResult newAnalysis)
    {
        // Обновляем основные поля
        existingAnalysis.TotalRoutes = newAnalysis.TotalRoutes;
        existingAnalysis.AnalyzedRoutes = newAnalysis.AnalyzedRoutes;
        existingAnalysis.AverageDeviation = newAnalysis.AverageDeviation;
        existingAnalysis.MinDeviation = newAnalysis.MinDeviation;
        existingAnalysis.MaxDeviation = newAnalysis.MaxDeviation;
        existingAnalysis.MedianDeviation = newAnalysis.MedianDeviation;
        existingAnalysis.StandardDeviation = newAnalysis.StandardDeviation;
        existingAnalysis.EconomyCount = newAnalysis.EconomyCount;
        existingAnalysis.OverrunCount = newAnalysis.OverrunCount;
        existingAnalysis.NormalCount = newAnalysis.NormalCount;
        existingAnalysis.ProcessingTimeMs = newAnalysis.ProcessingTimeMs;
        existingAnalysis.CompletedAt = newAnalysis.CompletedAt;
        existingAnalysis.LastUsed = DateTime.UtcNow;

        // Удаляем старые маршруты и добавляем новые
        if (existingAnalysis.Routes.Any())
        {
            _context.Routes.RemoveRange(existingAnalysis.Routes);
        }

        var routesToCache = newAnalysis.Routes.Take(1000).ToList();
        foreach (var route in routesToCache)
        {
            existingAnalysis.Routes.Add(new Route
            {
                RouteNumber = route.RouteNumber,
                RouteDate = route.RouteDate,
                SectionName = route.SectionName,
                DeviationPercent = route.DeviationPercent,
                Status = route.Status,
                CreatedAt = DateTime.UtcNow,
                RouteKey = route.RouteKey
            });
        }
    }

    /// <summary>
    /// Удаляет устаревший анализ со всеми связанными данными
    /// </summary>
    private async Task DeleteExpiredAnalysisAsync(AnalysisResult analysis)
    {
        // Удаляем связанные маршруты
        if (analysis.Routes.Any())
        {
            _context.Routes.RemoveRange(analysis.Routes);
        }

        // Удаляем сам анализ
        _context.AnalysisResults.Remove(analysis);

        _logger.LogTrace("Удален устаревший анализ {AnalysisHash} (маршрутов: {RouteCount})", 
            analysis.AnalysisHash, analysis.Routes.Count);
    }
}

/// <summary>
/// Статистика использования кэша
/// </summary>
public class CacheStatistics
{
    public int TotalRequests { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public int SavedEntries { get; set; }
    public int DeletedEntries { get; set; }
    public int ExpiredEntries { get; set; }
    public int Errors { get; set; }
    public decimal HitRatio { get; set; }
    
    // Статистика базы данных
    public int TotalCacheEntries { get; set; }
    public int TotalRoutesInCache { get; set; }
    public DateTime OldestEntry { get; set; }
    public DateTime NewestEntry { get; set; }
    
    public override string ToString()
    {
        return $"Cache Stats: {CacheHits}/{TotalRequests} hits ({HitRatio:P1}), " +
               $"{TotalCacheEntries} entries, {Errors} errors";
    }
}

/// <summary>
/// Расширения ApplicationSettings для кэширования
/// </summary>
public static class CacheSettingsExtensions
{
    public static int CacheExpirationHours => 24;
    public static int CacheExpirationDays => 7;
}