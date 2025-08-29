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

/// <summary>
/// СОЗДАТЬ: Единый класс для констант статусов отклонений  
/// Заменяет все дублирующиеся DeviationStatus static классы
/// </summary>
public static class DeviationStatusConstants
{
    public const string EconomyStrong = "Экономия сильная";
    public const string EconomyMedium = "Экономия средняя";  
    public const string EconomyWeak = "Экономия слабая";
    public const string Normal = "Норма";
    public const string OverrunWeak = "Перерасход слабый";
    public const string OverrunMedium = "Перерасход средний";
    public const string OverrunStrong = "Перерасход сильный";

    // Пороги отклонений (из Python StatusClassifier)
    public const decimal StrongEconomyThreshold = -30m;
    public const decimal MediumEconomyThreshold = -20m;
    public const decimal WeakEconomyThreshold = -5m;
    public const decimal NormalUpperThreshold = 5m;
    public const decimal WeakOverrunThreshold = 20m;
    public const decimal MediumOverrunThreshold = 30m;

    /// <summary>
    /// Определяет статус по отклонению в процентах
    /// Соответствует get_status из Python StatusClassifier
    /// </summary>
    public static string GetStatus(decimal deviation)
    {
        // ИСПРАВЛЕНО: Используем простые if-else вместо switch expression 
        // для решения проблемы CS8510
        if (deviation <= StrongEconomyThreshold) return EconomyStrong;
        if (deviation <= MediumEconomyThreshold) return EconomyMedium;
        if (deviation <= WeakEconomyThreshold) return EconomyWeak;
        if (deviation <= NormalUpperThreshold) return Normal;
        if (deviation <= WeakOverrunThreshold) return OverrunWeak;
        if (deviation <= MediumOverrunThreshold) return OverrunMedium;
        return OverrunStrong;
    }

    /// <summary>
    /// Возвращает цвет для статуса
    /// Соответствует get_status_color из Python StatusClassifier
    /// </summary>
    public static string GetStatusColor(string status)
    {
        if (status == EconomyStrong) return "darkgreen";
        if (status == EconomyMedium) return "green";
        if (status == EconomyWeak) return "lightgreen";
        if (status == Normal) return "blue";
        if (status == OverrunWeak) return "orange";
        if (status == OverrunMedium) return "darkorange";
        if (status == OverrunStrong) return "red";
        return "gray";
    }
}