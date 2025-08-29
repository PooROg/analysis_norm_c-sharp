using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Результат анализа участка
/// Полное соответствие Python statistics из analyzer.py
/// </summary>
[Table("AnalysisResults")]
public class AnalysisResult
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Название участка
    /// </summary>
    [Required]
    [StringLength(200)]
    public string SectionName { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор нормы (если анализ по конкретной норме)
    /// </summary>
    [StringLength(50)]
    public string? NormId { get; set; }

    /// <summary>
    /// Флаг "только один участок"
    /// </summary>
    public bool SingleSectionOnly { get; set; }

    /// <summary>
    /// Флаг применения коэффициентов
    /// </summary>
    public bool UseCoefficients { get; set; }

    // === ОСНОВНЫЕ СЧЕТЧИКИ (Python counters) ===
    /// <summary>
    /// Общее количество маршрутов - Python TotalRoutes
    /// </summary>
    public int TotalRoutes { get; set; }

    /// <summary>
    /// Количество обработанных маршрутов - Python AnalyzedRoutes
    /// </summary>
    public int AnalyzedRoutes { get; set; }

    /// <summary>
    /// Количество маршрутов, обработанных для анализа - Python ProcessedRoutes
    /// </summary>
    public int ProcessedRoutes { get; set; }

    // === ОСНОВНАЯ СТАТИСТИКА ===
    public int Total { get; set; }
    public int Processed { get; set; }
    public int Economy { get; set; }
    public int Normal { get; set; }
    public int Overrun { get; set; }

    // === ДЕТАЛЬНАЯ СТАТИСТИКА ПО ОТКЛОНЕНИЯМ ===
    /// <summary>
    /// Количество с сильной экономией - Python EconomyCount
    /// </summary>
    public int EconomyCount { get; set; }
    
    /// <summary>
    /// Количество с перерасходом - Python OverrunCount
    /// </summary>
    public int OverrunCount { get; set; }
    
    /// <summary>
    /// Количество в норме - Python NormalCount
    /// </summary>
    public int NormalCount { get; set; }

    public int EconomyStrong { get; set; }
    public int EconomyMedium { get; set; }
    public int EconomyWeak { get; set; }
    public int OverrunWeak { get; set; }
    public int OverrunMedium { get; set; }
    public int OverrunStrong { get; set; }

    // === СТАТИСТИЧЕСКИЕ ПОКАЗАТЕЛИ ===
    /// <summary>
    /// Среднее отклонение - Python AverageDeviation
    /// </summary>
    [Column(TypeName = "decimal(18,3)")]
    public decimal AverageDeviation { get; set; }

    /// <summary>
    /// Медиана отклонений - Python MedianDeviation
    /// </summary>
    [Column(TypeName = "decimal(18,3)")]
    public decimal MedianDeviation { get; set; }

    /// <summary>
    /// Минимальное отклонение - Python MinDeviation
    /// </summary>
    [Column(TypeName = "decimal(18,3)")]
    public decimal MinDeviation { get; set; }

    /// <summary>
    /// Максимальное отклонение - Python MaxDeviation
    /// </summary>
    [Column(TypeName = "decimal(18,3)")]
    public decimal MaxDeviation { get; set; }

    /// <summary>
    /// Среднеквадратическое отклонение - Python StandardDeviation
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal StandardDeviation { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal MeanDeviation { get; set; }

    // === ВРЕМЕННЫЕ МЕТКИ ===
    /// <summary>
    /// Время завершения анализа - Python CompletedAt
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время последнего использования - Python LastUsed
    /// </summary>
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата анализа - Python AnalysisDate
    /// </summary>
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время обработки в миллисекундах - Python ProcessingTimeMs
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    // === СИСТЕМНЫЕ ПОЛЯ ===
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Хэш для идентификации уникального анализа
    /// </summary>
    [StringLength(64)]
    public string AnalysisHash { get; set; } = string.Empty;

    /// <summary>
    /// Сообщение об ошибке (если есть) - Python ErrorMessage
    /// </summary>
    [StringLength(1000)]
    public string? ErrorMessage { get; set; }

    // === ДОПОЛНИТЕЛЬНЫЕ ДАННЫЕ ===
    /// <summary>
    /// Функции норм для интерполяции - Python NormFunctions
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? NormFunctions { get; set; }

    // === НАВИГАЦИОННЫЕ СВОЙСТВА ===
    [ForeignKey(nameof(NormId))]
    public virtual Norm? Norm { get; set; }

    public virtual ICollection<Route> Routes { get; set; } = new List<Route>();

    /// <summary>
    /// Генерирует хэш для кэширования анализа
    /// </summary>
    public void GenerateAnalysisHash()
    {
        var source = $"{SectionName}_{NormId ?? "ALL"}_{SingleSectionOnly}_{UseCoefficients}_{DateTime.Now:yyyyMMdd}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(source));
        AnalysisHash = Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Обновляет время последнего использования
    /// </summary>
    public void UpdateLastUsed()
    {
        LastUsed = DateTime.UtcNow;
    }

    /// <summary>
    /// Проверяет, актуален ли результат анализа
    /// </summary>
    public bool IsExpired(int expirationHours = 24)
    {
        return DateTime.UtcNow.Subtract(LastUsed).TotalHours > expirationHours;
    }
}