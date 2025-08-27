using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Норма расхода электроэнергии
/// Соответствует структуре norm_data из Python norm_storage.py
/// </summary>
[Table("Norms")]
public class Norm
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор нормы (извлекается из HTML)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string NormId { get; set; } = string.Empty;

    /// <summary>
    /// Тип нормы: "Нажатие" (по нагрузке на ось) или "Вес" (по весу поезда)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string NormType { get; set; } = "Нажатие";

    /// <summary>
    /// Описание нормы
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    // === BASE_DATA - метаданные нормы из Python ===
    public int? PriznokSostTyag { get; set; }
    public int? PriznokRek { get; set; }
    
    [StringLength(100)]
    public string? VidDvizheniya { get; set; }
    
    public int? SimvolRodRaboty { get; set; }
    public int? Rps { get; set; }
    public int? IdentifGruppy { get; set; }
    public int? PriznokSost { get; set; }
    public int? PriznokAlg { get; set; }
    
    [StringLength(50)]
    public string? DateStart { get; set; }
    
    [StringLength(50)]
    public string? DateEnd { get; set; }

    // === СИСТЕМНЫЕ ПОЛЯ ===
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // === НАВИГАЦИОННЫЕ СВОЙСТВА ===
    /// <summary>
    /// Точки нормы (Load, Consumption)
    /// </summary>
    public virtual ICollection<NormPoint> Points { get; set; } = new List<NormPoint>();

    public virtual ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();
}

/// <summary>
/// Точка нормы - пара (нагрузка, расход)
/// Соответствует элементу массива points из Python norm_storage.py
/// </summary>
[Table("NormPoints")]
public class NormPoint
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Внешний ключ к норме
    /// </summary>
    public int NormId { get; set; }

    /// <summary>
    /// Значение по оси X: нагрузка на ось (т/ось) или вес поезда (т)
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal Load { get; set; }

    /// <summary>
    /// Значение по оси Y: удельный расход (кВт·ч/10⁴ ткм)
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal Consumption { get; set; }

    /// <summary>
    /// Тип точки: "base" (из файла норм) или "additional" (из маршрутов)
    /// </summary>
    [Required]
    [StringLength(20)]
    public string PointType { get; set; } = "base";

    /// <summary>
    /// Порядковый номер точки в норме
    /// </summary>
    public int Order { get; set; }

    // === НАВИГАЦИОННЫЕ СВОЙСТВА ===
    [ForeignKey(nameof(NormId))]
    public virtual Norm Norm { get; set; } = null!;
}