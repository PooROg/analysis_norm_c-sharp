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

    // ИСПРАВЛЕНО: LocomotiveNumber как int? вместо string?
    public int? LocomotiveNumber { get; set; }

    // === ТЕХНИЧЕСКИЕ ХАРАКТЕРИСТИКИ ===
    // ИСПРАВЛЕНО: Правильные названия полей и типы
    [Column(TypeName = "decimal(18,3)")]
    public decimal? NettoTons { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? BruttoTons { get; set; }

    public int? AxesCount { get; set; }

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
    public decimal? TonKilometers { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? Kilometers { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? MechanicalWork { get; set; }

    // ИСПРАВЛЕНО: Правильные названия полей расходов
    [Column(TypeName = "decimal(18,3)")]
    public decimal? FactConsumption { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? NormConsumption { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? ElectricConsumption { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? UdNorma { get; set; }

    // ИСПРАВЛЕНО: AxleLoad как decimal? вместо string?
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
    // ИСПРАВЛЕНО: DuplicatesCount как int? вместо string?
    public int? DuplicatesCount { get; set; }

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

    // ИСПРАВЛЕНО: Status остается string? но с правильными значениями
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
    /// Табельный номер машиниста (недостающее свойство)
    /// </summary>
    public string? DriverTabNumber { get; set; }

    /// <summary>
    /// Названия участков (недостающее свойство)
    /// </summary>
    public List<string> SectionNames { get; set; } = new();

    /// <summary>
    /// Длина поезда (недостающее свойство)
    /// </summary>
    public decimal? TrainLength { get; set; }

    /// <summary>
    /// Время в пути (недостающее свойство)
    /// </summary>
    public TimeSpan? TripTime { get; set; }

    /// <summary>
    /// Расстояние (недостающее свойство)
    /// </summary>
    public decimal? Distance { get; set; }

    /// <summary>
    /// Нормативный удельный расход (недостающее свойство)
    /// </summary>
    public decimal? NormUd { get; set; }

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

    /// <summary>
    /// Вычисляет производные параметры (нагрузка на ось, удельный расход)
    /// Соответствует calculate_derived_fields из Python
    /// </summary>
    public void CalculateDerivedFields()
    {
        // Нагрузка на ось = Брутто / Количество осей
        if (BruttoTons.HasValue && AxesCount.HasValue && AxesCount > 0)
        {
            AxleLoad = Math.Round(BruttoTons.Value / AxesCount.Value, 3);
        }

        // Удельный расход = Электроэнергия / Тонно-километры * 10000
        if (ElectricConsumption.HasValue && TonKilometers.HasValue && TonKilometers > 0)
        {
            FactUd = Math.Round(ElectricConsumption.Value / TonKilometers.Value * 10000, 6);
        }

        // Механическая работа
        if (TonKilometers.HasValue)
        {
            MechanicalWork = TonKilometers.Value;
        }
    }

    /// <summary>
    /// Вычисляет отклонение от нормы в процентах
    /// Соответствует calculate_deviation из Python
    /// </summary>
    public void CalculateDeviation()
    {
        if (FactConsumption.HasValue && NormConsumption.HasValue && NormConsumption > 0)
        {
            var deviation = (FactConsumption.Value - NormConsumption.Value) / NormConsumption.Value * 100;
            DeviationPercent = Math.Round(deviation, 2);
            
            // Устанавливаем статус на основе отклонения
            Status = GetDeviationStatus(deviation);
            
            // Флаги для визуализации
            UseRedColor = Math.Abs(deviation) > 10.0m;
            UseRedRashod = deviation > 5.0m;
        }
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Определяет статус отклонения без switch expression
    /// Решение для CS8510
    /// </summary>
    private string GetDeviationStatus(decimal deviation)
    {
        // Используем простые if-else вместо проблемного switch
        if (deviation <= -30m) return "Экономия сильная";
        if (deviation <= -20m) return "Экономия средняя";
        if (deviation <= -5m) return "Экономия слабая";
        if (deviation <= 5m) return "Норма";
        if (deviation <= 20m) return "Перерасход слабый";
        if (deviation <= 30m) return "Перерасход средний";
        return "Перерасход сильный";
    }
}