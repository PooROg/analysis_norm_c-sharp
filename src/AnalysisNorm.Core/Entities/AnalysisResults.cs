using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Результат анализа участка
/// Соответствует statistics из Python analyzer.py
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

    // === ОСНОВНАЯ СТАТИСТИКА ===
    public int TotalRoutes { get; set; }
    public int ProcessedRoutes { get; set; }
    public int AnalyzedRoutes { get; set; }
    public int Economy { get; set; }
    public int Normal { get; set; }
    public int Overrun { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal AverageDeviation { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal MinDeviation { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal MaxDeviation { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal MedianDeviation { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal StandardDeviation { get; set; }

    // === ДЕТАЛЬНАЯ СТАТИСТИКА ===
    public int EconomyStrong { get; set; }
    public int EconomyMedium { get; set; }
    public int EconomyWeak { get; set; }
    public int OverrunWeak { get; set; }
    public int OverrunMedium { get; set; }
    public int OverrunStrong { get; set; }

    // === СИСТЕМНЫЕ ПОЛЯ ===
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CompletedAt { get; set; }
    public DateTime LastUsed { get; set; }

    /// <summary>
    /// Хэш для идентификации уникального анализа
    /// </summary>
    [StringLength(64)]
    public string AnalysisHash { get; set; } = string.Empty;

    /// <summary>
    /// Время обработки в миллисекундах
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Сообщение об ошибке (если есть)
    /// </summary>
    [StringLength(1000)]
    public string? ErrorMessage { get; set; }

    // === НАВИГАЦИОННЫЕ СВОЙСТВА ===
    [ForeignKey(nameof(NormId))]
    public virtual Norm? Norm { get; set; }

    public virtual ICollection<Route> Routes { get; set; } = new List<Route>();

    /// <summary>
    /// Статистика анализа (JSON)
    /// </summary>
    public string? Statistics { get; set; }

    /// <summary>
    /// Генерирует хэш для кэширования анализа
    /// </summary>
    public void GenerateAnalysisHash()
    {
        var source = $"{SectionName}_{NormId ?? "all"}_{SingleSectionOnly}_{UseCoefficients}_{DateTime.UtcNow:yyyyMMddHH}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(source));
        AnalysisHash = Convert.ToHexString(hash)[..16]; // Берем первые 16 символов
    }
}