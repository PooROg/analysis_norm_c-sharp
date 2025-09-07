// Services/Interfaces/IAdvancedConfigurationService.cs
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Расширенный интерфейс управления конфигурацией для CHAT 4
/// Наследует базовый IConfigurationService и добавляет расширенные возможности
/// Избегает конфликта с существующим интерфейсом
/// </summary>
public interface IAdvancedConfigurationService : IConfigurationService
{
    /// <summary>
    /// Получение конфигурации определенного типа с валидацией
    /// </summary>
    T GetValidatedConfiguration<T>() where T : class, new();

    /// <summary>
    /// Обновление конфигурации с расширенной валидацией
    /// </summary>
    Task<ConfigurationUpdateResult> UpdateConfigurationAsync<T>(T newConfiguration) where T : class, new();

    /// <summary>
    /// Сброс конфигурации к значениям по умолчанию с бэкапом
    /// </summary>
    Task<ProcessingResult<string>> ResetToDefaultsAsync<T>() where T : class, new();

    /// <summary>
    /// Получение всех конфигураций для диагностики
    /// </summary>
    Dictionary<string, object> GetAllConfigurations();

    /// <summary>
    /// Экспорт конфигурации в JSON с валидацией
    /// </summary>
    Task<ProcessingResult<string>> ExportConfigurationAsync();

    /// <summary>
    /// Импорт конфигурации из JSON с проверками
    /// </summary>
    Task<ProcessingResult<ImportResult>> ImportConfigurationAsync(string jsonConfiguration);

    /// <summary>
    /// Получение детализированной диагностической информации
    /// </summary>
    ConfigurationDiagnostics GetDiagnostics();

    /// <summary>
    /// Создание резервной копии конфигурации
    /// </summary>
    Task<ProcessingResult<string>> CreateBackupAsync();

    /// <summary>
    /// Восстановление из резервной копии
    /// </summary>
    Task<ProcessingResult<bool>> RestoreFromBackupAsync(string backupPath);

    /// <summary>
    /// Валидация конфигурации без применения изменений
    /// </summary>
    Task<ConfigurationValidationResult> ValidateConfigurationAsync<T>(T configuration) where T : class;
}

/// <summary>
/// Результат обновления конфигурации
/// </summary>
public record ConfigurationUpdateResult
{
    public bool IsSuccess { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
    public List<string> ValidationWarnings { get; init; } = new();
    public string? BackupPath { get; init; }
    public DateTime UpdateTime { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Результат импорта конфигурации
/// </summary>
public record ImportResult
{
    public int ImportedConfigurations { get; init; }
    public List<string> FailedConfigurations { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string? BackupPath { get; init; }
}

/// <summary>
/// Диагностика конфигурации
/// </summary>
public record ConfigurationDiagnostics
{
    public int LoadedConfigurationsCount { get; init; }
    public List<string> ConfigurationTypes { get; init; } = new();
    public DateTime LastUpdateTime { get; init; }
    public long TotalConfigurationSize { get; init; }
    public bool HasValidationErrors { get; init; }
    public List<string> ValidationIssues { get; init; } = new();
    public int BackupCount { get; init; }
    public SystemHealth ConfigurationHealth { get; init; }
}

/// <summary>
/// Результат валидации конфигурации
/// </summary>
public record ConfigurationValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public List<string> Suggestions { get; init; } = new();
    public Dictionary<string, object> ValidationMetadata { get; init; } = new();
}