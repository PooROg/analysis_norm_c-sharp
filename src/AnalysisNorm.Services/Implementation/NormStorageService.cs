using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Data;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Полная реализация сервиса хранения норм с Entity Framework
/// Соответствует NormStorage из Python core/norm_storage.py с улучшениями
/// </summary>
public class NormStorageService : INormStorageService
{
    private readonly ILogger<NormStorageService> _logger;
    private readonly AnalysisNormDbContext _context;
    
    // Кэш метаданных норм для быстрого доступа
    private readonly Dictionary<string, NormMetadata> _metadataCache = new();
    private readonly SemaphoreSlim _metadataSemaphore = new(1, 1);
    private DateTime _lastMetadataUpdate = DateTime.MinValue;

    public NormStorageService(ILogger<NormStorageService> logger, AnalysisNormDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Добавляет или обновляет нормы в хранилище
    /// Соответствует add_or_update_norms из Python NormStorage
    /// </summary>
    public async Task<Dictionary<string, string>> AddOrUpdateNormsAsync(IEnumerable<Norm> norms)
    {
        var normsList = norms.ToList();
        var results = new Dictionary<string, string>();
        
        _logger.LogInformation("Добавляем/обновляем {Count} норм в хранилище", normsList.Count);

        if (!normsList.Any())
        {
            _logger.LogWarning("Получен пустой список норм для добавления");
            return results;
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var processedCount = 0;
            var updatedCount = 0;
            var addedCount = 0;
            var skippedCount = 0;

            foreach (var norm in normsList)
            {
                try
                {
                    if (string.IsNullOrEmpty(norm.NormId))
                    {
                        results[Guid.NewGuid().ToString()] = "error: Отсутствует идентификатор нормы";
                        skippedCount++;
                        continue;
                    }

                    // Проверяем валидность нормы
                    if (!IsNormValid(norm))
                    {
                        results[norm.NormId] = "error: Норма не прошла валидацию";
                        skippedCount++;
                        continue;
                    }

                    // Проверяем существует ли норма
                    var existingNorm = await _context.Norms
                        .Include(n => n.Points)
                        .FirstOrDefaultAsync(n => n.NormId == norm.NormId);

                    if (existingNorm != null)
                    {
                        // Обновляем существующую норму
                        var updateResult = await UpdateExistingNormAsync(existingNorm, norm);
                        results[norm.NormId] = updateResult;
                        if (updateResult.StartsWith("updated"))
                            updatedCount++;
                        else
                            skippedCount++;
                    }
                    else
                    {
                        // Добавляем новую норму
                        await AddNewNormAsync(norm);
                        results[norm.NormId] = "added";
                        addedCount++;
                    }

                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка обработки нормы {NormId}", norm.NormId ?? "unknown");
                    results[norm.NormId ?? Guid.NewGuid().ToString()] = $"error: {ex.Message}";
                    skippedCount++;
                }
            }

            // Сохраняем изменения
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Очищаем кэш метаданных
            await InvalidateMetadataCacheAsync();

            _logger.LogInformation("Обработка норм завершена: обработано {ProcessedCount}, добавлено {AddedCount}, обновлено {UpdatedCount}, пропущено {SkippedCount}", 
                processedCount, addedCount, updatedCount, skippedCount);

            return results;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Критическая ошибка добавления/обновления норм");
            
            // Добавляем ошибку для всех необработанных норм
            foreach (var norm in normsList.Where(n => !results.ContainsKey(n.NormId ?? "")))
            {
                results[norm.NormId ?? Guid.NewGuid().ToString()] = $"error: Транзакция отменена: {ex.Message}";
            }
            
            return results;
        }
    }

    /// <summary>
    /// Получает норму по идентификатору
    /// Соответствует get_norm из Python NormStorage
    /// </summary>
    public async Task<Norm?> GetNormAsync(string normId)
    {
        if (string.IsNullOrEmpty(normId))
        {
            _logger.LogWarning("Получен пустой идентификатор нормы");
            return null;
        }

        _logger.LogTrace("Получаем норму {NormId}", normId);

        try
        {
            var norm = await _context.Norms
                .Include(n => n.Points.OrderBy(p => p.Order))
                .Include(n => n.AnalysisResults)
                .AsSplitQuery() // Оптимизация для больших данных
                .FirstOrDefaultAsync(n => n.NormId == normId);

            if (norm != null)
            {
                _logger.LogTrace("Норма {NormId} найдена: {PointCount} точек, тип {NormType}", 
                    normId, norm.Points.Count, norm.NormType);
            }
            else
            {
                _logger.LogTrace("Норма {NormId} не найдена", normId);
            }

            return norm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения нормы {NormId}", normId);
            return null;
        }
    }

    /// <summary>
    /// Получает все нормы из хранилища
    /// </summary>
    public async Task<IEnumerable<Norm>> GetAllNormsAsync()
    {
        _logger.LogDebug("Получаем все нормы из хранилища");

        try
        {
            var norms = await _context.Norms
                .Include(n => n.Points.OrderBy(p => p.Order))
                .AsSplitQuery()
                .OrderBy(n => n.NormId)
                .ToListAsync();

            _logger.LogDebug("Получено {Count} норм", norms.Count);
            return norms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения всех норм");
            return new List<Norm>();
        }
    }

    /// <summary>
    /// Получает информацию о хранилище
    /// Соответствует get_storage_info из Python
    /// </summary>
    public async Task<StorageInfo> GetStorageInfoAsync()
    {
        _logger.LogTrace("Получаем информацию о хранилище норм");

        try
        {
            // Получаем основную статистику
            var totalNorms = await _context.Norms.CountAsync();
            var totalPoints = await _context.NormPoints.CountAsync();
            
            // Группируем нормы по типу
            var normsByType = await _context.Norms
                .GroupBy(n => n.NormType ?? "Неизвестно")
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type, x => x.Count);

            // Получаем дату последнего обновления
            var lastUpdated = await _context.Norms
                .OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                .Select(n => n.UpdatedAt ?? n.CreatedAt)
                .FirstOrDefaultAsync();

            var storageInfo = new StorageInfo
            {
                TotalNorms = totalNorms,
                TotalPoints = totalPoints,
                NormsByType = normsByType,
                LastUpdated = lastUpdated
            };

            _logger.LogTrace("Информация о хранилище: {NormCount} норм, {PointCount} точек", 
                totalNorms, totalPoints);

            return storageInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения информации о хранилище");
            return new StorageInfo
            {
                TotalNorms = 0,
                TotalPoints = 0,
                NormsByType = new Dictionary<string, int>(),
                LastUpdated = DateTime.MinValue
            };
        }
    }

    /// <summary>
    /// Получает нормы по типу
    /// </summary>
    public async Task<IEnumerable<Norm>> GetNormsByTypeAsync(string normType)
    {
        if (string.IsNullOrEmpty(normType))
            return new List<Norm>();

        _logger.LogDebug("Получаем нормы типа {NormType}", normType);

        try
        {
            var norms = await _context.Norms
                .Include(n => n.Points.OrderBy(p => p.Order))
                .Where(n => n.NormType == normType)
                .OrderBy(n => n.NormId)
                .ToListAsync();

            _logger.LogDebug("Найдено {Count} норм типа {NormType}", norms.Count, normType);
            return norms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения норм типа {NormType}", normType);
            return new List<Norm>();
        }
    }

    /// <summary>
    /// Получает метаданные нормы (быстрый доступ без точек)
    /// </summary>
    public async Task<NormMetadata?> GetNormMetadataAsync(string normId)
    {
        if (string.IsNullOrEmpty(normId))
            return null;

        await _metadataSemaphore.WaitAsync();
        try
        {
            // Обновляем кэш если устарел
            if (DateTime.UtcNow - _lastMetadataUpdate > TimeSpan.FromMinutes(10))
            {
                await RefreshMetadataCacheAsync();
            }

            return _metadataCache.TryGetValue(normId, out var metadata) ? metadata : null;
        }
        finally
        {
            _metadataSemaphore.Release();
        }
    }

    /// <summary>
    /// Получает все доступные типы норм
    /// </summary>
    public async Task<IEnumerable<string>> GetNormTypesAsync()
    {
        _logger.LogTrace("Получаем список типов норм");

        try
        {
            var types = await _context.Norms
                .Where(n => !string.IsNullOrEmpty(n.NormType))
                .Select(n => n.NormType!)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            _logger.LogTrace("Найдено {Count} типов норм", types.Count);
            return types;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения типов норм");
            return new List<string>();
        }
    }

    /// <summary>
    /// Удаляет норму из хранилища
    /// </summary>
    public async Task<bool> DeleteNormAsync(string normId)
    {
        if (string.IsNullOrEmpty(normId))
        {
            _logger.LogWarning("Получен пустой идентификатор для удаления нормы");
            return false;
        }

        _logger.LogInformation("Удаляем норму {NormId}", normId);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var norm = await _context.Norms
                .Include(n => n.Points)
                .Include(n => n.AnalysisResults)
                .FirstOrDefaultAsync(n => n.NormId == normId);

            if (norm == null)
            {
                _logger.LogWarning("Норма {NormId} не найдена для удаления", normId);
                return false;
            }

            // Удаляем связанные записи
            if (norm.Points.Any())
            {
                _context.NormPoints.RemoveRange(norm.Points);
                _logger.LogDebug("Удалено {Count} точек нормы {NormId}", norm.Points.Count, normId);
            }

            // Удаляем кэш интерполяции
            var cacheEntries = await _context.NormInterpolationCache
                .Where(c => c.NormId == normId)
                .ToListAsync();
            
            if (cacheEntries.Any())
            {
                _context.NormInterpolationCache.RemoveRange(cacheEntries);
                _logger.LogDebug("Удалено {Count} записей кэша для нормы {NormId}", cacheEntries.Count, normId);
            }

            // Обновляем связанные результаты анализа (устанавливаем NormId в null)
            foreach (var result in norm.AnalysisResults)
            {
                result.NormId = null;
            }

            // Удаляем саму норму
            _context.Norms.Remove(norm);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Очищаем кэш метаданных
            await InvalidateMetadataCacheAsync();

            _logger.LogInformation("Норма {NormId} успешно удалена", normId);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Ошибка удаления нормы {NormId}", normId);
            return false;
        }
    }

    /// <summary>
    /// Проверяет валидность нормы перед сохранением
    /// </summary>
    private bool IsNormValid(Norm norm)
    {
        if (string.IsNullOrEmpty(norm.NormId))
        {
            _logger.LogTrace("Норма невалидна: отсутствует идентификатор");
            return false;
        }

        if (!norm.Points.Any())
        {
            _logger.LogTrace("Норма {NormId} невалидна: отсутствуют точки", norm.NormId);
            return false;
        }

        var validPoints = norm.Points.Where(p => 
            p.Load >= AnalysisConstants.MinValidLoad && 
            p.Load <= AnalysisConstants.MaxValidLoad &&
            p.Consumption >= AnalysisConstants.MinValidConsumption && 
            p.Consumption <= AnalysisConstants.MaxValidConsumption
        ).ToList();

        if (validPoints.Count < 2)
        {
            _logger.LogTrace("Норма {NormId} невалидна: недостаточно валидных точек ({Count})", 
                norm.NormId, validPoints.Count);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Обновляет существующую норму
    /// </summary>
    private async Task<string> UpdateExistingNormAsync(Norm existingNorm, Norm newNorm)
    {
        _logger.LogDebug("Обновляем существующую норму {NormId}", existingNorm.NormId);

        try
        {
            // Проверяем нужно ли обновление
            if (AreNormsEqual(existingNorm, newNorm))
            {
                _logger.LogTrace("Норма {NormId} не изменилась, обновление не требуется", existingNorm.NormId);
                return "unchanged";
            }

            // Обновляем основные поля
            existingNorm.NormType = newNorm.NormType ?? existingNorm.NormType;
            existingNorm.UpdatedAt = DateTime.UtcNow;

            // Удаляем старые точки
            if (existingNorm.Points.Any())
            {
                _context.NormPoints.RemoveRange(existingNorm.Points);
            }

            // Добавляем новые точки
            foreach (var point in newNorm.Points)
            {
                point.NormId = existingNorm.NormId!;
                existingNorm.Points.Add(point);
            }

            // Очищаем кэш интерполяции для этой нормы
            var cacheEntries = await _context.NormInterpolationCache
                .Where(c => c.NormId == existingNorm.NormId)
                .ToListAsync();
            
            if (cacheEntries.Any())
            {
                _context.NormInterpolationCache.RemoveRange(cacheEntries);
                _logger.LogTrace("Очищен кэш интерполяции для обновленной нормы {NormId}", existingNorm.NormId);
            }

            return $"updated: {newNorm.Points.Count} точек";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления нормы {NormId}", existingNorm.NormId);
            return $"error: {ex.Message}";
        }
    }

    /// <summary>
    /// Добавляет новую норму
    /// </summary>
    private async Task AddNewNormAsync(Norm norm)
    {
        _logger.LogDebug("Добавляем новую норму {NormId}", norm.NormId);

        // Устанавливаем временные метки
        norm.CreatedAt = DateTime.UtcNow;
        norm.UpdatedAt = null;

        // Устанавливаем порядок точек
        var orderedPoints = norm.Points.OrderBy(p => p.Load).ToList();
        for (int i = 0; i < orderedPoints.Count; i++)
        {
            orderedPoints[i].Order = i + 1;
            orderedPoints[i].NormId = norm.NormId!;
        }

        norm.Points = orderedPoints;

        await _context.Norms.AddAsync(norm);
    }

    /// <summary>
    /// Сравнивает две нормы на равенство
    /// </summary>
    private bool AreNormsEqual(Norm existing, Norm newNorm)
    {
        if (existing.NormType != newNorm.NormType)
            return false;

        if (existing.Points.Count != newNorm.Points.Count)
            return false;

        var existingPoints = existing.Points.OrderBy(p => p.Load).ToList();
        var newPoints = newNorm.Points.OrderBy(p => p.Load).ToList();

        for (int i = 0; i < existingPoints.Count; i++)
        {
            if (Math.Abs(existingPoints[i].Load - newPoints[i].Load) > 0.01m ||
                Math.Abs(existingPoints[i].Consumption - newPoints[i].Consumption) > 0.01m)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Обновляет кэш метаданных
    /// </summary>
    private async Task RefreshMetadataCacheAsync()
    {
        try
        {
            var metadata = await _context.Norms
                .Select(n => new NormMetadata
                {
                    NormId = n.NormId!,
                    NormType = n.NormType ?? "Неизвестно",
                    PointCount = n.Points.Count,
                    MinLoad = n.Points.Any() ? n.Points.Min(p => p.Load) : 0,
                    MaxLoad = n.Points.Any() ? n.Points.Max(p => p.Load) : 0,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdatedAt
                })
                .ToListAsync();

            _metadataCache.Clear();
            foreach (var item in metadata)
            {
                _metadataCache[item.NormId] = item;
            }

            _lastMetadataUpdate = DateTime.UtcNow;
            _logger.LogTrace("Обновлен кэш метаданных: {Count} норм", metadata.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления кэша метаданных");
        }
    }

    /// <summary>
    /// Очищает кэш метаданных
    /// </summary>
    private async Task InvalidateMetadataCacheAsync()
    {
        await _metadataSemaphore.WaitAsync();
        try
        {
            _metadataCache.Clear();
            _lastMetadataUpdate = DateTime.MinValue;
            _logger.LogTrace("Кэш метаданных очищен");
        }
        finally
        {
            _metadataSemaphore.Release();
        }
    }
}

/// <summary>
/// Метаданные нормы (без точек для быстрого доступа)
/// </summary>
public class NormMetadata
{
    public string NormId { get; set; } = string.Empty;
    public string NormType { get; set; } = string.Empty;
    public int PointCount { get; set; }
    public decimal MinLoad { get; set; }
    public decimal MaxLoad { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}