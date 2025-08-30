using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Точка нормы - пара (нагрузка, расход)
/// Полное соответствие Python norm point structure
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

/// <summary>
/// Настройки приложения с недостающими свойствами
/// </summary>
public class ApplicationSettings
{
    /// <summary>
    /// Время истечения кэша в часах
    /// </summary>
    public int CacheExpirationHours { get; set; } = 24;

    /// <summary>
    /// Время истечения кэша в днях
    /// </summary>
    public int CacheExpirationDays { get; set; } = 7;

    /// <summary>
    /// Максимальный размер кэша в мегабайтах
    /// </summary>
    public int MaxCacheSizeMb { get; set; } = 1024;

    /// <summary>
    /// Строка подключения к базе данных
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Уровень логирования
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Временная папка для файлов
    /// </summary>
    public string TempDirectory { get; set; } = Path.GetTempPath();

    /// <summary>
    /// Максимальный размер загружаемых файлов в мегабайтах
    /// </summary>
    public int MaxFileSizeMb { get; set; } = 100;

    /// <summary>
    /// Таймаут для операций в секундах
    /// </summary>
    public int OperationTimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// Опции экспорта (исправленный класс без дубликатов)
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Включить графики в экспорт
    /// </summary>
    public bool IncludeCharts { get; set; } = true;

    /// <summary>
    /// Включить детальную статистику
    /// </summary>
    public bool IncludeDetailedStatistics { get; set; } = true;

    /// <summary>
    /// Формат дат для экспорта
    /// </summary>
    public string DateFormat { get; set; } = "dd.MM.yyyy";

    /// <summary>
    /// Включить сводную информацию
    /// </summary>
    public bool IncludeSummary { get; set; } = true;

    /// <summary>
    /// Показывать только обработанные данные
    /// </summary>
    public bool ProcessedDataOnly { get; set; } = false;
}

/// <summary>
/// Результат обработки (обобщенный класс)
/// </summary>
public class ProcessingResult<T>
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Данные результата
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Статистика обработки
    /// </summary>
    public ProcessingStatistics? Statistics { get; set; }

    /// <summary>
    /// Создает успешный результат
    /// </summary>
    public static ProcessingResult<T> Success(T data, ProcessingStatistics? statistics = null)
    {
        return new ProcessingResult<T>
        {
            IsSuccess = true,
            Data = data,
            Statistics = statistics
        };
    }

    /// <summary>
    /// Создает результат с ошибкой
    /// </summary>
    public static ProcessingResult<T> Failure(string errorMessage, ProcessingStatistics? statistics = null)
    {
        return new ProcessingResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Statistics = statistics
        };
    }
}