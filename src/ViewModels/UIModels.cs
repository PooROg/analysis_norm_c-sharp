// ViewModels/UIModels.cs
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.ViewModels;

/// <summary>
/// Сообщение для UI лога - отображается в интерфейсе
/// </summary>
public record LogMessage
{
    public string Source { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Color { get; init; } = "Black";
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public AlertLevel Level { get; init; } = AlertLevel.Info;
}

/// <summary>
/// Метрики производительности для отображения в UI
/// </summary>
public record PerformanceMetrics
{
    public decimal MemoryUsageMB { get; init; }
    public int ActiveOperations { get; init; }
    public int TotalOperations { get; init; }
    public decimal AverageOperationTime { get; init; }
    public DateTime LastUpdate { get; init; } = DateTime.UtcNow;
    public string Status { get; init; } = "OK";
    public Dictionary<string, decimal> DetailedMetrics { get; init; } = new();
}

/// <summary>
/// Расширенные метрики производительности для EnhancedMainViewModel
/// </summary>
public record EnhancedPerformanceMetrics : PerformanceMetrics
{
    public string SystemStatus { get; init; } = "OK";
    public int FilesProcessed { get; init; }
    public TimeSpan UptimeTotal { get; init; }
    public decimal CpuUsagePercent { get; init; }
    public long DiskUsageMB { get; init; }
}

/// <summary>
/// Диагностическая информация об обработке для UI
/// </summary>
public record ProcessingDiagnostics
{
    public int TotalFiles { get; init; }
    public int SuccessfulFiles { get; init; }
    public int FailedFiles { get; init; }
    public TimeSpan TotalTime { get; init; }
    public DateTime LastProcessingTime { get; init; }
    public List<string> RecentErrors { get; init; } = new();
    public Dictionary<string, int> ProcessingStats { get; init; } = new();
}

/// <summary>
/// Шаг обработки для отображения прогресса
/// </summary>
public record ProcessingStep
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = "Pending";
    public bool IsCompleted { get; init; }
    public bool IsActive { get; init; }
    public bool HasError { get; init; }
    public string Icon { get; init; } = "⏳";
    public string Description { get; init; } = string.Empty;
    public int ProgressPercent { get; init; }
}

/// <summary>
/// Диагностическое уведомление для UI
/// </summary>
public record DiagnosticAlert
{
    public AlertLevel Level { get; init; }
    public string Component { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public bool IsResolved { get; init; } = false;
    public string? RecommendedAction { get; init; }
}

/// <summary>
/// Результат автоматического тестирования
/// </summary>
public record TestResult
{
    public string TestName { get; init; } = string.Empty;
    public bool IsSuccess { get; init; }
    public string Result { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public DateTime ExecutionTime { get; init; } = DateTime.UtcNow;
    public List<string> Assertions { get; init; } = new();
}

/// <summary>
/// Информация о хранилище норм для UI
/// </summary>
public record StorageInfo
{
    public int TotalNorms { get; init; }
    public int CachedFunctions { get; init; }
    public long MemoryUsageBytes { get; init; }
    public DateTime LastUpdated { get; init; }
    public int ActiveConnections { get; init; }
    public bool IsHealthy { get; init; } = true;
}

/// <summary>
/// Пользовательские настройки для UI
/// </summary>
public record UserPreferences
{
    public string Theme { get; init; } = "Light";
    public string Language { get; init; } = "ru-RU";
    public WindowPosition MainWindowPosition { get; init; } = new();
    public List<string> RecentFiles { get; init; } = new();
    public Dictionary<string, object> CustomSettings { get; init; } = new();
    public DateTime LastModified { get; init; } = DateTime.UtcNow;
    public bool EnableNotifications { get; init; } = true;
    public bool AutoSaveEnabled { get; init; } = true;
}

/// <summary>
/// Позиция и размеры окна
/// </summary>
public record WindowPosition
{
    public int X { get; init; } = 100;
    public int Y { get; init; } = 100;
    public int Width { get; init; } = 1600;
    public int Height { get; init; } = 1000;
    public bool IsMaximized { get; init; } = false;
    public bool IsMinimized { get; init; } = false;
}