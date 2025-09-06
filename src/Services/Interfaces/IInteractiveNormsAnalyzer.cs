// Services/Interfaces/IInteractiveNormsAnalyzer.cs
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Главный анализатор норм - совместимый с существующей архитектурой проекта
/// Упрощенный интерфейс без избыточных абстракций, использует существующие типы
/// </summary>
public interface IInteractiveNormsAnalyzer
{
    /// <summary>
    /// Анализирует участок с возможностью фильтрации - использует существующие типы данных
    /// </summary>
    Task<BasicAnalysisResult> AnalyzeSectionAsync(string sectionName, string? specificNormId = null);

    /// <summary>
    /// Получить список доступных участков
    /// </summary>
    Task<IEnumerable<string>> GetAvailableSectionsAsync();

    /// <summary>
    /// Получить статистику обработки - совместимо с существующими типами
    /// </summary>
    BasicProcessingStats GetProcessingStats();

    /// <summary>
    /// Текущие загруженные маршруты - использует существующий тип Route
    /// </summary>
    IReadOnlyList<Route> LoadedRoutes { get; }
}

/// <summary>
/// Упрощенный результат анализа - совместим с существующими типами проекта
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
}

/// <summary>
/// Упрощенная статистика обработки - совместима с существующими типами
/// </summary>
public record BasicProcessingStats
{
    public int TotalRoutesLoaded { get; init; }
    public int TotalNormsLoaded { get; init; }
    public int TotalErrors { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public DateTime LastProcessingDate { get; init; }
}