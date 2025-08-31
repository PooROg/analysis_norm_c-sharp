// === AnalysisNorm.Core/Entities/Route.cs ===
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Маршрут движения локомотива с полным набором свойств
/// ИСПРАВЛЕНО: Добавлены все недостающие свойства из ошибок компиляции
/// </summary>
[Table("Routes")]
public class Route
{
    #region Primary Key

    [Key]
    public int Id { get; set; }

    #endregion

    #region Basic Route Info

    /// <summary>
    /// Номер маршрута
    /// </summary>
    [MaxLength(50)]
    public string? RouteNumber { get; set; }

    /// <summary>
    /// Дата маршрута - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Название участка
    /// </summary>
    [MaxLength(200)]
    public string? SectionName { get; set; }

    /// <summary>
    /// Номер нормы - ИСПРАВЛЕНО: добавлено недостающее свойство  
    /// </summary>
    [MaxLength(50)]
    public string? NormId { get; set; }

    /// <summary>
    /// Номер нормы (альтернативное название для совместимости)
    /// </summary>
    [MaxLength(50)]
    public string? NormNumber { get; set; }

    #endregion

    #region Locomotive Info

    /// <summary>
    /// Серия локомотива
    /// </summary>
    [MaxLength(20)]
    public string? LocomotiveSeries { get; set; }

    /// <summary>
    /// Номер локомотива
    /// </summary>
    public int? LocomotiveNumber { get; set; }

    /// <summary>
    /// Тип локомотива - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    [MaxLength(50)]
    public string? LocomotiveType { get; set; }

    /// <summary>
    /// Депо приписки
    /// </summary>
    [MaxLength(100)]
    public string? Depot { get; set; }

    #endregion

    #region Load and Weight Data

    /// <summary>
    /// Масса нетто (тонны)
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal? NettoTons { get; set; }

    /// <summary>
    /// Масса брутто (тонны)
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal? BruttoTons { get; set; }

    /// <summary>
    /// Масса поезда - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal? TrainWeight { get; set; }

    /// <summary>
    /// Количество осей
    /// </summary>
    public int? AxesCount { get; set; }

    /// <summary>
    /// Нагрузка на ось (тонны)
    /// </summary>
    [Column(TypeName = "decimal(8,3)")]
    public decimal? AxleLoad { get; set; }

    #endregion

    #region Distance and Time

    /// <summary>
    /// Расстояние (км)
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? Distance { get; set; }

    /// <summary>
    /// Время в пути - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    public TimeSpan? TravelTime { get; set; }

    /// <summary>
    /// Средняя скорость - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal? AverageSpeed { get; set; }

    /// <summary>
    /// Тонно-километры
    /// </summary>
    [Column(TypeName = "decimal(12,3)")]
    public decimal? TonKilometers { get; set; }

    #endregion

    #region Energy Consumption

    /// <summary>
    /// Фактический расход электроэнергии (кВт*ч)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? FactConsumption { get; set; }

    /// <summary>
    /// Нормативный расход электроэнергии (кВт*ч)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? NormConsumption { get; set; }

    /// <summary>
    /// Фактический удельный расход (кВт*ч на 10000 ткм)
    /// </summary>
    [Column(TypeName = "decimal(8,4)")]
    public decimal? FactUd { get; set; }

    /// <summary>
    /// Нормативный удельный расход (кВт*ч на 10000 ткм)
    /// </summary>
    [Column(TypeName = "decimal(8,4)")]
    public decimal? NormUd { get; set; }

    /// <summary>
    /// Удельный расход - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    [Column(TypeName = "decimal(8,4)")]
    public decimal? SpecificConsumption { get; set; }

    #endregion

    #region Analysis Results

    /// <summary>
    /// Отклонение в процентах
    /// </summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal? DeviationPercent { get; set; }

    /// <summary>
    /// Статус отклонения (экономия, норма, перерасход)
    /// </summary>
    public DeviationStatus Status { get; set; } = DeviationStatus.Normal;

    /// <summary>
    /// Эффективность - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    [Column(TypeName = "decimal(6,4)")]
    public decimal? Efficiency { get; set; }

    #endregion

    #region Additional Data

    /// <summary>
    /// Погодные условия - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    [MaxLength(100)]
    public string? WeatherConditions { get; set; }

    /// <summary>
    /// Комментарии - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    [MaxLength(500)]
    public string? Comments { get; set; }

    /// <summary>
    /// Дата создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата последнего изменения
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Связанная норма расхода
    /// </summary>
    [ForeignKey(nameof(NormId))]
    public virtual Norm? Norm { get; set; }

    #endregion

    #region Calculated Properties

    /// <summary>
    /// Экономия/перерасход в абсолютных единицах (кВт*ч)
    /// </summary>
    [NotMapped]
    public decimal? DeviationAbsolute
    {
        get
        {
            if (!FactConsumption.HasValue || !NormConsumption.HasValue)
                return null;
            return FactConsumption.Value - NormConsumption.Value;
        }
    }

    /// <summary>
    /// Признак экономии электроэнергии
    /// </summary>
    [NotMapped]
    public bool IsEconomy => DeviationPercent < 0;

    /// <summary>
    /// Признак перерасхода электроэнергии  
    /// </summary>
    [NotMapped]
    public bool IsOverrun => DeviationPercent > 5; // Порог перерасхода 5%

    /// <summary>
    /// Полное название локомотива (серия + номер)
    /// </summary>
    [NotMapped]
    public string FullLocomotiveName
    {
        get
        {
            if (string.IsNullOrEmpty(LocomotiveSeries))
                return "Неизвестно";

            if (!LocomotiveNumber.HasValue)
                return LocomotiveSeries;

            return $"{LocomotiveSeries}-{LocomotiveNumber}";
        }
    }

    /// <summary>
    /// Уникальный ключ маршрута для группировки дубликатов
    /// </summary>
    [NotMapped]
    public string RouteKey
    {
        get
        {
            var parts = new[]
            {
                RouteNumber ?? "Unknown",
                Date?.ToString("yyyy-MM-dd") ?? "NoDate",
                LocomotiveSeries ?? "NoSeries",
                LocomotiveNumber?.ToString() ?? "NoNumber"
            };
            return string.Join("_", parts);
        }
    }

    #endregion

    #region Override Methods

    public override string ToString()
    {
        return $"Route {RouteNumber}: {FullLocomotiveName} on {Date:dd.MM.yyyy} - {DeviationPercent:F1}%";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Route other) return false;
        return Id == other.Id || RouteKey == other.RouteKey;
    }

    public override int GetHashCode()
    {
        return RouteKey.GetHashCode();
    }

    #endregion
}