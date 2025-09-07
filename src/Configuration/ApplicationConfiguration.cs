// ===================================================================
// ФАЙЛ: src/Configuration/ApplicationConfiguration.cs
// Базовые конфигурационные классы для устранения ошибок компиляции
// ===================================================================

namespace AnalysisNorm.Configuration;

/// <summary>
/// Основная конфигурация приложения
/// </summary>
public class ApplicationConfiguration
{
    /// <summary>
    /// Название приложения
    /// </summary>
    public string ApplicationName { get; set; } = "AnalysisNorm";
    
    /// <summary>
    /// Версия приложения
    /// </summary>
    public string Version { get; set; } = "1.3.4.0";
    
    /// <summary>
    /// Рабочая директория
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// Максимальный размер файла для обработки (в байтах)
    /// </summary>
    public long MaxFileSize { get; set; } = 100_000_000; // 100MB
    
    /// <summary>
    /// Таймаут операций в секундах
    /// </summary>
    public int OperationTimeoutSeconds { get; set; } = 300; // 5 минут
    
    /// <summary>
    /// Включить детальное логирование
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
    
    /// <summary>
    /// Автоматическое резервное копирование
    /// </summary>
    public bool EnableAutoBackup { get; set; } = true;
}

/// <summary>
/// Конфигурация производительности
/// </summary>
public class PerformanceConfiguration
{
    /// <summary>
    /// Размер кэша в мегабайтах
    /// </summary>
    public int CacheSizeMB { get; set; } = 256;
    
    /// <summary>
    /// Максимальное количество параллельных потоков
    /// </summary>
    public int MaxParallelThreads { get; set; } = Environment.ProcessorCount;
    
    /// <summary>
    /// Размер буфера для чтения файлов (в байтах)
    /// </summary>
    public int FileBufferSize { get; set; } = 8192; // 8KB
    
    /// <summary>
    /// Интервал очистки кэша в минутах
    /// </summary>
    public int CacheCleanupIntervalMinutes { get; set; } = 30;
    
    /// <summary>
    /// Включить мониторинг производительности
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;
    
    /// <summary>
    /// Максимальное использование памяти в мегабайтах
    /// </summary>
    public int MaxMemoryUsageMB { get; set; } = 1024; // 1GB
    
    /// <summary>
    /// Включить сборку мусора после операций
    /// </summary>
    public bool EnableGCAfterOperations { get; set; } = false;
    
    /// <summary>
    /// Пороговое значение для предупреждения о производительности (в миллисекундах)
    /// </summary>
    public int PerformanceWarningThresholdMs { get; set; } = 5000; // 5 секунд
}

/// <summary>
/// Конфигурация пользовательского интерфейса
/// </summary>
public class UIConfiguration
{
    /// <summary>
    /// Тема приложения (Light/Dark)
    /// </summary>
    public string Theme { get; set; } = "Light";
    
    /// <summary>
    /// Основной цвет приложения
    /// </summary>
    public string PrimaryColor { get; set; } = "DeepPurple";
    
    /// <summary>
    /// Вторичный цвет приложения
    /// </summary>
    public string SecondaryColor { get; set; } = "Lime";
    
    /// <summary>
    /// Размер шрифта по умолчанию
    /// </summary>
    public double DefaultFontSize { get; set; } = 14.0;
    
    /// <summary>
    /// Семейство шрифтов
    /// </summary>
    public string FontFamily { get; set; } = "Segoe UI";
    
    /// <summary>
    /// Показывать всплывающие подсказки
    /// </summary>
    public bool ShowTooltips { get; set; } = true;
    
    /// <summary>
    /// Автоматически сохранять состояние окна
    /// </summary>
    public bool SaveWindowState { get; set; } = true;
    
    /// <summary>
    /// Включить анимации
    /// </summary>
    public bool EnableAnimations { get; set; } = true;
    
    /// <summary>
    /// Показывать индикатор прогресса для длительных операций
    /// </summary>
    public bool ShowProgressIndicator { get; set; } = true;
    
    /// <summary>
    /// Язык интерфейса
    /// </summary>
    public string Language { get; set; } = "ru-RU";
}

/// <summary>
/// Конфигурация HTML парсинга
/// </summary>
public class HtmlParsingConfiguration
{
    /// <summary>
    /// Кодировка по умолчанию для HTML файлов
    /// </summary>
    public string DefaultEncoding { get; set; } = "UTF-8";
    
    /// <summary>
    /// Таймаут парсинга в секундах
    /// </summary>
    public int ParsingTimeoutSeconds { get; set; } = 60;
    
    /// <summary>
    /// Игнорировать CSS стили при парсинге
    /// </summary>
    public bool IgnoreStyles { get; set; } = true;
    
    /// <summary>
    /// Игнорировать JavaScript при парсинге
    /// </summary>
    public bool IgnoreScripts { get; set; } = true;
    
    /// <summary>
    /// Валидировать HTML структуру
    /// </summary>
    public bool ValidateHtmlStructure { get; set; } = false;
    
    /// <summary>
    /// Максимальная глубина вложенности элементов
    /// </summary>
    public int MaxNestingLevel { get; set; } = 100;
}

/// <summary>
/// Конфигурация Excel экспорта
/// </summary>
public class ExcelExportConfiguration
{
    /// <summary>
    /// Формат файла по умолчанию (xlsx/xls)
    /// </summary>
    public string DefaultFormat { get; set; } = "xlsx";
    
    /// <summary>
    /// Включить автоматическое изменение размера колонок
    /// </summary>
    public bool AutoSizeColumns { get; set; } = true;
    
    /// <summary>
    /// Включить фильтры в заголовках
    /// </summary>
    public bool EnableFilters { get; set; } = true;
    
    /// <summary>
    /// Применять стили оформления
    /// </summary>
    public bool ApplyStyles { get; set; } = true;
    
    /// <summary>
    /// Максимальное количество строк на листе
    /// </summary>
    public int MaxRowsPerSheet { get; set; } = 1_000_000;
    
    /// <summary>
    /// Включить защиту листа
    /// </summary>
    public bool EnableSheetProtection { get; set; } = false;
    
    /// <summary>
    /// Пароль для защиты (если EnableSheetProtection = true)
    /// </summary>
    public string? ProtectionPassword { get; set; }
    
    /// <summary>
    /// Шаблон имени файла
    /// </summary>
    public string FileNameTemplate { get; set; } = "AnalysisNorm_Export_{0:yyyy-MM-dd_HH-mm-ss}";
}

/// <summary>
/// Конфигурация диагностики системы
/// </summary>
public class DiagnosticsConfiguration
{
    /// <summary>
    /// Включить системную диагностику
    /// </summary>
    public bool EnableSystemDiagnostics { get; set; } = true;
    
    /// <summary>
    /// Интервал проверки системы в минутах
    /// </summary>
    public int SystemCheckIntervalMinutes { get; set; } = 15;
    
    /// <summary>
    /// Сохранять отчеты диагностики
    /// </summary>
    public bool SaveDiagnosticReports { get; set; } = true;
    
    /// <summary>
    /// Максимальное количество отчетов для хранения
    /// </summary>
    public int MaxDiagnosticReports { get; set; } = 50;
    
    /// <summary>
    /// Пороговое значение использования памяти для предупреждения (в %)
    /// </summary>
    public double MemoryWarningThresholdPercent { get; set; } = 80.0;
    
    /// <summary>
    /// Пороговое значение использования CPU для предупреждения (в %)
    /// </summary>
    public double CpuWarningThresholdPercent { get; set; } = 80.0;
    
    /// <summary>
    /// Минимальное свободное место на диске в мегабайтах
    /// </summary>
    public long MinFreeDiskSpaceMB { get; set; } = 1024; // 1GB
    
    /// <summary>
    /// Включить автоматическую очистку временных файлов
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;
}