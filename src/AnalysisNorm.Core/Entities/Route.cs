using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Маршрут - основная сущность данных анализа
/// Полное соответствие Python DataFrame структуре из analyzer.py
/// </summary>
[Table("Routes")]
public class Route
{
    [Key]
    public int Id { get; set; }

    // === ИДЕНТИФИКАЦИЯ МАРШРУТА ===
    [Required]
    [StringLength(50)]
    public string RouteNumber { get; set; } = string.Empty;

    /// <summary>
    /// Дата поездки в формате строки (как в Python)
    /// </summary>
    [StringLength(20)]
    public string? TripDate { get; set; }

    /// <summary>
    /// Табельный номер машиниста
    /// </summary>
    [StringLength(20)]
    public string? DriverTab { get; set; }

    /// <summary>
    /// Уникальный ключ маршрута (генерируется автоматически)
    /// </summary>
    [StringLength(100)]
    public string? RouteKey { get; set; }

    // === ДАТА И ВРЕМЯ (Python date fields) ===
    /// <summary>
    /// Дата маршрута для индексирования
    /// </summary>
    public DateTime? RouteDate { get; set; }

    /// <summary>
    /// Дата маршрута в исходном формате Python
    /// </summary>
    [StringLength(20)]
    public string? Date { get; set; }

    // === УЧАСТОК И НОРМА ===
    [Required]
    [StringLength(200)]
    public string SectionName { get; set; } = string.Empty;

    /// <summary>
    /// Все участки маршрута через запятую (Python SectionNames)
    /// </summary>
    [StringLength(500)]
    public string? SectionNames { get; set; }

    /// <summary>
    /// Номер нормы из Python
    /// </summary>
    [StringLength(50)]
    public string? NormNumber { get; set; }

    /// <summary>
    /// ID нормы для связи с таблицей норм
    /// </summary>
    [StringLength(50)]
    public string? NormId { get; set; }

    // === ЛОКОМОТИВ ===
    [StringLength(50)]
    public string? LocomotiveSeries { get; set; }

    [StringLength(20)]
    public string? LocomotiveNumber { get; set; }

    /// <summary>
    /// Тип локомотива (Python LocomotiveType)
    /// </summary>
    [StringLength(50)]
    public string? LocomotiveType { get; set; }

    // === ОСНОВНЫЕ ДАННЫЕ РАБОТЫ (Python core data) ===
    /// <summary>
    /// Механическая работа (кВт⋅час) - Python MechanicalWork
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MechanicalWork { get; set; }

    /// <summary>
    /// Расход электроэнергии (кВт⋅час) - Python ElectricConsumption
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ElectricConsumption { get; set; }

    /// <summary>
    /// Норма расхода (кВт⋅час) - Python NormConsumption
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? NormConsumption { get; set; }

    /// <summary>
    /// Удельный расход (кВт⋅час/10⁴ ткм) - Python SpecificConsumption
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? SpecificConsumption { get; set; }

    /// <summary>
    /// Фактический расход для расчетов - Python FactConsumption
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? FactConsumption { get; set; }

    /// <summary>
    /// Фактическая работа для расчетов - Python WorkFact
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? WorkFact { get; set; }

    // === ФИЗИЧЕСКИЕ ПАРАМЕТРЫ ===
    /// <summary>
    /// Масса поезда (тонны) - Python TrainWeight
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? TrainWeight { get; set; }

    /// <summary>
    /// Масса нетто (тонны) - Python NettoTons
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? NettoTons { get; set; }

    /// <summary>
    /// Масса брутто (тонны) - Python BruttoTons
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? BruttoTons { get; set; }

    /// <summary>
    /// Количество осей - Python AxesCount
    /// </summary>
    public int? AxesCount { get; set; }

    /// <summary>
    /// Расстояние (км) - Python Distance
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal? Distance { get; set; }

    /// <summary>
    /// Время в пути (часы) - Python TravelTime
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? TravelTime { get; set; }

    /// <summary>
    /// Средняя скорость (км/ч) - Python AverageSpeed
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? AverageSpeed { get; set; }

    /// <summary>
    /// Километры (как в Python) - Python Kilometers
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal? Kilometers { get; set; }

    /// <summary>
    /// Тонно-километры - Python TonKilometers
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? TonKilometers { get; set; }

    // === АНАЛИЗ ОТКЛОНЕНИЙ ===
    /// <summary>
    /// Процент отклонения от нормы
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? DeviationPercent { get; set; }

    /// <summary>
    /// Статус отклонения
    /// </summary>
    public DeviationStatus DeviationStatus { get; set; } = DeviationStatus.Normal;

    /// <summary>
    /// КПД (эффективность) - Python Efficiency
    /// </summary>
    [Column(TypeName = "decimal(8,4)")]
    public decimal? Efficiency { get; set; }

    // === ДОПОЛНИТЕЛЬНАЯ ИНФОРМАЦИЯ ===
    /// <summary>
    /// Погодные условия - Python WeatherConditions
    /// </summary>
    [StringLength(100)]
    public string? WeatherConditions { get; set; }

    /// <summary>
    /// Комментарии - Python Comments
    /// </summary>
    [StringLength(500)]
    public string? Comments { get; set; }

    // === СИСТЕМНЫЕ ПОЛЯ ===
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Флаг обработки
    /// </summary>
    public bool IsProcessed { get; set; }

    // === НАВИГАЦИОННЫЕ СВОЙСТВА ===
    [ForeignKey(nameof(NormId))]
    public virtual Norm? Norm { get; set; }

    public virtual AnalysisResult? AnalysisResult { get; set; }

    /// <summary>
    /// Генерирует уникальный ключ маршрута (аналог extract_route_key из Python)
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

    /// <summary>
    /// Рассчитывает процент отклонения от нормы
    /// </summary>
    public void CalculateDeviation()
    {
        if (FactConsumption.HasValue && NormConsumption.HasValue && NormConsumption.Value > 0)
        {
            DeviationPercent = ((FactConsumption.Value - NormConsumption.Value) / NormConsumption.Value) * 100;
            DeviationStatus = DeviationStatus.GetStatus(DeviationPercent.Value);
        }
    }
}

/// <summary>
/// Статусы отклонения (соответствует Python StatusClassifier)
/// </summary>
public enum DeviationStatus
{
    EconomyStrong = -3,    // < -15%
    EconomyMedium = -2,    // -15% to -10%  
    EconomyWeak = -1,      // -10% to -5%
    Normal = 0,            // -5% to +5%
    OverrunWeak = 1,       // +5% to +10%
    OverrunMedium = 2,     // +10% to +15%
    OverrunStrong = 3      // > +15%
}

/// <summary>
/// Методы расширения для работы со статусами отклонения
/// </summary>
public static class DeviationStatusExtensions
{
    /// <summary>
    /// Определяет статус по проценту отклонения (Python get_status)
    /// </summary>
    public static DeviationStatus GetStatus(this DeviationStatus _, decimal deviationPercent)
    {
        return deviationPercent switch
        {
            < -15 => DeviationStatus.EconomyStrong,
            >= -15 and < -10 => DeviationStatus.EconomyMedium,
            >= -10 and < -5 => DeviationStatus.EconomyWeak,
            >= -5 and <= 5 => DeviationStatus.Normal,
            > 5 and <= 10 => DeviationStatus.OverrunWeak,
            > 10 and <= 15 => DeviationStatus.OverrunMedium,
            > 15 => DeviationStatus.OverrunStrong,
            _ => DeviationStatus.Normal
        };
    }

    /// <summary>
    /// Возвращает описание статуса на русском языке
    /// </summary>
    public static string GetDescription(this DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.EconomyStrong => "Сильная экономия",
            DeviationStatus.EconomyMedium => "Средняя экономия",
            DeviationStatus.EconomyWeak => "Слабая экономия",
            DeviationStatus.Normal => "Норма",
            DeviationStatus.OverrunWeak => "Слабый перерасход",
            DeviationStatus.OverrunMedium => "Средний перерасход",
            DeviationStatus.OverrunStrong => "Сильный перерасход",
            _ => "Неопределено"
        };
    }
}