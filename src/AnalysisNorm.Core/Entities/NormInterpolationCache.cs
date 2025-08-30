// === AnalysisNorm.Core/Entities/NormInterpolationCache.cs ===
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Кэш интерполированных значений норм для оптимизации производительности
/// Соответствует кэшированию в Python NormStorage + scipy.interpolate
/// Оптимизировано для высокочастотных запросов интерполяции
/// ПРИМЕЧАНИЕ: Индексы настроены в AnalysisNormDbContext через Fluent API
/// </summary>
[Table("norm_interpolation_cache")]
public class NormInterpolationCache
{
    /// <summary>
    /// Первичный ключ кэша
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор нормы (внешний ключ)
    /// Соответствует Norm.NormId
    /// </summary>
    [Required]
    [StringLength(50)]
    public string NormId { get; set; } = string.Empty;

    /// <summary>
    /// Значение параметра нагрузки для интерполяции
    /// Обычно это Load в тоннах, для которой выполнялась интерполяция
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal ParameterValue { get; set; }

    /// <summary>
    /// Кэшированное интерполированное значение потребления
    /// Результат scipy.interpolate или Math.NET Numerics интерполяции
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal InterpolatedValue { get; set; }

    /// <summary>
    /// Тип интерполяции, использованной для вычисления
    /// (linear, cubic, spline и т.д.)
    /// </summary>
    [StringLength(20)]
    public string InterpolationType { get; set; } = "linear";

    /// <summary>
    /// Время создания записи в кэше
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время последнего использования для LRU eviction
    /// Обновляется при каждом обращении к кэшированному значению
    /// </summary>
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Количество обращений к этой записи кэша
    /// Используется для статистики и приоритизации
    /// </summary>
    public int HitCount { get; set; } = 1;

    /// <summary>
    /// Точность интерполяции (tolerance) использованная при создании
    /// Помогает решить, можно ли переиспользовать близкие значения
    /// </summary>
    [Column(TypeName = "decimal(18,8)")]
    public decimal Tolerance { get; set; } = 0.01m;

    /// <summary>
    /// Хэш исходных точек нормы для валидации актуальности
    /// Если норма изменилась, кэш становится невалидным
    /// </summary>
    [StringLength(64)]
    public string? NormPointsHash { get; set; }

    /// <summary>
    /// Флаг валидности записи кэша
    /// Может быть установлен в false при изменении исходной нормы
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Размер данных в байтах (для мониторинга использования памяти кэша)
    /// </summary>
    public int DataSize { get; set; }

    /// <summary>
    /// Версия алгоритма интерполяции
    /// Позволяет инвалидировать кэш при обновлении алгоритмов
    /// </summary>
    public int AlgorithmVersion { get; set; } = 1;

    #region Navigation Properties

    /// <summary>
    /// Связь с исходной нормой
    /// Каскадное удаление при удалении нормы
    /// </summary>
    [ForeignKey(nameof(NormId))]
    public virtual Norm Norm { get; set; } = null!;

    #endregion

    #region Методы для оптимизации

    /// <summary>
    /// Проверяет, не устарела ли запись кэша
    /// </summary>
    /// <param name="maxAge">Максимальный возраст записи</param>
    /// <returns>true если запись устарела</returns>
    public bool IsExpired(TimeSpan maxAge)
    {
        return DateTime.UtcNow - CreatedAt > maxAge;
    }

    /// <summary>
    /// Проверяет, подходит ли кэшированное значение для заданной нагрузки
    /// с учетом толерантности
    /// </summary>
    /// <param name="requestedLoad">Запрашиваемая нагрузка</param>
    /// <returns>true если можно использовать это кэшированное значение</returns>
    public bool IsApplicableForLoad(decimal requestedLoad)
    {
        return Math.Abs(ParameterValue - requestedLoad) <= Tolerance;
    }

    /// <summary>
    /// Обновляет статистику использования записи
    /// Thread-safe инкремент счетчиков
    /// </summary>
    public void UpdateUsageStatistics()
    {
        LastUsed = DateTime.UtcNow;
        Interlocked.Increment(ref _hitCountField);
    }

    // Backing field для thread-safe операций
    private int _hitCountField = 1;

    /// <summary>
    /// Thread-safe свойство для HitCount
    /// </summary>
    public int HitCountThreadSafe 
    { 
        get => _hitCountField;
        set => _hitCountField = value;
    }

    /// <summary>
    /// Вычисляет приоритет записи для LRU eviction
    /// Комбинирует частоту использования и время последнего обращения
    /// </summary>
    public double GetEvictionPriority()
    {
        var daysSinceLastUsed = (DateTime.UtcNow - LastUsed).TotalDays;
        var hitFrequency = HitCount / Math.Max(1, (DateTime.UtcNow - CreatedAt).TotalDays);
        
        // Чем меньше значение, тем выше приоритет на удаление
        return daysSinceLastUsed / Math.Max(0.1, hitFrequency);
    }

    /// <summary>
    /// Генерирует хэш для точек нормы для валидации актуальности кэша
    /// </summary>
    public static string GenerateNormPointsHash(IEnumerable<NormPoint> points)
    {
        var sortedPoints = points.OrderBy(p => p.Load).ToList();
        var source = string.Join("|", sortedPoints.Select(p => $"{p.Load:F6}:{p.Consumption:F6}"));
        
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash);
    }

    #endregion

    #region Константы для настройки производительности

    /// <summary>
    /// Константы для управления кэшем
    /// </summary>
    public static class CacheSettings
    {
        /// <summary>Максимальный возраст записи кэша по умолчанию</summary>
        public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(24);
        
        /// <summary>Толерантность по умолчанию для поиска близких значений</summary>
        public const decimal DefaultTolerance = 0.01m;
        
        /// <summary>Максимальное количество записей в кэше на одну норму</summary>
        public const int MaxEntriesPerNorm = 1000;
        
        /// <summary>Порог для автоматической очистки кэша</summary>
        public const int CleanupThreshold = 10000;
    }

    #endregion

    #region Override для правильной работы с EF Core

    /// <summary>
    /// Переопределение ToString для отладки
    /// </summary>
    public override string ToString()
    {
        return $"NormCache[{NormId}]: Load={ParameterValue:F3} → Value={InterpolatedValue:F6} (Hits: {HitCount}, LastUsed: {LastUsed:MM-dd HH:mm})";
    }

    /// <summary>
    /// Equals для правильного сравнения в коллекциях
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not NormInterpolationCache other) return false;
        return Id == other.Id && 
               NormId == other.NormId && 
               Math.Abs(ParameterValue - other.ParameterValue) < 0.000001m;
    }

    /// <summary>
    /// GetHashCode для правильной работы в Dictionary/HashSet
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Id, NormId, ParameterValue);
    }

    #endregion
}