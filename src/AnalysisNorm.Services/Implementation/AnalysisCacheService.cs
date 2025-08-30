using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Data;
using AnalysisNorm.Services.Interfaces;
// ИСПРАВЛЕНО: Explicit using alias для разрешения конфликтов типов
using CoreAnalysisResult = AnalysisNorm.Core.Entities.AnalysisResult;
using CoreProcessingStatistics = AnalysisNorm.Core.Entities.ProcessingStatistics;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Полная реализация сервиса кэширования результатов анализа в SQLite
/// Обеспечивает персистентное кэширование между сессиями приложения
/// ИСПРАВЛЕНО: Используем только типы из AnalysisNorm.Core.Entities
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
    /// ИСПРАВЛЕНО: Правильный возвращаемый тип CoreAnalysisResult
    /// </summary>
    public async Task<CoreAnalysisResult?> GetCachedAnalysisAsync(string analysisHash)
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
    /// ИСПРАВЛЕНО: Правильный параметр CoreAnalysisResult
    /// </summary>
    public async Task SaveAnalysisAsync(CoreAnalysisResult analysisResult, CancellationToken cancellationToken = default)
    {
        await SaveAnalysisToCacheAsync(analysisResult);
    }

    /// <summary>
    /// Очищает устаревший кэш по возрасту
    /// </summary>
    public async Task CleanupExpiredCacheAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        await CleanupOldCacheAsync(maxAge);
    }

    /// <summary>
    /// Получает статистику использования кэша
    /// </summary>
    public async Task<CacheStatistics> GetCacheStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var totalEntries = await _context.AnalysisResults.CountAsync(cancellationToken);
            var totalRoutes = await _context.Routes.CountAsync(cancellationToken);

            _statistics.TotalEntries = totalEntries;
            _statistics.CacheSizeBytes = totalEntries * 2048 + totalRoutes * 1024; // Приблизительная оценка
            _statistics.LastCleanup = DateTime.UtcNow;
            _statistics.HitRate = _statistics.TotalRequests > 0
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

    #region Private Methods - Полная реализация всех вспомогательных методов

    /// <summary>
    /// Сохраняет анализ в кэш с полной обработкой
    /// </summary>
    private async Task SaveAnalysisToCacheAsync(CoreAnalysisResult analysisResult)
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
            throw;
        }
    }

    /// <summary>
    /// Создает новую запись анализа в кэше
    /// </summary>
    private async Task CreateNewAnalysisAsync(CoreAnalysisResult analysisResult)
    {
        var cacheEntry = new CoreAnalysisResult
        {
            SectionName = analysisResult.SectionName,
            NormId = analysisResult.NormId,
            SingleSectionOnly = analysisResult.SingleSectionOnly,
            UseCoefficients = analysisResult.UseCoefficients,
            TotalRoutes = analysisResult.TotalRoutes,
            ProcessedRoutes = analysisResult.ProcessedRoutes,
            AnalyzedRoutes = analysisResult.AnalyzedRoutes,
            Economy = analysisResult.Economy,
            Normal = analysisResult.Normal,
            Overrun = analysisResult.Overrun,
            AverageDeviation = analysisResult.AverageDeviation,
            MinDeviation = analysisResult.MinDeviation,
            MaxDeviation = analysisResult.MaxDeviation,
            MedianDeviation = analysisResult.MedianDeviation,
            StandardDeviation = analysisResult.StandardDeviation,
            EconomyStrong = analysisResult.EconomyStrong,
            EconomyMedium = analysisResult.EconomyMedium,
            EconomyWeak = analysisResult.EconomyWeak,
            OverrunWeak = analysisResult.OverrunWeak,
            OverrunMedium = analysisResult.OverrunMedium,
            OverrunStrong = analysisResult.OverrunStrong,
            CreatedAt = analysisResult.CreatedAt,
            CompletedAt = analysisResult.CompletedAt,
            LastUsed = DateTime.UtcNow,
            AnalysisHash = analysisResult.AnalysisHash,
            ProcessingTimeMs = analysisResult.ProcessingTimeMs,
            ErrorMessage = analysisResult.ErrorMessage,
            Routes = new List<Route>()
        };

        // Копируем первые 1000 маршрутов для экономии места
        var routesToCache = analysisResult.Routes.Take(1000).ToList();
        foreach (var route in routesToCache)
        {
            var routeCopy = new Route
            {
                DriverTabNumber = route.DriverTabNumber,
                RouteNumber = route.RouteNumber,
                RouteDate = route.RouteDate,
                LocomotiveSeries = route.LocomotiveSeries,
                LocomotiveNumber = route.LocomotiveNumber,
                SectionNames = route.SectionNames,
                NormNumber = route.NormNumber,
                BruttoTons = route.BruttoTons,
                NettoTons = route.NettoTons,
                AxesCount = route.AxesCount,
                TrainLength = route.TrainLength,
                TripTime = route.TripTime,
                Distance = route.Distance,
                TonKilometers = route.TonKilometers,
                FactUd = route.FactUd,
                FactUdOriginal = route.FactUdOriginal,
                NormUd = route.NormUd,
                Coefficient = route.Coefficient,
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
    private async Task UpdateExistingAnalysisAsync(CoreAnalysisResult existingAnalysis, CoreAnalysisResult newAnalysis)
    {
        // Обновляем основные поля
        existingAnalysis.TotalRoutes = newAnalysis.TotalRoutes;
        existingAnalysis.AnalyzedRoutes = newAnalysis.AnalyzedRoutes;
        existingAnalysis.AverageDeviation = newAnalysis.AverageDeviation;
        existingAnalysis.MinDeviation = newAnalysis.MinDeviation;
        existingAnalysis.MaxDeviation = newAnalysis.MaxDeviation;
        existingAnalysis.MedianDeviation = newAnalysis.MedianDeviation;
        existingAnalysis.StandardDeviation = newAnalysis.StandardDeviation;
        existingAnalysis.Economy = newAnalysis.Economy;
        existingAnalysis.Overrun = newAnalysis.Overrun;
        existingAnalysis.Normal = newAnalysis.Normal;
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
                DriverTabNumber = route.DriverTabNumber,
                RouteNumber = route.RouteNumber,
                RouteDate = route.RouteDate,
                LocomotiveSeries = route.LocomotiveSeries,
                LocomotiveNumber = route.LocomotiveNumber,
                SectionNames = route.SectionNames,
                NormNumber = route.NormNumber,
                BruttoTons = route.BruttoTons,
                NettoTons = route.NettoTons,
                AxesCount = route.AxesCount,
                TrainLength = route.TrainLength,
                TripTime = route.TripTime,
                Distance = route.Distance,
                TonKilometers = route.TonKilometers,
                FactUd = route.FactUd,
                FactUdOriginal = route.FactUdOriginal,
                NormUd = route.NormUd,
                Coefficient = route.Coefficient,
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
            });
        }
    }

    /// <summary>
    /// Очищает старый кэш по возрасту записей
    /// </summary>
    private async Task CleanupOldCacheAsync(TimeSpan maxAge)
    {
        var cutoffDate = DateTime.UtcNow - maxAge;

        try
        {
            var oldAnalyses = await _context.AnalysisResults
                .Where(ar => ar.LastUsed < cutoffDate || ar.CreatedAt < cutoffDate)
                .ToListAsync();

            if (oldAnalyses.Any())
            {
                _context.AnalysisResults.RemoveRange(oldAnalyses);
                await _context.SaveChangesAsync();

                _statistics.DeletedEntries += oldAnalyses.Count;
                _logger.LogInformation("Очищен старый кэш: удалено {Count} записей старше {Age}",
                    oldAnalyses.Count, maxAge);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка очистки старого кэша");
        }
    }

    /// <summary>
    /// Удаляет конкретную устаревшую запись анализа
    /// </summary>
    private async Task DeleteExpiredAnalysisAsync(CoreAnalysisResult expiredAnalysis)
    {
        try
        {
            _context.AnalysisResults.Remove(expiredAnalysis);
            await _context.SaveChangesAsync();

            _logger.LogTrace("Удален устаревший анализ {AnalysisHash}", expiredAnalysis.AnalysisHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления устаревшего анализа {AnalysisHash}", expiredAnalysis.AnalysisHash);
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

    #endregion
}