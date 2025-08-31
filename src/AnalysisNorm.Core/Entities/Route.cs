using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// ВОССТАНОВЛЕНО: Полная версия маршрута, объединяющая исходную функциональность
/// с новыми исправленными свойствами
/// </summary>
[Table("Routes")]
public class Route
{
    #region Primary Key

    [Key]
    public int Id { get; set; }

    #endregion

    #region Basic Route Info - ВОССТАНОВЛЕНО из исходного

    /// <summary>
    /// Номер маршрута
    /// </summary>
    [MaxLength(50)]
    public string? RouteNumber { get; set; }

    /// <summary>
    /// Название маршрута - ВОССТАНОВЛЕНО: было потеряно при исправлении
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Дата маршрута - ИСПРАВЛЕНО: восстановлено из исходного
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Дата поездки - ИСПРАВЛЕНО: добавлено недостающее свойство из ошибок компиляции
    /// </summary>
    public DateTime TripDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Название участка - ВОССТАНОВЛЕНО
    /// </summary>
    [MaxLength(200)]
    public string? SectionName { get; set; }

    /// <summary>
    /// Номер нормы - ИСПРАВЛЕНО: добавлено недостающее свойство из ошибок
    /// </summary>
    [MaxLength(50)]
    public string? NormId { get; set; }

    /// <summary>
    /// Номер нормы (альтернативное название для совместимости) - ВОССТАНОВЛЕНО
    /// </summary>
    [MaxLength(50)]
    public string? NormNumber { get; set; }

    #endregion

    #region Locomotive Info - ВОССТАНОВЛЕНО

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
    /// Тип локомотива - ИСПРАВЛЕНО: добавлено недостающее свойство из ошибок
    /// </summary>
    [MaxLength(50)]
    public string? LocomotiveType { get; set; }

    /// <summary>
    /// Депо приписки - ВОССТАНОВЛЕНО
    /// </summary>
    [MaxLength(100)]
    public string? Depot { get; set; }

    #endregion

    #region Load and Weight Data - ВОССТАНОВЛЕНО + ИСПРАВЛЕНО

    /// <summary>
    /// Масса нетто (тонны) - ВОССТАНОВЛЕНО
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal? NettoTons { get; set; }

    /// <summary>
    /// Масса брутто (тонны) - ВОССТАНОВЛЕНО
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal? BruttoTons { get; set; }

    /// <summary>
    /// Масса поезда - ВОССТАНОВЛЕНО из исходного
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal? TrainWeight { get; set; }

    /// <summary>
    /// Масса состава в тоннах - ИСПРАВЛЕНО: из новой версии для совместимости с новым кодом
    /// </summary>
    [Range(0.1, 50000)]
    public decimal TrainMass { get; set; }

    /// <summary>
    /// Количество осей - ВОССТАНОВЛЕНО
    /// </summary>
    public int? AxesCount { get; set; }

    /// <summary>
    /// Нагрузка на ось (тонны) - ВОССТАНОВЛЕНО + ИСПРАВЛЕНО
    /// </summary>
    [Column(TypeName = "decimal(8,3)")]
    [Range(0.1, 50)]
    public decimal AxleLoad { get; set; }

    #endregion

    #region Distance and Time - ВОССТАНОВЛЕНО + ИСПРАВЛЕНО

    /// <summary>
    /// Расстояние (км) - ВОССТАНОВЛЕНО
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    [Range(0.1, 10000)]
    public decimal Distance { get; set; }

    /// <summary>
    /// Время в пути - ИСПРАВЛЕНО: КРИТИЧЕСКИ ВАЖНОЕ свойство из ошибок компиляции
    /// </summary>
    public TimeSpan TravelTime { get; set; }

    /// <summary>
    /// Средняя скорость - ВОССТАНОВЛЕНО из исходного
    /// </summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal? AverageSpeed { get; set; }

    /// <summary>
    /// Тонно-километры - ВОССТАНОВЛЕНО
    /// </summary>
    [Column(TypeName = "decimal(12,3)")]
    public decimal? TonKilometers { get; set; }

    #endregion

    #region Energy Consumption - КРИТИЧЕСКОЕ ВОССТАНОВЛЕНИЕ

    /// <summary>
    /// КРИТИЧЕСКИ ВАЖНО: Расход электроэнергии в кВт*ч - ВОССТАНОВЛЕНО из исходного
    /// Это свойство было потеряно при первом исправлении!
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? ElectricConsumption { get; set; }

    /// <summary>
    /// КРИТИЧЕСКИ ВАЖНО: Механическая работа в кВт*ч - ВОССТАНОВЛЕНО из исходного
    /// Это свойство было потеряно при первом исправлении!
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? MechanicalWork { get; set; }

    /// <summary>
    /// Фактический расход электроэнергии (кВт*ч) - ВОССТАНОВЛЕНО (альтернативное название)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? FactConsumption { get; set; }

    /// <summary>
    /// Нормативный расход электроэнергии (кВт*ч) - ВОССТАНОВЛЕНО
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? NormConsumption { get; set; }

    /// <summary>
    /// Фактический расход - ИСПРАВЛЕНО: добавлено для совместимости с новым кодом
    /// </summary>
    public decimal? ActualConsumption { get; set; }

    /// <summary>
    /// Нормативный расход - ИСПРАВЛЕНО: добавлено для совместимости с новым кодом  
    /// </summary>
    public decimal? NormativeConsumption { get; set; }

    /// <summary>
    /// Фактический удельный расход (кВт*ч на 10000 ткм) - ВОССТАНОВЛЕНО
    /// </summary>
    [Column(TypeName = "decimal(8,4)")]
    public decimal? FactUd { get; set; }

    /// <summary>
    /// Нормативный удельный расход (кВт*ч на 10000 ткм) - ВОССТАНОВЛЕНО
    /// </summary>
    [Column(TypeName = "decimal(8,4)")]
    public decimal? NormUd { get; set; }

    /// <summary>
    /// КРИТИЧЕСКИ ВАЖНО: Удельный расход - ИСПРАВЛЕНО из ошибок компиляции
    /// Это свойство было потеряно при первом исправлении!
    /// </summary>
    [Column(TypeName = "decimal(8,4)")]
    public decimal? SpecificConsumption { get; set; }

    #endregion

    #region Analysis Results - ВОССТАНОВЛЕНО + ИСПРАВЛЕНО

    /// <summary>
    /// Отклонение от нормы - ИСПРАВЛЕНО: для совместимости с новым кодом
    /// </summary>
    public decimal? Deviation { get; set; }

    /// <summary>
    /// Отклонение в процентах - ВОССТАНОВЛЕНО
    /// </summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal? DeviationPercent { get; set; }

    /// <summary>
    /// Процентное отклонение - ИСПРАВЛЕНО: для совместимости с новым кодом
    /// </summary>
    public decimal? DeviationPercentage { get; set; }

    /// <summary>
    /// Статус отклонения - ВОССТАНОВЛЕНО + ИСПРАВЛЕНО
    /// </summary>
    public DeviationStatus Status { get; set; } = DeviationStatus.Unknown;

    /// <summary>
    /// Эффективность - ВОССТАНОВЛЕНО
    /// </summary>
    [Column(TypeName = "decimal(6,4)")]
    public decimal? Efficiency { get; set; }

    #endregion

    #region Sections - ИСПРАВЛЕНО из ошибок компиляции

    /// <summary>
    /// КРИТИЧЕСКИ ВАЖНО: Названия участков маршрута - из ошибок компиляции
    /// Это свойство было потеряно при первом исправлении!
    /// </summary>
    [JsonIgnore]
    public List<string> SectionNames { get; set; } = new();

    /// <summary>
    /// Названия участков как строка (для удобства отображения)
    /// </summary>
    public string SectionNamesString
    {
        get => string.Join(", ", SectionNames);
        set => SectionNames = string.IsNullOrEmpty(value)
            ? new List<string>()
            : value.Split(',').Select(s => s.Trim()).ToList();
    }

    #endregion

    #region Additional Data - ВОССТАНОВЛЕНО

    /// <summary>
    /// Погодные условия - ВОССТАНОВЛЕНО из исходного
    /// </summary>
    [MaxLength(100)]
    public string? WeatherConditions { get; set; }

    /// <summary>
    /// Комментарии - ВОССТАНОВЛЕНО из исходного
    /// </summary>
    [MaxLength(500)]
    public string? Comments { get; set; }

    /// <summary>
    /// Дата создания записи - ВОССТАНОВЛЕНО + ИСПРАВЛЕНО
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата последнего изменения - ВОССТАНОВЛЕНО + ИСПРАВЛЕНО
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Источник данных - ИСПРАВЛЕНО: из новой версии
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Дополнительные метаданные в формате JSON - ИСПРАВЛЕНО: из новой версии
    /// </summary>
    public string? Metadata { get; set; }

    #endregion

    #region Navigation Properties - ВОССТАНОВЛЕНО

    /// <summary>
    /// Связанная норма расхода - ВОССТАНОВЛЕНО
    /// </summary>
    [ForeignKey(nameof(NormId))]
    public virtual Norm? Norm { get; set; }

    #endregion

    #region Calculated Properties - ВОССТАНОВЛЕНО + ИСПРАВЛЕНО

    /// <summary>
    /// Экономия/перерасход в абсолютных единицах (кВт*ч) - ВОССТАНОВЛЕНО
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
    /// Признак экономии электроэнергии - ВОССТАНОВЛЕНО
    /// </summary>
    [NotMapped]
    public bool IsEconomy => DeviationPercent < 0;

    /// <summary>
    /// Признак перерасхода электроэнергии - ВОССТАНОВЛЕНО
    /// </summary>
    [NotMapped]
    public bool IsOverrun => DeviationPercent > 5;

    /// <summary>
    /// Полное название локомотива - ВОССТАНОВЛЕНО + ИСПРАВЛЕНО совместимость
    /// </summary>
    [NotMapped]
    [JsonIgnore]
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
    /// Полное имя локомотива - ИСПРАВЛЕНО: из новой версии для совместимости
    /// </summary>
    [JsonIgnore]
    public string LocomotiveFullName => LocomotiveNumber.HasValue
        ? $"{LocomotiveSeries}-{LocomotiveNumber}"
        : LocomotiveSeries ?? "Неизвестно";

    /// <summary>
    /// Уникальный ключ маршрута для группировки дубликатов - ВОССТАНОВЛЕНО
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

    /// <summary>
    /// Признак валидности данных маршрута - ИСПРАВЛЕНО: из новой версии
    /// </summary>
    [JsonIgnore]
    public bool IsValid => !string.IsNullOrEmpty(Name)
        && Distance > 0
        && TrainMass > 0
        && AxleLoad > 0;

    /// <summary>
    /// Эффективность маршрута - ИСПРАВЛЕНО: из новой версии (если есть данные)
    /// </summary>
    [JsonIgnore]
    public decimal? EfficiencyCalculated
    {
        get
        {
            if (MechanicalWork.HasValue && ElectricConsumption.HasValue
                && ElectricConsumption > 0)
            {
                return MechanicalWork / ElectricConsumption * 100;
            }
            return null;
        }
    }

    #endregion

    #region Override Methods - ВОССТАНОВЛЕНО + ИСПРАВЛЕНО

    /// <summary>
    /// ВОССТАНОВЛЕНО: исходный ToString с учетом новых свойств
    /// </summary>
    public override string ToString()
    {
        return !string.IsNullOrEmpty(Name)
            ? $"{Name} ({LocomotiveFullName}) - {Distance:F1}км"
            : $"Route {RouteNumber}: {FullLocomotiveName} on {Date:dd.MM.yyyy} - {DeviationPercent:F1}%";
    }

    /// <summary>
    /// ВОССТАНОВЛЕНО: исходный Equals с учетом новых свойств
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Route other) return false;

        // Если есть ID, используем его
        if (Id != 0 && other.Id != 0)
            return Id == other.Id;

        // Иначе используем RouteKey или Name
        return !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(other.Name)
            ? Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase)
            : RouteKey == other.RouteKey;
    }

    /// <summary>
    /// ВОССТАНОВЛЕНО: исходный GetHashCode с учетом новых свойств
    /// </summary>
    public override int GetHashCode()
    {
        if (Id != 0)
            return Id.GetHashCode();

        return !string.IsNullOrEmpty(Name)
            ? Name.GetHashCode(StringComparison.OrdinalIgnoreCase)
            : RouteKey.GetHashCode();
    }

    #endregion

    #region Factory Methods - ИСПРАВЛЕНО: из новой версии

    /// <summary>
    /// Создает новый маршрут с базовыми параметрами
    /// </summary>
    public static Route Create(string name, decimal distance, decimal trainMass, decimal axleLoad)
    {
        return new Route
        {
            Id = 0, // Будет назначен EF
            Name = name,
            Distance = distance,
            TrainMass = trainMass,
            AxleLoad = axleLoad,
            Date = DateTime.UtcNow,
            TripDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion
}