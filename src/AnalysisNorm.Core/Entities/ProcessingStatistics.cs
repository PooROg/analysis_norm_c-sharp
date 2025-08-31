using System.Text.Json.Serialization;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// ВОССТАНОВЛЕНО: Полная объединенная версия ProcessingStatistics
/// Включает все свойства из Core и Services слоев без дублирования
/// </summary>
public class ProcessingStatistics
{
    #region Thread-Safe Fields

    private volatile int _totalProcessed;
    private volatile int _successfullyProcessed;
    private volatile int _errorCount;
    private volatile int _skippedCount;
    private volatile int _warningCount;
    private volatile long _bytesProcessed;

    #endregion

    #region Core Processing Properties

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
    /// Количество ошибок
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

    /// <summary>
    /// Количество предупреждений
    /// </summary>
    public int WarningCount
    {
        get => _warningCount;
        set => _warningCount = value;
    }

    #endregion

    #region Timing Properties

    /// <summary>
    /// Время начала обработки
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время окончания обработки
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Общее время выполнения
    /// </summary>
    [JsonIgnore]
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

    /// <summary>
    /// Время обработки - для совместимости с Services слоем
    /// </summary>
    [JsonIgnore]
    public TimeSpan ProcessingTime => Duration;

    /// <summary>
    /// ВОССТАНОВЛЕНО: Время обработки в миллисекундах (из Services слоя)
    /// </summary>
    public long ProcessingTimeMs
    {
        get => (long)Duration.TotalMilliseconds;
        set { /* Только для чтения, вычисляется из Duration */ }
    }

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

    #region File Processing Properties

    /// <summary>
    /// ВОССТАНОВЛЕНО: Общее количество файлов для обработки (из Services слоя)
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// ВОССТАНОВЛЕНО: Количество успешно обработанных файлов (из Services слоя)
    /// </summary>
    public int ProcessedFiles { get; set; }

    /// <summary>
    /// ВОССТАНОВЛЕНО: Количество файлов с ошибками (из Services слоя)
    /// </summary>
    public int ErrorFiles { get; set; }

    /// <summary>
    /// ВОССТАНОВЛЕНО: Количество пропущенных файлов (из Services слоя)
    /// </summary>
    public int SkippedFiles { get; set; }

    /// <summary>
    /// Размер обработанных данных в байтах
    /// </summary>
    public long BytesProcessed
    {
        get => _bytesProcessed;
        set => _bytesProcessed = value;
    }

    #endregion

    #region Route Processing Properties - ВОССТАНОВЛЕНО из Services слоя

    /// <summary>
    /// ВОССТАНОВЛЕНО: Общее количество маршрутов (из Services слоя)
    /// КРИТИЧЕСКИ ВАЖНО: это свойство использовалось в ошибках компиляции
    /// </summary>
    public int TotalRoutes { get; set; }

    /// <summary>
    /// ВОССТАНОВЛЕНО: Количество обработанных маршрутов (из Services слоя)
    /// КРИТИЧЕСКИ ВАЖНО: это свойство использовалось в ошибках компиляции
    /// </summary>
    public int ProcessedRoutes { get; set; }

    /// <summary>
    /// ВОССТАНОВЛЕНО: Количество дублированных маршрутов (из Services слоя)
    /// КРИТИЧЕСКИ ВАЖНО: это свойство использовалось в ошибках компиляции
    /// </summary>
    public int DuplicateRoutes { get; set; }

    #endregion

    #region Data Quality Metrics

    /// <summary>
    /// Количество найденных элементов (норм/маршрутов)
    /// </summary>
    public int ItemsFound { get; set; }

    /// <summary>
    /// Количество нормализованных записей
    /// </summary>
    public int ItemsNormalized { get; set; }

    /// <summary>
    /// Количество дублированных записей (общий счетчик)
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

    #region Error and Warning Collections

    /// <summary>
    /// ВОССТАНОВЛЕНО: Список ошибок обработки (из Services слоя)
    /// КРИТИЧЕСКИ ВАЖНО: это свойство использовалось в ошибках компиляции
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// ВОССТАНОВЛЕНО: Список предупреждений (из Services слоя)
    /// КРИТИЧЕСКИ ВАЖНО: это свойство использовалось в ошибках компиляции
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    #endregion

    #region Computed Properties

    /// <summary>
    /// Процент успешности обработки
    /// </summary>
    [JsonIgnore]
    public double SuccessPercentage => TotalProcessed > 0
        ? (double)SuccessfullyProcessed / TotalProcessed * 100
        : 0;

    /// <summary>
    /// ВОССТАНОВЛЕНО: Коэффициент успешности (из Services слоя)
    /// </summary>
    [JsonIgnore]
    public double SuccessRate => TotalProcessed > 0
        ? (double)SuccessfullyProcessed / TotalProcessed
        : 0;

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

    /// <summary>
    /// Обработка завершена (есть время окончания)
    /// </summary>
    [JsonIgnore]
    public bool IsCompleted => EndTime.HasValue;

    #endregion

    #region Thread-Safe Operations

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

    #endregion

    #region Utility Methods

    /// <summary>
    /// Добавляет ошибку в список
    /// Потокобезопасная операция
    /// </summary>
    public void AddError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            lock (Errors)
            {
                if (!Errors.Contains(error))
                {
                    Errors.Add(error);
                    IncrementError();
                }
            }
        }
    }

    /// <summary>
    /// Добавляет предупреждение в список
    /// Потокобезопасная операция
    /// </summary>
    public void AddWarning(string warning)
    {
        if (!string.IsNullOrWhiteSpace(warning))
        {
            lock (Warnings)
            {
                if (!Warnings.Contains(warning))
                {
                    Warnings.Add(warning);
                    IncrementWarning();
                }
            }
        }
    }

    /// <summary>
    /// Объединяет статистику с другим экземпляром
    /// Полезно для агрегации результатов параллельной обработки
    /// </summary>
    public void Merge(ProcessingStatistics other)
    {
        if (other == null) return;

        // Суммируем основные счетчики
        TotalProcessed += other.TotalProcessed;
        SuccessfullyProcessed += other.SuccessfullyProcessed;
        ErrorCount += other.ErrorCount;
        SkippedCount += other.SkippedCount;
        WarningCount += other.WarningCount;
        DuplicateCount += other.DuplicateCount;
        InvalidValueCount += other.InvalidValueCount;
        MissingDataCount += other.MissingDataCount;

        // Файлы и данные
        TotalFiles += other.TotalFiles;
        ProcessedFiles += other.ProcessedFiles;
        ErrorFiles += other.ErrorFiles;
        SkippedFiles += other.SkippedFiles;
        BytesProcessed += other.BytesProcessed;
        ItemsFound += other.ItemsFound;
        ItemsNormalized += other.ItemsNormalized;

        // ВОССТАНОВЛЕНО: Маршруты (из Services слоя)
        TotalRoutes += other.TotalRoutes;
        ProcessedRoutes += other.ProcessedRoutes;
        DuplicateRoutes += other.DuplicateRoutes;

        // Время - выбираем границы
        if (other.StartTime < StartTime)
            StartTime = other.StartTime;

        if (other.EndTime.HasValue && (!EndTime.HasValue || other.EndTime > EndTime))
            EndTime = other.EndTime;

        // Объединяем списки ошибок и предупреждений
        lock (Errors)
        {
            foreach (var error in other.Errors.Where(e => !Errors.Contains(e)))
                Errors.Add(error);
        }

        lock (Warnings)
        {
            foreach (var warning in other.Warnings.Where(w => !Warnings.Contains(w)))
                Warnings.Add(warning);
        }
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Создает новую статистику с началом отсчета времени
    /// </summary>
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

    #region Overrides

    /// <summary>
    /// Возвращает детальную информацию о статистике для отладки
    /// </summary>
    public override string ToString()
    {
        return $"ProcessingStatistics: {SuccessfullyProcessed}/{TotalProcessed} успешно " +
               $"({SuccessPercentage:F1}%), {ErrorCount} ошибок, " +
               $"{Duration.TotalSeconds:F1}с, {ProcessingRate:F1} эл/сек";
    }

    public override bool Equals(object? obj)
    {
        return obj is ProcessingStatistics other &&
               TotalProcessed == other.TotalProcessed &&
               SuccessfullyProcessed == other.SuccessfullyProcessed &&
               ErrorCount == other.ErrorCount &&
               StartTime == other.StartTime;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TotalProcessed, SuccessfullyProcessed, ErrorCount, StartTime);
    }

    #endregion

    #region Report Generation

    /// <summary>
    /// Генерирует подробный отчет о статистике
    /// </summary>
    public string GenerateDetailedReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== СТАТИСТИКА ОБРАБОТКИ ===");
        sb.AppendLine($"Период: {StartTime:yyyy-MM-dd HH:mm:ss} - {(EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "В процессе")}");
        sb.AppendLine($"Длительность: {Duration.TotalMinutes:F2} минут");
        sb.AppendLine();

        sb.AppendLine("--- ОБЩИЕ ПОКАЗАТЕЛИ ---");
        sb.AppendLine($"Обработано элементов: {TotalProcessed}");
        sb.AppendLine($"Успешно: {SuccessfullyProcessed} ({SuccessPercentage:F1}%)");
        sb.AppendLine($"Ошибки: {ErrorCount}");
        sb.AppendLine($"Пропущено: {SkippedCount}");
        sb.AppendLine($"Предупреждения: {WarningCount}");
        sb.AppendLine($"Скорость: {ProcessingRate:F1} элементов/сек");
        sb.AppendLine();

        if (TotalFiles > 0)
        {
            sb.AppendLine("--- ФАЙЛЫ ---");
            sb.AppendLine($"Всего файлов: {TotalFiles}");
            sb.AppendLine($"Обработано: {ProcessedFiles}");
            sb.AppendLine($"С ошибками: {ErrorFiles}");
            sb.AppendLine($"Пропущено: {SkippedFiles}");
            sb.AppendLine($"Размер данных: {BytesProcessed / 1024.0 / 1024.0:F2} МБ");
            sb.AppendLine();
        }

        if (TotalRoutes > 0)
        {
            sb.AppendLine("--- МАРШРУТЫ ---");
            sb.AppendLine($"Всего маршрутов: {TotalRoutes}");
            sb.AppendLine($"Обработано: {ProcessedRoutes}");
            sb.AppendLine($"Дубликатов: {DuplicateRoutes}");
            sb.AppendLine();
        }

        if (HasErrors)
        {
            sb.AppendLine("--- ОШИБКИ ---");
            foreach (var error in Errors.Take(10))
                sb.AppendLine($"• {error}");
            if (Errors.Count > 10)
                sb.AppendLine($"... и еще {Errors.Count - 10} ошибок");
        }

        return sb.ToString();
    }

    #endregion
}