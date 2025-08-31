using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Результат анализа маршрутов с нормами потребления
/// </summary>
public class AnalysisResult
{
    #region Basic Properties

    /// <summary>
    /// Уникальный идентификатор результата анализа
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Дата проведения анализа - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Название анализа
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Описание анализа
    /// </summary>
    public string? Description { get; set; }

    #endregion

    #region Collections - ИСПРАВЛЕНО

    /// <summary>
    /// Маршруты, участвующие в анализе
    /// </summary>
    public List<Route> Routes { get; set; } = new();

    /// <summary>
    /// Использованные нормы - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    public List<Norm> Norms { get; set; } = new();

    /// <summary>
    /// Функции норм для интерполяции - ИСПРАВЛЕНО: добавлено недостающее свойство
    /// </summary>
    [JsonIgnore] // Сложные объекты исключаем из JSON сериализации
    public Dictionary<string, InterpolationFunction> NormFunctions { get; set; } = new();

    #endregion

    #region Analysis Statistics

    /// <summary>
    /// Общая статистика анализа
    /// </summary>
    public AnalysisStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Статистика по статусам отклонений
    /// </summary>
    public Dictionary<DeviationStatus, int> DeviationCounts { get; set; } = new();

    /// <summary>
    /// Общее количество маршрутов
    /// </summary>
    [JsonIgnore]
    public int TotalRoutes => Routes.Count;

    /// <summary>
    /// Количество маршрутов с отклонениями
    /// </summary>
    [JsonIgnore]
    public int RoutesWithDeviations => Routes.Count(r => r.Status != DeviationStatus.Normal && r.Status != DeviationStatus.Unknown);

    #endregion

    #region Analysis Results Summary

    /// <summary>
    /// Средний процент отклонения
    /// </summary>
    public decimal? AverageDeviationPercentage { get; set; }

    /// <summary>
    /// Медианное отклонение
    /// </summary>
    public decimal? MedianDeviation { get; set; }

    /// <summary>
    /// Общая экономия (отрицательное значение = экономия)
    /// </summary>
    public decimal? TotalSavings { get; set; }

    /// <summary>
    /// Общий перерасход (положительное значение = перерасход)
    /// </summary>
    public decimal? TotalOverrun { get; set; }

    /// <summary>
    /// Процент маршрутов в норме
    /// </summary>
    [JsonIgnore]
    public decimal NormalRoutesPercentage => TotalRoutes > 0
        ? (decimal)Routes.Count(r => r.Status == DeviationStatus.Normal) / TotalRoutes * 100
        : 0;

    #endregion

    #region Processing Information

    /// <summary>
    /// Время начала анализа
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время завершения анализа
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Длительность анализа
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => EndTime?.Subtract(StartTime);

    /// <summary>
    /// Версия алгоритма анализа
    /// </summary>
    public string AlgorithmVersion { get; set; } = "1.0";

    /// <summary>
    /// Параметры анализа
    /// </summary>
    public AnalysisParameters Parameters { get; set; } = new();

    #endregion

    #region Data Quality

    /// <summary>
    /// Количество ошибок во время анализа
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Количество предупреждений
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Список ошибок
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Список предупреждений
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Качество данных (от 0 до 100)
    /// </summary>
    [Range(0, 100)]
    public int DataQualityScore { get; set; } = 100;

    #endregion

    #region Export and Reporting

    /// <summary>
    /// Путь к экспортированному файлу (если есть)
    /// </summary>
    public string? ExportPath { get; set; }

    /// <summary>
    /// Формат экспорта
    /// </summary>
    public string? ExportFormat { get; set; }

    /// <summary>
    /// Дата последнего экспорта
    /// </summary>
    public DateTime? LastExportDate { get; set; }

    #endregion

    #region Metadata

    /// <summary>
    /// Пользователь, проводивший анализ
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Дополнительные метаданные
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Теги для категоризации результатов
    /// </summary>
    public List<string> Tags { get; set; } = new();

    #endregion

    #region Computed Properties

    /// <summary>
    /// Статус анализа
    /// </summary>
    [JsonIgnore]
    public string Status => EndTime.HasValue ? "Завершен" : "В процессе";

    /// <summary>
    /// Признак успешности анализа
    /// </summary>
    [JsonIgnore]
    public bool IsSuccessful => ErrorCount == 0 && EndTime.HasValue;

    /// <summary>
    /// Общий коэффициент эффективности
    /// </summary>
    [JsonIgnore]
    public decimal? OverallEfficiency
    {
        get
        {
            var routesWithEfficiency = Routes.Where(r => r.Efficiency.HasValue).ToList();
            return routesWithEfficiency.Count > 0
                ? routesWithEfficiency.Average(r => r.Efficiency!.Value)
                : null;
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Добавляет маршрут к анализу
    /// </summary>
    public void AddRoute(Route route)
    {
        if (route != null && !Routes.Any(r => r.Id == route.Id))
        {
            Routes.Add(route);
            UpdateStatistics();
        }
    }

    /// <summary>
    /// Добавляет несколько маршрутов
    /// </summary>
    public void AddRoutes(IEnumerable<Route> routes)
    {
        foreach (var route in routes)
        {
            AddRoute(route);
        }
    }

    /// <summary>
    /// Обновляет статистику анализа
    /// </summary>
    public void UpdateStatistics()
    {
        // Пересчет статистики по статусам отклонений
        DeviationCounts = Routes
            .GroupBy(r => r.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        // Расчет средних значений
        var routesWithDeviations = Routes.Where(r => r.DeviationPercentage.HasValue).ToList();
        if (routesWithDeviations.Count > 0)
        {
            AverageDeviationPercentage = routesWithDeviations.Average(r => r.DeviationPercentage!.Value);

            var sortedDeviations = routesWithDeviations.Select(r => r.DeviationPercentage!.Value).OrderBy(d => d).ToList();
            MedianDeviation = sortedDeviations.Count % 2 == 0
                ? (sortedDeviations[sortedDeviations.Count / 2 - 1] + sortedDeviations[sortedDeviations.Count / 2]) / 2
                : sortedDeviations[sortedDeviations.Count / 2];
        }

        // Расчет общих экономий и перерасходов
        var routesWithActualConsumption = Routes.Where(r => r.ActualConsumption.HasValue && r.NormativeConsumption.HasValue).ToList();
        if (routesWithActualConsumption.Count > 0)
        {
            TotalSavings = routesWithActualConsumption
                .Where(r => r.ActualConsumption < r.NormativeConsumption)
                .Sum(r => r.NormativeConsumption - r.ActualConsumption);

            TotalOverrun = routesWithActualConsumption
                .Where(r => r.ActualConsumption > r.NormativeConsumption)
                .Sum(r => r.ActualConsumption - r.NormativeConsumption);
        }
    }

    /// <summary>
    /// Завершает анализ
    /// </summary>
    public void Complete()
    {
        EndTime = DateTime.UtcNow;
        UpdateStatistics();
    }

    /// <summary>
    /// Добавляет ошибку
    /// </summary>
    public void AddError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error) && !Errors.Contains(error))
        {
            Errors.Add(error);
            ErrorCount = Errors.Count;
        }
    }

    /// <summary>
    /// Добавляет предупреждение
    /// </summary>
    public void AddWarning(string warning)
    {
        if (!string.IsNullOrWhiteSpace(warning) && !Warnings.Contains(warning))
        {
            Warnings.Add(warning);
            WarningCount = Warnings.Count;
        }
    }

    #endregion

    #region Object Overrides

    public override string ToString()
    {
        var statusText = IsSuccessful ? "Успешно" : "С ошибками";
        return $"{Name} ({AnalysisDate:yyyy-MM-dd}) - {TotalRoutes} маршрутов - {statusText}";
    }

    public override bool Equals(object? obj)
    {
        return obj is AnalysisResult other && Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Создает новый результат анализа
    /// </summary>
    public static AnalysisResult Create(string name, string? description = null)
    {
        return new AnalysisResult
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            AnalysisDate = DateTime.UtcNow,
            StartTime = DateTime.UtcNow
        };
    }

    #endregion
}

/// <summary>
/// Статистика анализа
/// </summary>
public class AnalysisStatistics
{
    /// <summary>
    /// Количество обработанных маршрутов
    /// </summary>
    public int ProcessedRoutes { get; set; }

    /// <summary>
    /// Количество успешно проанализированных маршрутов
    /// </summary>
    public int SuccessfulRoutes { get; set; }

    /// <summary>
    /// Количество маршрутов с ошибками
    /// </summary>
    public int FailedRoutes { get; set; }

    /// <summary>
    /// Скорость обработки (маршрутов в минуту)
    /// </summary>
    public double ProcessingRate { get; set; }

    /// <summary>
    /// Общее время обработки
    /// </summary>
    public TimeSpan TotalTime { get; set; }
}

/// <summary>
/// Параметры анализа
/// </summary>
public class AnalysisParameters
{
    /// <summary>
    /// Использовать интерполяцию для недостающих значений норм
    /// </summary>
    public bool UseInterpolation { get; set; } = true;

    /// <summary>
    /// Минимальное количество точек для интерполяции
    /// </summary>
    public int MinInterpolationPoints { get; set; } = 2;

    /// <summary>
    /// Максимально допустимое отклонение для считания нормой (%)
    /// </summary>
    public decimal MaxNormalDeviation { get; set; } = 2m;

    /// <summary>
    /// Игнорировать маршруты с неполными данными
    /// </summary>
    public bool IgnoreIncompleteRoutes { get; set; } = false;

    /// <summary>
    /// Использовать кэширование вычислений
    /// </summary>
    public bool UseCaching { get; set; } = true;

    /// <summary>
    /// Дополнительные пользовательские параметры
    /// </summary>
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}