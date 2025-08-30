using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Коэффициент расхода локомотива
/// Соответствует структуре из Python coefficients.py
/// </summary>
[Table("LocomotiveCoefficients")]
public class LocomotiveCoefficient
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Оригинальная серия локомотива (как в файле)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Series { get; set; } = string.Empty;

    /// <summary>
    /// Нормализованная серия (только буквы/цифры, верхний регистр)
    /// Соответствует normalize_series из Python
    /// </summary>
    [Required]
    [StringLength(50)]
    public string SeriesNormalized { get; set; } = string.Empty;

    /// <summary>
    /// Номер локомотива
    /// </summary>
    [Required]
    public int Number { get; set; }

    /// <summary>
    /// Коэффициент расхода (значение для корректировки фактического расхода)
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal Coefficient { get; set; }

    /// <summary>
    /// Отклонение от нормы в процентах
    /// (coefficient - 1.0) * 100.0
    /// </summary>
    [Column(TypeName = "decimal(18,3)")]
    public decimal DeviationPercent { get; set; }

    /// <summary>
    /// Общая работа локомотива (для фильтрации по min_work_threshold)
    /// </summary>
    [Column(TypeName = "decimal(18,3)")]
    public decimal WorkTotal { get; set; }

    // === СИСТЕМНЫЕ ПОЛЯ ===
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Источник данных коэффициента: "coefficient" или "percent"
    /// </summary>
    [StringLength(20)]
    public string? Source { get; set; }

    /// <summary>
    /// Нормализует название серии локомотива
    /// Соответствует normalize_series из Python coefficients.py
    /// </summary>
    /// <param name="series">Исходная серия</param>
    /// <returns>Нормализованная серия (только буквы/цифры, верхний регистр)</returns>
    public static string NormalizeSeries(string series)
    {
        if (string.IsNullOrEmpty(series)) return string.Empty;

        // Убираем все кроме букв и цифр, приводим к верхнему регистру
        var normalized = Regex.Replace(series.ToUpper(), @"[^А-ЯA-Z0-9]", string.Empty);
        return normalized;
    }

    /// <summary>
    /// Обновляет нормализованную серию и отклонение при изменении основных свойств
    /// </summary>
    public void UpdateCalculatedFields()
    {
        SeriesNormalized = NormalizeSeries(Series);
        DeviationPercent = (Coefficient - 1.0m) * 100.0m;
    }
    
        /// <summary>
    /// Фактическая работа (недостающее свойство)
    /// </summary>
    public decimal? WorkFact { get; set; }

    /// <summary>
    /// Нормативная работа (недостающее свойство)
    /// </summary>
    public decimal? WorkNorm { get; set; }
}

/// <summary>
/// Статистика коэффициентов по серии
/// Соответствует get_statistics из Python coefficients.py
/// </summary>
public class CoefficientStatistics
{
    public int TotalLocomotives { get; set; }
    public int SeriesCount { get; set; }
    public decimal AvgCoefficient { get; set; }
    public decimal MinCoefficient { get; set; }
    public decimal MaxCoefficient { get; set; }
    public decimal AvgDeviationPercent { get; set; }
    public int LocomotivesAboveNorm { get; set; }
    public int LocomotivesBelowNorm { get; set; }
    public int LocomotivesAtNorm { get; set; }
}