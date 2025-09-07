// Services/Interfaces/IInteractiveNormsAnalyzer.cs (ИСПРАВЛЕН)
using AnalysisNorm.Models.Domain;
using AnalysisNorm.Services.Implementation;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// ИСПРАВЛЕННЫЙ интерфейс главного анализатора норм
/// Объединяет функциональность CHAT 2 + CHAT 3-4
/// Совместим с существующей архитектурой проекта
/// </summary>
public interface IInteractiveNormsAnalyzer
{
    /// <summary>
    /// Анализирует участок с возможностью фильтрации
    /// </summary>
    Task<BasicAnalysisResult> AnalyzeSectionAsync(string sectionName, string? specificNormId = null);

    /// <summary>
    /// Получить список доступных участков
    /// </summary>
    Task<IEnumerable<string>> GetAvailableSectionsAsync();

    /// <summary>
    /// Получить статистику обработки
    /// </summary>
    BasicProcessingStats GetProcessingStats();

    /// <summary>
    /// Текущие загруженные маршруты
    /// </summary>
    IReadOnlyList<Route> LoadedRoutes { get; }

    /// <summary>
    /// CHAT 2-4: Загрузка маршрутов из HTML файлов
    /// Точная копия Python load_routes_from_html
    /// </summary>
    Task<bool> LoadRoutesFromHtmlAsync(List<string> htmlFiles);

    /// <summary>
    /// CHAT 2-4: Загрузка норм из HTML файлов  
    /// Точная копия Python load_norms_from_html
    /// </summary>
    Task<bool> LoadNormsFromHtmlAsync(List<string> htmlFiles);

    /// <summary>
    /// CHAT 2-4: Результаты анализа участков
    /// </summary>
    IReadOnlyDictionary<string, BasicAnalysisResult> AnalyzedResults { get; }

    /// <summary>
    /// НОВОЕ CHAT 3-4: Карта участков и норм
    /// Точная копия Python sections_norms_map
    /// </summary>
    IReadOnlyDictionary<string, List<string>> GetSectionsNormsMap();

    /// <summary>
    /// НОВОЕ CHAT 3-4: Статистика по участкам
    /// </summary>
    Task<Dictionary<string, int>> GetSectionStatisticsAsync();

    /// <summary>
    /// НОВОЕ CHAT 3-4: Проверка целостности данных
    /// </summary>
    Task<DataIntegrityReport> ValidateDataIntegrityAsync();
}

/// <summary>
/// Результат анализа участка - совместим с существующими типами проекта
/// </summary>
public record BasicAnalysisResult
{
    public string SectionName { get; init; } = string.Empty;
    public int TotalRoutes { get; init; }
    public int AnalyzedItems { get; init; }
    public decimal MeanDeviation { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public DateTime AnalysisDate { get; init; }
    
    public static BasicAnalysisResult Empty(string sectionName) => new()
    {
        SectionName = sectionName,
        AnalysisDate = DateTime.UtcNow
    };

    /// <summary>
    /// Качественная оценка результата
    /// </summary>
    public string QualityAssessment => MeanDeviation switch
    {
        < 5 => "Отличное",
        < 10 => "Хорошее", 
        < 20 => "Приемлемое",
        < 30 => "Плохое",
        _ => "Критическое"
    };
}

/// <summary>
/// Статистика обработки - совместима с существующими типами
/// </summary>
public record BasicProcessingStats
{
    public int TotalRoutesLoaded { get; init; }
    public int TotalNormsLoaded { get; init; }
    public int TotalErrors { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public DateTime LastProcessingDate { get; init; }

    /// <summary>
    /// Скорость обработки (маршрутов в секунду)
    /// </summary>
    public decimal ProcessingRate => ProcessingTime.TotalSeconds > 0 
        ? (decimal)(TotalRoutesLoaded / ProcessingTime.TotalSeconds) 
        : 0;

    /// <summary>
    /// Процент успешности обработки
    /// </summary>
    public decimal SuccessRate => TotalRoutesLoaded + TotalErrors > 0 
        ? (decimal)TotalRoutesLoaded / (TotalRoutesLoaded + TotalErrors) * 100 
        : 0;
}