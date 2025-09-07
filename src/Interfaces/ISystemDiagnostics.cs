// Services/Interfaces/ISystemDiagnostics.cs
using AnalysisNorm.Models.Domain;
using AnalysisNorm.ViewModels;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Интерфейс диагностики и мониторинга системы для CHAT 4
/// Полностью новый интерфейс без конфликтов
/// </summary>
public interface ISystemDiagnostics
{
    /// <summary>
    /// Выполнение полной диагностики системы
    /// </summary>
    Task<SystemDiagnosticResult> RunFullDiagnosticsAsync();

    /// <summary>
    /// Быстрая проверка состояния системы
    /// </summary>
    Task<HealthCheckResult> QuickHealthCheckAsync();

    /// <summary>
    /// Мониторинг производительности в реальном времени
    /// </summary>
    Task StartPerformanceMonitoringAsync(TimeSpan interval);

    /// <summary>
    /// Остановка мониторинга
    /// </summary>
    void StopPerformanceMonitoring();

    /// <summary>
    /// Получение метрик производительности
    /// </summary>
    PerformanceMetrics GetCurrentMetrics();

    /// <summary>
    /// Создание отчета о состоянии системы
    /// </summary>
    Task<ProcessingResult<string>> GenerateSystemReportAsync();

    /// <summary>
    /// Получение истории диагностики
    /// </summary>
    Task<List<SystemDiagnosticResult>> GetDiagnosticHistoryAsync(TimeSpan period);

    /// <summary>
    /// Проверка отдельного компонента
    /// </summary>
    Task<ComponentDiagnostic> DiagnoseComponentAsync(string componentName);

    /// <summary>
    /// Получение алертов системы
    /// </summary>
    List<SystemAlert> GetActiveAlerts();

    /// <summary>
    /// Очистка алертов
    /// </summary>
    Task ClearAlertsAsync();
}

/// <summary>
/// Результат диагностики системы
/// </summary>
public record SystemDiagnosticResult
{
    public DateTime DiagnosticTime { get; init; } = DateTime.UtcNow;
    public SystemHealth OverallHealth { get; init; }
    public List<ComponentDiagnostic> ComponentDiagnostics { get; init; } = new();
    public PerformanceMetrics PerformanceMetrics { get; init; } = new();
    public List<SystemAlert> Alerts { get; init; } = new();
    public string Summary { get; init; } = string.Empty;
    public TimeSpan DiagnosticDuration { get; init; }
    public Dictionary<string, object> DetailedMetrics { get; init; } = new();
}

/// <summary>
/// Диагностика компонента
/// </summary>
public record ComponentDiagnostic
{
    public string ComponentName { get; init; } = string.Empty;
    public SystemHealth Health { get; init; }
    public string Status { get; init; } = string.Empty;
    public Dictionary<string, object> Metrics { get; init; } = new();
    public List<string> Issues { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
    public DateTime LastCheck { get; init; } = DateTime.UtcNow;
    public TimeSpan ResponseTime { get; init; }
}

/// <summary>
/// Системное предупреждение
/// </summary>
public record SystemAlert
{
    public AlertLevel Level { get; init; }
    public string Component { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? RecommendedAction { get; init; }
    public bool IsResolved { get; init; } = false;
    public string? Resolution { get; init; }
    public int OccurrenceCount { get; init; } = 1;
}

/// <summary>
/// Результат проверки состояния
/// </summary>
public record HealthCheckResult
{
    public bool IsHealthy { get; init; }
    public string Status { get; init; } = string.Empty;
    public TimeSpan ResponseTime { get; init; }
    public Dictionary<string, bool> ComponentStatus { get; init; } = new();
    public List<string> FailedComponents { get; init; } = new();
    public DateTime CheckTime { get; init; } = DateTime.UtcNow;
    public string? ErrorMessage { get; init; }
}