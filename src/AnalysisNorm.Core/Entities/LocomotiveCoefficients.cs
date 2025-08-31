using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Коэффициент локомотива для расчета нормативного расхода
/// </summary>
public class LocomotiveCoefficient
{
    #region Primary Properties

    /// <summary>
    /// Уникальный идентификатор коэффициента
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Серия локомотива
    /// </summary>
    [Required]
    public string LocomotiveSeries { get; set; } = string.Empty;

    /// <summary>
    /// Номер локомотива (может быть null для общих коэффициентов серии)
    /// </summary>
    public int? LocomotiveNumber { get; set; }

    #endregion

    #region Coefficient Data - ИСПРАВЛЕНО

    /// <summary>
    /// Значение коэффициента - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    [Required]
    [Range(0.1, 10.0, ErrorMessage = "Коэффициент должен быть в диапазоне от 0.1 до 10.0")]
    public decimal Value { get; set; } = 1.0m;

    /// <summary>
    /// Базовый коэффициент расхода
    /// </summary>
    [Range(0.1, 5.0)]
    public decimal BaseCoefficient { get; set; } = 1.0m;

    /// <summary>
    /// Коэффициент для тяги
    /// </summary>
    [Range(0.5, 2.0)]
    public decimal TractionCoefficient { get; set; } = 1.0m;

    /// <summary>
    /// Коэффициент для рекуперации (если применимо)
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal RegenerationCoefficient { get; set; } = 0.0m;

    #endregion

    #region Technical Specifications

    /// <summary>
    /// Тип локомотива (электровоз, тепловоз, электропоезд)
    /// </summary>
    public LocomotiveType Type { get; set; } = LocomotiveType.Electric;

    /// <summary>
    /// Мощность локомотива в кВт
    /// </summary>
    [Range(100, 20000)]
    public decimal? Power { get; set; }

    /// <summary>
    /// Масса локомотива в тоннах
    /// </summary>
    [Range(20, 200)]
    public decimal? Mass { get; set; }

    /// <summary>
    /// Максимальная скорость в км/ч
    /// </summary>
    [Range(20, 300)]
    public decimal? MaxSpeed { get; set; }

    #endregion

    #region Validity and Application

    /// <summary>
    /// Дата начала действия коэффициента
    /// </summary>
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата окончания действия коэффициента
    /// </summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>
    /// Признак активности коэффициента
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Условия применения коэффициента
    /// </summary>
    public string? ApplicationConditions { get; set; }

    #endregion

    #region Quality and Source Information

    /// <summary>
    /// Источник данных коэффициента
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Уровень достоверности (от 1 до 5)
    /// </summary>
    [Range(1, 5)]
    public int ReliabilityLevel { get; set; } = 3;

    /// <summary>
    /// Количество использований коэффициента
    /// </summary>
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// Последнее использование
    /// </summary>
    public DateTime? LastUsed { get; set; }

    #endregion

    #region Metadata

    /// <summary>
    /// Дата создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата последнего обновления
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Пользователь, создавший запись
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Комментарии к коэффициенту
    /// </summary>
    public string? Comments { get; set; }

    /// <summary>
    /// Дополнительные метаданные в формате JSON
    /// </summary>
    public string? Metadata { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// Полное наименование локомотива
    /// </summary>
    [JsonIgnore]
    public string FullName => LocomotiveNumber.HasValue
        ? $"{LocomotiveSeries}-{LocomotiveNumber}"
        : $"{LocomotiveSeries} (серия)";

    /// <summary>
    /// Признак валидности коэффициента
    /// </summary>
    [JsonIgnore]
    public bool IsValid
    {
        get
        {
            var now = DateTime.UtcNow;
            return IsActive &&
                   ValidFrom <= now &&
                   (ValidTo == null || ValidTo >= now) &&
                   Value > 0;
        }
    }

    /// <summary>
    /// Эффективный коэффициент с учетом всех составляющих
    /// </summary>
    [JsonIgnore]
    public decimal EffectiveCoefficient
    {
        get
        {
            var effective = Value * BaseCoefficient * TractionCoefficient;

            // Учитываем рекуперацию для снижения расхода
            if (Type == LocomotiveType.Electric && RegenerationCoefficient > 0)
            {
                effective *= (1 - RegenerationCoefficient * 0.1m); // 10% экономии на каждые 0.1 коэф. рекуперации
            }

            return effective;
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Проверяет применимость коэффициента для конкретного маршрута
    /// </summary>
    public bool IsApplicableToRoute(Route route)
    {
        if (!IsValid) return false;

        // Проверяем соответствие серии и номера локомотива
        if (route.LocomotiveSeries != LocomotiveSeries) return false;

        if (LocomotiveNumber.HasValue && route.LocomotiveNumber != LocomotiveNumber) return false;

        return true;
    }

    /// <summary>
    /// Вычисляет нормативный расход для маршрута
    /// </summary>
    public decimal CalculateNormativeConsumption(Route route, decimal baseNorm)
    {
        if (!IsApplicableToRoute(route))
            throw new InvalidOperationException("Коэффициент не применим к данному маршруту");

        // Базовый расчет с учетом эффективного коэффициента
        var normative = baseNorm * EffectiveCoefficient;

        // Корректировка по массе состава
        if (route.TrainMass > 0)
        {
            var massCoefficient = 1 + (route.TrainMass - 1000) / 10000; // Увеличение на 1% на каждые 100 тонн свыше 1000
            normative *= Math.Max(0.5m, Math.Min(2.0m, massCoefficient)); // Ограничиваем коэффициент
        }

        return Math.Max(0, normative);
    }

    /// <summary>
    /// Отмечает использование коэффициента
    /// </summary>
    public void MarkAsUsed()
    {
        UsageCount++;
        LastUsed = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Object Overrides

    public override string ToString()
    {
        return $"{FullName}: {Value:F3} ({(IsValid ? "активен" : "неактивен")})";
    }

    public override bool Equals(object? obj)
    {
        return obj is LocomotiveCoefficient other && Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Создает коэффициент для серии локомотивов
    /// </summary>
    public static LocomotiveCoefficient CreateForSeries(string series, decimal value, LocomotiveType type = LocomotiveType.Electric)
    {
        return new LocomotiveCoefficient
        {
            Id = Guid.NewGuid().ToString(),
            LocomotiveSeries = series,
            Value = value,
            Type = type,
            ValidFrom = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Создает коэффициент для конкретного локомотива
    /// </summary>
    public static LocomotiveCoefficient CreateForLocomotive(string series, int number, decimal value, LocomotiveType type = LocomotiveType.Electric)
    {
        return new LocomotiveCoefficient
        {
            Id = Guid.NewGuid().ToString(),
            LocomotiveSeries = series,
            LocomotiveNumber = number,
            Value = value,
            Type = type,
            ValidFrom = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion
}

/// <summary>
/// Тип локомотива
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LocomotiveType
{
    /// <summary>
    /// Электровоз
    /// </summary>
    Electric = 0,

    /// <summary>
    /// Тепловоз
    /// </summary>
    Diesel = 1,

    /// <summary>
    /// Электропоезд
    /// </summary>
    ElectricTrain = 2,

    /// <summary>
    /// Дизельпоезд
    /// </summary>
    DieselTrain = 3,

    /// <summary>
    /// Гибридный
    /// </summary>
    Hybrid = 4,

    /// <summary>
    /// Неизвестный тип
    /// </summary>
    Unknown = 99
}

/// <summary>
/// Расширения для LocomotiveType
/// </summary>
public static class LocomotiveTypeExtensions
{
    /// <summary>
    /// Получает описание типа локомотива
    /// </summary>
    public static string GetDescription(this LocomotiveType type)
    {
        return type switch
        {
            LocomotiveType.Electric => "Электровоз",
            LocomotiveType.Diesel => "Тепловоз",
            LocomotiveType.ElectricTrain => "Электропоезд",
            LocomotiveType.DieselTrain => "Дизельпоезд",
            LocomotiveType.Hybrid => "Гибридный",
            _ => "Неизвестный тип"
        };
    }

    /// <summary>
    /// Проверяет использует ли тип электрическую тягу
    /// </summary>
    public static bool IsElectric(this LocomotiveType type)
    {
        return type is LocomotiveType.Electric or LocomotiveType.ElectricTrain or LocomotiveType.Hybrid;
    }

    /// <summary>
    /// Проверяет поддерживает ли тип рекуперацию
    /// </summary>
    public static bool SupportsRegeneration(this LocomotiveType type)
    {
        return IsElectric(type);
    }
}