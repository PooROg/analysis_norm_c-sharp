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
    public int Total { get; set; }
    public int Processed { get; set; }
    public int Economy { get; set; }
    public int Normal { get; set; }
    public int Overrun { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal MeanDeviation { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal MedianDeviation { get; set; }

    // === ДЕТАЛЬНАЯ СТАТИСТИКА ===
    public int EconomyStrong { get; set; }
    public int EconomyMedium { get; set; }
    public int EconomyWeak { get; set; }
    public int OverrunWeak { get; set; }
    public int OverrunMedium { get; set; }
    public int OverrunStrong { get; set; }

    // === СИСТЕМНЫЕ ПОЛЯ ===
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Хэш для идентификации уникального анализа
    /// </summary>
    [StringLength(64)]
    public string AnalysisHash { get; set; } = string.Empty;

    // === НАВИГАЦИОННЫЕ СВОЙСТВА ===
    [ForeignKey(nameof(NormId))]
    public virtual Norm? Norm { get; set; }

    public virtual ICollection<Route> Routes { get; set; } = new List<Route>();

    /// <summary>
    /// Генерирует хэш для кэширования анализа
    /// </summary>
    public void GenerateAnalysisHash()
    {
        var source = $"{SectionName}_{NormId ?? "ALL"}_{SingleSectionOnly}_{UseCoefficients}";
        AnalysisHash = ComputeHash(source);
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }
}

/// <summary>
/// Кэш результатов интерполяции норм для повышения производительности
/// </summary>
[Table("NormInterpolationCache")]
public class NormInterpolationCache
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор нормы
    /// </summary>
    [Required]
    [StringLength(50)]
    public string NormId { get; set; } = string.Empty;

    /// <summary>
    /// Значение параметра (нагрузка на ось или вес)
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal ParameterValue { get; set; }

    /// <summary>
    /// Интерполированное значение расхода
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal InterpolatedValue { get; set; }

    /// <summary>
    /// Дата последнего использования (для очистки кэша)
    /// </summary>
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    // === НАВИГАЦИОННЫЕ СВОЙСТВА ===
    [ForeignKey(nameof(NormId))]
    public virtual Norm Norm { get; set; } = null!;
}

/// <summary>
/// Статусы отклонений (соответствует StatusClassifier из Python utils.py)
/// </summary>
public static class DeviationStatus
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
        return deviation switch
        {
            < StrongEconomyThreshold => EconomyStrong,
            < MediumEconomyThreshold => EconomyMedium,
            < WeakEconomyThreshold => EconomyWeak,
            <= NormalUpperThreshold => Normal,
            <= WeakOverrunThreshold => OverrunWeak,
            <= MediumOverrunThreshold => OverrunMedium,
            _ => OverrunStrong
        };
    }

    /// <summary>
    /// Возвращает цвет для статуса
    /// Соответствует get_status_color из Python StatusClassifier
    /// </summary>
    public static string GetStatusColor(string status)
    {
        return status switch
        {
            EconomyStrong => "darkgreen",
            EconomyMedium => "green", 
            EconomyWeak => "lightgreen",
            Normal => "blue",
            OverrunWeak => "orange",
            OverrunMedium => "darkorange", 
            OverrunStrong => "red",
            _ => "gray"
        };
    }
}