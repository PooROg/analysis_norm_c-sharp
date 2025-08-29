// === AnalysisNorm.Core/Entities/DeviationStatus.cs ===
using System.ComponentModel.DataAnnotations;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Единый enum для статусов отклонений (замена всех дублирующихся DeviationStatus)
/// Соответствует StatusClassifier из Python utils.py
/// </summary>
public enum DeviationStatus
{
    [Display(Name = "Экономия сильная")]
    EconomyStrong,
    
    [Display(Name = "Экономия средняя")]
    EconomyMedium,
    
    [Display(Name = "Экономия слабая")]
    EconomyWeak,
    
    [Display(Name = "Норма")]
    Normal,
    
    [Display(Name = "Перерасход слабый")]
    OverrunWeak,
    
    [Display(Name = "Перерасход средний")]
    OverrunMedium,
    
    [Display(Name = "Перерасход сильный")]
    OverrunStrong
}

/// <summary>
/// Расширения для работы со статусами отклонений
/// Замещает все статические классы DeviationStatus
/// </summary>
public static class DeviationStatusExtensions
{
    // Пороги отклонений (из Python StatusClassifier)
    private const decimal StrongEconomyThreshold = -30m;
    private const decimal MediumEconomyThreshold = -20m;
    private const decimal WeakEconomyThreshold = -5m;
    private const decimal NormalUpperThreshold = 5m;
    private const decimal WeakOverrunThreshold = 20m;
    private const decimal MediumOverrunThreshold = 30m;

    /// <summary>
    /// Определяет статус по отклонению в процентах
    /// Соответствует get_status из Python StatusClassifier
    /// </summary>
    public static DeviationStatus GetStatus(decimal deviationPercent)
    {
        return deviationPercent switch
        {
            <= StrongEconomyThreshold => DeviationStatus.EconomyStrong,
            <= MediumEconomyThreshold => DeviationStatus.EconomyMedium,
            <= WeakEconomyThreshold => DeviationStatus.EconomyWeak,
            <= NormalUpperThreshold => DeviationStatus.Normal,
            <= WeakOverrunThreshold => DeviationStatus.OverrunWeak,
            <= MediumOverrunThreshold => DeviationStatus.OverrunMedium,
            _ => DeviationStatus.OverrunStrong
        };
    }

    /// <summary>
    /// Получает отображаемое имя статуса
    /// </summary>
    public static string GetDisplayName(this DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.EconomyStrong => "Экономия сильная",
            DeviationStatus.EconomyMedium => "Экономия средняя",
            DeviationStatus.EconomyWeak => "Экономия слабая",
            DeviationStatus.Normal => "Норма",
            DeviationStatus.OverrunWeak => "Перерасход слабый",
            DeviationStatus.OverrunMedium => "Перерасход средний",
            DeviationStatus.OverrunStrong => "Перерасход сильный",
            _ => "Неизвестно"
        };
    }

    /// <summary>
    /// Возвращает цвет для статуса
    /// Соответствует get_status_color из Python StatusClassifier
    /// </summary>
    public static string GetStatusColor(this DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.EconomyStrong => "darkgreen",
            DeviationStatus.EconomyMedium => "green", 
            DeviationStatus.EconomyWeak => "lightgreen",
            DeviationStatus.Normal => "blue",
            DeviationStatus.OverrunWeak => "orange",
            DeviationStatus.OverrunMedium => "darkorange", 
            DeviationStatus.OverrunStrong => "red",
            _ => "gray"
        };
    }
}

// === AnalysisNorm.Core/Entities/Route.cs (исправленная версия) ===
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Сущность маршрута - исправленная версия без проблемных switch patterns
/// Соответствует структуре данных из Python analysis
/// </summary>
[Table("Routes")]
public class Route
{
    [Key]
    public int Id { get; set; }

    // === ОСНОВНЫЕ ИДЕНТИФИКАЦИОННЫЕ ПОЛЯ ===
    [StringLength(50)]
    public string? RouteNumber { get; set; }

    [StringLength(10)] 
    public string? RouteDate { get; set; }

    [StringLength(10)]
    public string? TripDate { get; set; }

    [StringLength(20)]
    public string? DriverTab { get; set; }

    [StringLength(100)]
    public string? SectionName { get; set; }

    // === ХАРАКТЕРИСТИКИ ПОЕЗДА ===
    [StringLength(20)]
    public string? LocomotiveSeries { get; set; }

    [StringLength(20)] 
    public string? LocomotiveNumber { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? BruttoTons { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? NettoTons { get; set; }

    public int? AxesCount { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TonKilometers { get; set; }

    // === РАСХОДЫ ЭЛЕКТРОЭНЕРГИИ ===
    [Column(TypeName = "decimal(18,3)")]
    public decimal? FactConsumption { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? NormConsumption { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? ElectricConsumption { get; set; }

    // === ПРОИЗВОДНЫЕ ПАРАМЕТРЫ ===
    [Column(TypeName = "decimal(18,6)")]
    public decimal? MechanicalWork { get; set; }

    [Column(TypeName = "decimal(18,6)")]
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
    [Column(TypeName = "decimal(18,3)")]
    public decimal? IdleBrigadaTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? IdleBrigadaNorm { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? ManevryTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? ManevryNorm { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? StartTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? StartNorm { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? DelayTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? DelayNorm { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? SpeedLimitTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? SpeedLimitNorm { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TransferLocoTotal { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TransferLocoNorm { get; set; }

    // === МЕТАДАННЫЕ ОБРАБОТКИ ===
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

    // ИСПРАВЛЕНО: использование enum вместо string для статуса
    public DeviationStatus Status { get; set; } = DeviationStatus.Normal;

    // Флаги для улучшенной визуализации
    public bool UseRedColor { get; set; }
    
    public bool UseRedRashod { get; set; }

    // === КОЭФФИЦИЕНТЫ ===
    [Column(TypeName = "decimal(18,6)")]
    public decimal? Coefficient { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? FactUdOriginal { get; set; }

    // === СИСТЕМНЫЕ ПОЛЯ ===
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Хэш для идентификации дубликатов
    /// </summary>
    [StringLength(200)]
    public string? RouteKey { get; set; }

    // === НАВИГАЦИОННЫЕ СВОЙСТВА ===
    public virtual ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();

    // === СПИСКИ УЧАСТКОВ ===
    [StringLength(1000)]
    public string? SectionNames { get; set; } // JSON массив участков

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
    /// ИСПРАВЛЕНО: Обновляет статус на основе отклонения
    /// Убирает проблемный switch pattern, используя статический метод
    /// </summary>
    public void UpdateStatus()
    {
        if (DeviationPercent.HasValue)
        {
            Status = DeviationStatusExtensions.GetStatus(DeviationPercent.Value);
            
            // Устанавливаем флаги визуализации
            UseRedColor = Status == DeviationStatus.OverrunMedium || Status == DeviationStatus.OverrunStrong;
            UseRedRashod = DeviationPercent > 15.0m;
        }
    }
}

// === AnalysisNorm.Core/Entities/ApplicationSettings.cs (обновленная) ===
namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Настройки приложения - единый источник конфигурации
/// </summary>
public class ApplicationSettings
{
    // === DIRECTORIES ===
    public string DataDirectory { get; set; } = "Data";
    public string TempDirectory { get; set; } = "Temp";
    public string ExportsDirectory { get; set; } = "Exports";
    public string LogsDirectory { get; set; } = "Logs";

    // === FILE PROCESSING ===
    public string[] SupportedEncodings { get; set; } = { "UTF-8", "Windows-1251", "CP866" };
    public int MaxTempFiles { get; set; } = 1000;
    public double DefaultTolerancePercent { get; set; } = 5.0;
    public double MinWorkThreshold { get; set; } = 0.1;
    public int HtmlProcessingTimeoutSeconds { get; set; } = 300;
    public int MaxConcurrentFiles { get; set; } = Environment.ProcessorCount;
    public bool EnableHtmlValidation { get; set; } = true;
    public bool PreserveOriginalHtml { get; set; } = false;

    // === CACHE SETTINGS ===
    public int CacheExpirationHours { get; set; } = 24;
    public int CacheExpirationDays { get; set; } = 7;

    // === UI SETTINGS ===
    public double DefaultWindowWidth { get; set; } = 1200;
    public double DefaultWindowHeight { get; set; } = 800;

    // === LOGGING SETTINGS ===
    public LoggingSettings Logging { get; set; } = new();
    
    // === PERFORMANCE SETTINGS ===
    public PerformanceSettings Performance { get; set; } = new();
}

public class LoggingSettings
{
    public string MinimumLevel { get; set; } = "Information";
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;
    public bool EnableDetailedErrors { get; set; } = true;
    public int RetainedFileCountLimit { get; set; } = 30;
}

public class PerformanceSettings
{
    public bool EnableInterpolationCaching { get; set; } = true;
    public int CacheExpirationDays { get; set; } = 7;
    public int MaxConcurrentProcessing { get; set; } = Environment.ProcessorCount;
    public int DatabasePoolSize { get; set; } = 100;
    public bool EnableAsyncProcessing { get; set; } = true;
    public int MaxMemoryUsageMB { get; set; } = 2048;
}