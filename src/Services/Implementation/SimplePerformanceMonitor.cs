using System.Collections.Concurrent;
using System.Diagnostics;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Простая и эффективная реализация мониторинга производительности
/// Минимальные накладные расходы с максимальной информативностью
/// </summary>
public class SimplePerformanceMonitor : IPerformanceMonitor, IDisposable
{
    private readonly ConcurrentDictionary<string, Stopwatch> _activeOperations = new();
    private readonly ConcurrentQueue<OperationMetric> _operationHistory = new();
    private readonly IApplicationLogger _logger;
    private readonly Process _currentProcess;
    private readonly object _lockObject = new();
    
    private int _totalOperations;
    private decimal _totalOperationTime;
    private long _peakMemoryUsage;
    private bool _disposed;

    /// <summary>
    /// Конструктор с dependency injection
    /// </summary>
    public SimplePerformanceMonitor(IApplicationLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentProcess = Process.GetCurrentProcess();
        _peakMemoryUsage = _currentProcess.WorkingSet64;
        
        _logger.LogInformation("SimplePerformanceMonitor инициализирован");
    }

    /// <summary>
    /// Начать отслеживание операции с автоматическим завершением
    /// </summary>
    public IDisposable StartOperation(string operationName)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);
        
        // Используем уникальный ключ для поддержки вложенных операций
        var operationKey = $"{operationName}_{Thread.CurrentThread.ManagedThreadId}_{stopwatch.GetHashCode()}";
        
        if (!_activeOperations.TryAdd(operationKey, stopwatch))
        {
            _logger.LogWarning("Операция {OperationName} уже активна", operationName);
        }

        _logger.LogDebug("Начата операция: {OperationName}", operationName);

        return new OperationTracker(operationKey, operationName, memoryBefore, this);
    }

    /// <summary>
    /// Завершить операцию вручную
    /// </summary>
    public void EndOperation(string operationName)
    {
        // Ищем операцию по имени (для обратной совместимости)
        var operationKey = _activeOperations.Keys
            .FirstOrDefault(k => k.StartsWith($"{operationName}_"));

        if (operationKey != null)
        {
            EndOperationInternal(operationKey, operationName);
        }
        else
        {
            _logger.LogWarning("Операция {OperationName} не найдена среди активных", operationName);
        }
    }

    /// <summary>
    /// Внутренний метод завершения операции
    /// </summary>
    internal void EndOperationInternal(string operationKey, string operationName)
    {
        if (_activeOperations.TryRemove(operationKey, out var stopwatch))
        {
            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);
            var currentMemory = _currentProcess.WorkingSet64;
            
            // Обновляем пиковое использование памяти
            if (currentMemory > _peakMemoryUsage)
            {
                _peakMemoryUsage = currentMemory;
            }

            var metric = new OperationMetric
            {
                Name = operationName,
                Duration = stopwatch.Elapsed,
                MemoryBefore = 0, // Будет установлено в OperationTracker
                MemoryAfter = memoryAfter,
                StartTime = DateTime.Now - stopwatch.Elapsed,
                EndTime = DateTime.Now
            };

            // Добавляем в историю с ограничением размера
            _operationHistory.Enqueue(metric);
            TrimOperationHistory();

            // Обновляем общую статистику
            lock (_lockObject)
            {
                _totalOperations++;
                _totalOperationTime += (decimal)stopwatch.Elapsed.TotalMilliseconds;
            }

            _logger.LogDebug("Завершена операция: {OperationName}, время: {Duration}мс", 
                operationName, stopwatch.Elapsed.TotalMilliseconds);

            // Предупреждение о долгих операциях
            if (stopwatch.Elapsed.TotalSeconds > 5)
            {
                _logger.LogWarning("Долгая операция: {OperationName} выполнялась {Duration}сек", 
                    operationName, stopwatch.Elapsed.TotalSeconds);
            }
        }
    }

    /// <summary>
    /// Логирование использования памяти
    /// </summary>
    public void LogMemoryUsage(string? context = null)
    {
        try
        {
            _currentProcess.Refresh();
            var workingSet = _currentProcess.WorkingSet64;
            var privateMemory = _currentProcess.PrivateMemorySize64;
            var gcMemory = GC.GetTotalMemory(false);

            var contextInfo = string.IsNullOrWhiteSpace(context) ? "" : $" [{context}]";
            
            _logger.LogInformation("Использование памяти{Context}: WorkingSet={WorkingSetMB:F1}MB, " +
                                 "Private={PrivateMB:F1}MB, GC={GcMB:F1}MB",
                contextInfo,
                workingSet / 1024.0 / 1024.0,
                privateMemory / 1024.0 / 1024.0,
                gcMemory / 1024.0 / 1024.0);

            // Обновляем пиковое значение
            if (workingSet > _peakMemoryUsage)
            {
                _peakMemoryUsage = workingSet;
            }

            // Предупреждение при высоком использовании памяти
            var memoryMB = workingSet / 1024.0 / 1024.0;
            if (memoryMB > 150) // 150MB warning threshold
            {
                _logger.LogWarning("Высокое использование памяти: {MemoryMB:F1}MB", memoryMB);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при логировании использования памяти");
        }
    }

    /// <summary>
    /// Получить текущие метрики производительности
    /// </summary>
    public PerformanceMetrics GetCurrentMetrics()
    {
        try
        {
            _currentProcess.Refresh();
            var currentMemoryMB = (decimal)_currentProcess.WorkingSet64 / 1024 / 1024;
            var peakMemoryMB = (decimal)_peakMemoryUsage / 1024 / 1024;

            decimal averageOperationTime;
            lock (_lockObject)
            {
                averageOperationTime = _totalOperations > 0 
                    ? _totalOperationTime / _totalOperations 
                    : 0;
            }

            var lastOperation = _operationHistory.LastOrDefault();
            var lastOperationTime = lastOperation?.Duration.TotalMilliseconds ?? 0;

            return new PerformanceMetrics
            {
                MemoryUsageMB = currentMemoryMB,
                PeakMemoryUsageMB = peakMemoryMB,
                ActiveOperations = _activeOperations.Count,
                TotalOperations = _totalOperations,
                AverageOperationTime = averageOperationTime,
                LastOperationTime = lastOperationTime,
                TotalCodeLines = 8500, // Примерная оценка
                UpTime = DateTime.Now - _currentProcess.StartTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения метрик производительности");
            return new PerformanceMetrics(); // Возвращаем пустую структуру при ошибке
        }
    }

    /// <summary>
    /// Получить историю операций
    /// </summary>
    public IEnumerable<OperationMetric> GetOperationHistory(int maxItems = 100)
    {
        var items = _operationHistory.ToArray();
        return items.Reverse().Take(maxItems);
    }

    /// <summary>
    /// Сбросить счетчики и очистить историю
    /// </summary>
    public void Reset()
    {
        lock (_lockObject)
        {
            _totalOperations = 0;
            _totalOperationTime = 0;
        }

        // Очищаем историю операций
        while (_operationHistory.TryDequeue(out _)) { }

        // Сбрасываем пиковую память
        _peakMemoryUsage = _currentProcess.WorkingSet64;

        _logger.LogInformation("Счетчики производительности сброшены");
    }

    /// <summary>
    /// Оценка количества строк кода (приблизительная)
    /// </summary>
    private static int EstimateCodeLines()
    {
        // Для CHAT 1: приблизительная оценка
        // В будущих версиях можно добавить подсчет реальных строк
        return 2500;
    }

    /// <summary>
    /// Ограничение размера истории операций для экономии памяти
    /// </summary>
    private void TrimOperationHistory()
    {
        const int maxHistorySize = 500;
        
        while (_operationHistory.Count > maxHistorySize)
        {
            _operationHistory.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Освобождение ресурсов
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _activeOperations.Clear();
            while (_operationHistory.TryDequeue(out _)) { }
            _currentProcess?.Dispose();
            _disposed = true;
            
            _logger.LogInformation("SimplePerformanceMonitor освобожден");
        }
    }
}

/// <summary>
/// Tracker для автоматического завершения операций с помощью using
/// </summary>
internal class OperationTracker : IDisposable
{
    private readonly string _operationKey;
    private readonly string _operationName;
    private readonly long _memoryBefore;
    private readonly SimplePerformanceMonitor _monitor;
    private bool _disposed;

    public OperationTracker(string operationKey, string operationName, long memoryBefore, SimplePerformanceMonitor monitor)
    {
        _operationKey = operationKey;
        _operationName = operationName;
        _memoryBefore = memoryBefore;
        _monitor = monitor;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _monitor.EndOperationInternal(_operationKey, _operationName);
            _disposed = true;
        }
    }
}