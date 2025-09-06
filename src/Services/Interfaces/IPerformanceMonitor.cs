namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Интерфейс для мониторинга производительности приложения
/// Отслеживает время операций, использование памяти и общую производительность
/// </summary>
public interface IPerformanceMonitor
{
    /// <summary>
    /// Начать отслеживание операции (возвращает IDisposable для автоматического завершения)
    /// </summary>
    IDisposable StartOperation(string operationName);

    /// <summary>
    /// Завершить отслеживание операции вручную
    /// </summary>
    void EndOperation(string operationName);

    /// <summary>
    /// Залогировать текущее использование памяти
    /// </summary>
    void LogMemoryUsage(string? context = null);

    /// <summary>
    /// Получить текущие метрики производительности
    /// </summary>
    PerformanceMetrics GetCurrentMetrics();

    /// <summary>
    /// Получить историю операций
    /// </summary>
    IEnumerable<OperationMetric> GetOperationHistory(int maxItems = 100);

    /// <summary>
    /// Сбросить счетчики и очистить историю
    /// </summary>
    void Reset();
}

/// <summary>
/// Метрики производительности приложения
/// </summary>
public record PerformanceMetrics
{
    /// <summary>
    /// Текущее использование памяти в мегабайтах
    /// </summary>
    public decimal MemoryUsageMB { get; init; }

    /// <summary>
    /// Пиковое использование памяти в мегабайтах
    /// </summary>
    public decimal PeakMemoryUsageMB { get; init; }

    /// <summary>
    /// Количество активных операций
    /// </summary>
    public int ActiveOperations { get; init; }

    /// <summary>
    /// Общее количество выполненных операций
    /// </summary>
    public int TotalOperations { get; init; }

    /// <summary>
    /// Среднее время выполнения операций в миллисекундах
    /// </summary>
    public decimal AverageOperationTime { get; init; }

    /// <summary>
    /// Время последней операции в миллисекундах
    /// </summary>
    public decimal LastOperationTime { get; init; }

    /// <summary>
    /// Приблизительное количество строк кода в проекте
    /// </summary>
    public int TotalCodeLines { get; init; } = 2500; // Текущая оценка для CHAT 1

    /// <summary>
    /// Количество норм в хранилище
    /// </summary>
    public int TotalNorms { get; init; }

    /// <summary>
    /// Время запуска приложения
    /// </summary>
    public DateTime ApplicationStartTime { get; init; } = DateTime.Now;
}

/// <summary>
/// Метрика отдельной операции
/// </summary>
public record OperationMetric
{
    /// <summary>
    /// Название операции
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Время выполнения
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Использование памяти до операции
    /// </summary>
    public long MemoryBefore { get; init; }

    /// <summary>
    /// Использование памяти после операции
    /// </summary>
    public long MemoryAfter { get; init; }

    /// <summary>
    /// Время начала операции
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Время завершения операции
    /// </summary>
    public DateTime EndTime { get; init; }

    /// <summary>
    /// Изменение использования памяти
    /// </summary>
    public long MemoryDelta => MemoryAfter - MemoryBefore;
}