using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Маршрут - основная единица данных, эквивалент строки в Python DataFrame
/// Соответствует структуре данных из html_route_processor.py
/// </summary>
[Table("Routes")]
public class Route
{
    [Key]
    public int Id { get; set; }

    // === ОСНОВНАЯ ИНФОРМАЦИЯ МАРШРУТА ===
    [Required]
    [StringLength(50)]
    public string RouteNumber { get; set; } = string.Empty;

    [Required]
    public DateTime RouteDate { get; set; }

    [StringLength(50)]
    public string? TripDate { get; set; }

    [StringLength(50)]
    public string? DriverTab { get; set; }

    [StringLength(100)]
    public string? Depot { get; set; }

    [StringLength(50)]
    public string? Identifier { get; set; }

    // === ЛОКОМОТИВ ===
    [StringLength(20)]
    public string? LocomotiveSeries { get; set; }

    [StringLength(20)]
    public string? LocomotiveNumber { get; set; }

    // === ТЕХНИЧЕСКИЕ ХАРАКТЕРИСТИКИ ===
    public double? Netto { get; set; }
    public double? Brutto { get; set; }
    public int? Osi { get; set; }

    /// <summary>
    /// Флаг использования красного цвета для НЕТТО/БРУТТО/ОСИ (из Python USE_RED_COLOR)
    /// </summary>
    public bool UseRedColor { get; set; }

    /// <summary>
    /// Флаг использования красного цвета для расходов (из Python USE_RED_RASHOD)
    /// </summary>
    public bool UseRedRashod { get; set; }

    // === УЧАСТОК ===
    [Required]
    [StringLength(200)]
    public string SectionName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? NormNumber { get; set; }

    [StringLength(20)]
    public string? DoubleTraction { get; set; }

    // === ОСНОВНЫЕ ПОКАЗАТЕЛИ ===
    [Column(TypeName = "decimal(18,3)")]
    public decimal? TkmBrutto { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? Km { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? Pr { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? RashodFact { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? RashodNorm { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? UdNorma { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? AxleLoad { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? NormaWork { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? FactUd { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? FactWork { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? NormaSingle { get; set; }

    // === ДОПОЛНИТЕЛЬНЫЕ СОСТАВЛЯЮЩИЕ ===
    // Простой с бригадой
    [Column(TypeName = "decimal(18,3)")]
    public decimal? IdleBrigadaTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? IdleBrigadaNorm { get; set; }

    // Маневры
    [Column(TypeName = "decimal(18,3)")]
    public decimal? ManevryTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? ManevryNorm { get; set; }

    // Трогание с места
    [Column(TypeName = "decimal(18,3)")]
    public decimal? StartTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? StartNorm { get; set; }

    // Нагон опозданий
    [Column(TypeName = "decimal(18,3)")]
    public decimal? DelayTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? DelayNorm { get; set; }

    // Ограничения скорости
    [Column(TypeName = "decimal(18,3)")]
    public decimal? SpeedLimitTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? SpeedLimitNorm { get; set; }

    // На пересылаемые локомотивы
    [Column(TypeName = "decimal(18,3)")]
    public decimal? TransferLocoTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TransferLocoNorm { get; set; }

    // === МЕТАДАННЫЕ ОБРАБОТКИ ===
    [StringLength(20)]
    public string? DuplicatesCount { get; set; }

    [StringLength(10)]
    public string? NEqualsF { get; set; }

    // === АНАЛИЗ (рассчитывается при анализе) ===
    [Column(TypeName = "decimal(18,6)")]
    public decimal? NormInterpolated { get; set; }

    [StringLength(50)]
    public string? NormalizationParameter { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? ParameterValue { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? DeviationPercent { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    // === КОЭФФИЦИЕНТЫ ===
    [Column(TypeName = "decimal(18,6)")]
    public decimal? Coefficient { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? FactUdOriginal { get; set; }

    // === СИСТЕМНЫЕ ПОЛЯ ===
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Хэш для идентификации дубликатов (номер_маршрута + дата_поездки + табельный)
    /// Эквивалент extract_route_key из Python utils.py
    /// </summary>
    [StringLength(200)]
    public string? RouteKey { get; set; }

    // === НАВИГАЦИОННЫЕ СВОЙСТВА ===
    public virtual ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();

    /// <summary>
    /// Вычисляет ключ маршрута для группировки дубликатов
    /// Соответствует extract_route_key из Python utils.py
    /// </summary>
    public void GenerateRouteKey()
    {
        if (!string.IsNullOrEmpty(RouteNumber) && 
            !string.IsNullOrEmpty(TripDate) && 
            !string.IsNullOrEmpty(DriverTab))
        {
            RouteKey = $"{RouteNumber}_{TripDate}_{DriverTab}";
        }
    }
}