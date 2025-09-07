// Models/Domain/Enums.cs
namespace AnalysisNorm.Models.Domain;

/// <summary>
/// Статус отклонения для классификации норм
/// Используется StatusClassifier для определения критичности отклонений
/// </summary>
public enum DeviationStatus
{
    Unknown = 0,
    Excellent = 1,      // Отклонение <= 5%
    Good = 2,           // Отклонение <= 10%
    Acceptable = 3,     // Отклонение <= 20%
    Poor = 4,           // Отклонение <= 30%
    Critical = 5        // Отклонение > 30%
}

/// <summary>
/// Общее состояние здоровья системы для диагностики
/// </summary>
public enum SystemHealth
{
    Excellent = 1,      // Все компоненты работают идеально
    Good = 2,           // Мелкие проблемы, не влияющие на работу
    Fair = 3,           // Заметные проблемы, требующие внимания
    Poor = 4,           // Серьезные проблемы, влияющие на производительность
    Critical = 5        // Критические проблемы, система может не работать
}

/// <summary>
/// Уровень важности алертов и уведомлений
/// </summary>
public enum AlertLevel
{
    Info = 1,           // Информационные сообщения
    Warning = 2,        // Предупреждения
    Error = 3,          // Ошибки
    Critical = 4        // Критические ошибки
}

/// <summary>
/// Тип операции для мониторинга производительности
/// </summary>
public enum OperationType
{
    Unknown = 0,
    FileLoading = 1,
    HtmlParsing = 2,
    DataAnalysis = 3,
    ExcelExport = 4,
    ConfigurationUpdate = 5,
    SystemDiagnostics = 6
}

/// <summary>
/// Режим работы приложения
/// </summary>
public enum ApplicationMode
{
    Development = 1,
    Testing = 2,
    Production = 3
}