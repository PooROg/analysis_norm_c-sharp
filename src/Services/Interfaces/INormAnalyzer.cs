using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Интерфейс для высокопроизводительного хранилища норм расхода
/// Поддерживает кэширование, интерполяцию и быстрый поиск
/// </summary>
public interface INormStorage
{
    /// <summary>
    /// Получить норму по идентификатору
    /// </summary>
    Task<Norm?> GetNormAsync(string normId);

    /// <summary>
    /// Получить все нормы из хранилища
    /// </summary>
    Task<IEnumerable<Norm>> GetAllNormsAsync();

    /// <summary>
    /// Сохранить норму в хранилище
    /// </summary>
    Task SaveNormAsync(Norm norm);

    /// <summary>
    /// Сохранить несколько норм одновременно (оптимизированная операция)
    /// </summary>
    Task SaveNormsAsync(IEnumerable<Norm> norms);

    /// <summary>
    /// Получить функцию интерполяции для нормы (кэшированная)
    /// </summary>
    Task<Func<decimal, decimal>?> GetInterpolationFunctionAsync(string normId);

    /// <summary>
    /// Поиск норм по критериям
    /// </summary>
    Task<IEnumerable<Norm>> SearchNormsAsync(string? type = null, string? pattern = null);

    /// <summary>
    /// Получить статистику хранилища
    /// </summary>
    Task<StorageInfo> GetStorageInfoAsync();

    /// <summary>
    /// Очистить кэш (для отладки и оптимизации памяти)
    /// </summary>
    Task ClearCacheAsync();

    /// <summary>
    /// Проверить валидность всех норм в хранилище
    /// </summary>
    Task<ValidationResult> ValidateNormsAsync();
}

/// <summary>
/// Информация о состоянии хранилища
/// </summary>
public record StorageInfo(
    int TotalNorms,
    int CachedFunctions,
    decimal CacheSizeMB,
    DateTime LastUpdated);

/// <summary>
/// Результат валидации норм
/// </summary>
public record ValidationResult(
    int ValidNorms,
    int InvalidNorms,
    IReadOnlyList<string> ValidationErrors);