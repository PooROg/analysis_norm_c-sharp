// === AnalysisNorm.Core/Entities/ProcessingStatistics.cs ===
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Статистика обработки данных
/// Соответствует Python statistics tracking из analysis/norm_processor.py
/// Высокопроизводительная структура с минимальным memory footprint
/// </summary>
public sealed class ProcessingStatistics
{
    #region Core Counters - Thread-Safe Fields

    // ИСПРАВЛЕНО: Используем поля для thread-safe операций с Interlocked
    private int _totalProcessed;
    private int _successfullyProcessed;
    private int _errorCount;
    private int _skippedCount;
    private int _warningCount;
    private long _bytesProcessed;

    /// <summary>
    /// Общее количество обработанных элементов
    /// </summary>
    public int TotalProcessed
    {
        get => _totalProcessed;
        set => _totalProcessed = value;
    }

    /// <summary>
    /// Количество успешно обработанных элементов
    /// </summary>
    public int SuccessfullyProcessed
    {
        get => _successfullyProcessed;
        set => _successfullyProcessed = value;
    }

    /// <summary>
    /// Количество элементов с ошибками
    /// </summary>
    public int ErrorCount
    {
        get => _errorCount;
        set => _errorCount = value;
    }

    /// <summary>
    /// Количество пропущенных элементов
    /// </summary>
    public int SkippedCount
    {
        get => _skippedCount;
        set => _skippedCount = value;
    }

    #endregion

    #region Performance Metrics

    /// <summary>
    /// Время начала обработки (UTC)
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время завершения обработки (UTC)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Общее время выполнения
    /// </summary>
    [JsonIgnore]
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

    /// <summary>
    /// Скорость обработки (элементов в секунду)
    /// </summary>
    [JsonIgnore]
    public double ProcessingRate
    {
        get
        {
            var seconds = Duration.TotalSeconds;
            return seconds > 0 ? TotalProcessed / seconds : 0;
        }
    }

    #endregion

    #region File Processing Specifics

    /// <summary>
    /// Количество обработанных файлов
    /// </summary>
    public int FilesProcessed { get; set; }

    /// <summary>
    /// Количество файлов с ошибками
    /// </summary>
    public int ErrorFiles { get; set; }

    /// <summary>
    /// Количество успешно обработанных файлов
    /// </summary>
    public int ProcessedFiles { get; set; }

    /// <summary>
    /// Общее количество файлов для обработки
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Размер обработанных данных в байтах
    /// </summary>
    public long BytesProcessed
    {
        get => _bytesProcessed;
        set => _bytesProcessed = value;
    }

    /// <summary>
    /// Количество найденных норм/маршрутов
    /// </summary>
    public int ItemsFound { get; set; }

    /// <summary>
    /// Количество нормализованных записей
    /// </summary>
    public int ItemsNormalized { get; set; }

    /// <summary>
    /// Общее количество маршрутов
    /// </summary>
    public int TotalRoutes { get; set; }

    /// <summary>
    /// Количество обработанных маршрутов
    /// </summary>
    public int ProcessedRoutes { get; set; }

    /// <summary>
    /// Количество дублированных маршрутов
    /// </summary>
    public int DuplicateRoutes { get; set; }

    /// <summary>
    /// Время обработки
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Список ошибок обработки
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Список предупреждений
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    #endregion

    #region Data Quality Metrics - Thread-Safe Fields

    /// <summary>
    /// Количество записей с предупреждениями
    /// </summary>
    public int WarningCount
    {
        get => _warningCount;
        set => _warningCount = value;
    }

    /// <summary>
    /// Количество дублированных записей
    /// </summary>
    public int DuplicateCount { get; set; }

    /// <summary>
    /// Количество некорректных значений
    /// </summary>
    public int InvalidValueCount { get; set; }

    /// <summary>
    /// Количество записей с отсутствующими данными
    /// </summary>
    public int MissingDataCount { get; set; }

    #endregion

    #region Convenience Properties

    /// <summary>
    /// Процент успешности обработки
    /// </summary>
    [JsonIgnore]
    public double SuccessPercentage
    {
        get
        {
            return TotalProcessed > 0
                ? (double)SuccessfullyProcessed / TotalProcessed * 100
                : 0;
        }
    }

    /// <summary>
    /// Есть ли ошибки в процессе обработки
    /// </summary>
    [JsonIgnore]
    public bool HasErrors => ErrorCount > 0;

    /// <summary>
    /// Есть ли предупреждения
    /// </summary>
    [JsonIgnore]
    public bool HasWarnings => WarningCount > 0;

    /// <summary>
    /// Обработка завершена успешно (без критических ошибок)
    /// </summary>
    [JsonIgnore]
    public bool IsSuccessful => ErrorCount == 0 && TotalProcessed > 0;

    #endregion

    #region Factory Methods

    /// <summary>
    /// Создает новую статистику с началом отсчета времени
    /// </summary>
    /// <returns>Новый экземпляр ProcessingStatistics</returns>
    public static ProcessingStatistics StartNew()
    {
        return new ProcessingStatistics
        {
            StartTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Завершает подсчет статистики
    /// </summary>
    public void Finish()
    {
        EndTime = DateTime.UtcNow;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Увеличивает счетчик успешно обработанных элементов
    /// Атомарная операция для thread-safety
    /// </summary>
    public void IncrementSuccess()
    {
        Interlocked.Increment(ref _successfullyProcessed);
        Interlocked.Increment(ref _totalProcessed);
    }

    /// <summary>
    /// Увеличивает счетчик ошибок
    /// Атомарная операция для thread-safety
    /// </summary>
    public void IncrementError()
    {
        Interlocked.Increment(ref _errorCount);
        Interlocked.Increment(ref _totalProcessed);
    }

    /// <summary>
    /// Увеличивает счетчик пропущенных элементов
    /// Атомарная операция для thread-safety
    /// </summary>
    public void IncrementSkipped()
    {
        Interlocked.Increment(ref _skippedCount);
        Interlocked.Increment(ref _totalProcessed);
    }

    /// <summary>
    /// Увеличивает счетчик предупреждений
    /// Атомарная операция для thread-safety
    /// </summary>
    public void IncrementWarning()
    {
        Interlocked.Increment(ref _warningCount);
    }

    /// <summary>
    /// Добавляет обработанные байты к общему счетчику
    /// Атомарная операция для thread-safety
    /// </summary>
    public void AddBytesProcessed(long bytes)
    {
        Interlocked.Add(ref _bytesProcessed, bytes);
    }

    /// <summary>
    /// Объединяет статистику с другим экземпляром
    /// Полезно для агрегации результатов параллельной обработки
    /// </summary>
    public void Merge(ProcessingStatistics other)
    {
        if (other == null) return;

        TotalProcessed += other.TotalProcessed;
        SuccessfullyProcessed += other.SuccessfullyProcessed;
        ErrorCount += other.ErrorCount;
        SkippedCount += other.SkippedCount;
        WarningCount += other.WarningCount;
        DuplicateCount += other.DuplicateCount;
        InvalidValueCount += other.InvalidValueCount;
        MissingDataCount += other.MissingDataCount;
        FilesProcessed += other.FilesProcessed;
        BytesProcessed += other.BytesProcessed;
        ItemsFound += other.ItemsFound;
        ItemsNormalized += other.ItemsNormalized;

        // Выбираем наиболее раннее время начала
        if (other.StartTime < StartTime)
            StartTime = other.StartTime;

        // Выбираем наиболее позднее время окончания
        if (other.EndTime.HasValue && (!EndTime.HasValue || other.EndTime > EndTime))
            EndTime = other.EndTime;
    }

    #endregion

    #region ToString for Debugging

    /// <summary>
    /// Возвращает детальную информацию о статистике для отладки
    /// </summary>
    public override string ToString()
    {
        return $"ProcessingStatistics: {SuccessfullyProcessed}/{TotalProcessed} успешно " +
               $"({SuccessPercentage:F1}%), {ErrorCount} ошибок, " +
               $"{Duration.TotalSeconds:F1}с, {ProcessingRate:F1} эл/сек";
    }

    #endregion
}