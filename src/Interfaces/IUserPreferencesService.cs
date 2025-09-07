// Services/Interfaces/IUserPreferencesService.cs
using AnalysisNorm.Models.Domain;
using AnalysisNorm.ViewModels;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Интерфейс управления пользовательскими настройками
/// Полностью новый интерфейс без конфликтов
/// </summary>
public interface IUserPreferencesService
{
    /// <summary>
    /// Загрузка пользовательских настроек
    /// </summary>
    Task<UserPreferences> LoadUserPreferencesAsync();

    /// <summary>
    /// Сохранение пользовательских настроек
    /// </summary>
    Task<ProcessingResult<bool>> SaveUserPreferencesAsync(UserPreferences preferences);

    /// <summary>
    /// Сброс к настройкам по умолчанию
    /// </summary>
    Task<ProcessingResult<UserPreferences>> ResetToDefaultsAsync();

    /// <summary>
    /// Экспорт настроек в файл
    /// </summary>
    Task<ProcessingResult<string>> ExportPreferencesAsync(string filePath);

    /// <summary>
    /// Импорт настроек из файла
    /// </summary>
    Task<ProcessingResult<UserPreferences>> ImportPreferencesAsync(string filePath);

    /// <summary>
    /// Получение настройки по ключу
    /// </summary>
    T? GetPreference<T>(string key, T? defaultValue = default);

    /// <summary>
    /// Установка настройки по ключу
    /// </summary>
    Task SetPreferenceAsync<T>(string key, T value);

    /// <summary>
    /// Добавление недавнего файла
    /// </summary>
    Task AddRecentFileAsync(string filePath);

    /// <summary>
    /// Очистка списка недавних файлов
    /// </summary>
    Task ClearRecentFilesAsync();

    /// <summary>
    /// Сохранение позиции окна
    /// </summary>
    Task SaveWindowPositionAsync(WindowPosition position);

    /// <summary>
    /// Получение событий изменения настроек
    /// </summary>
    event EventHandler<UserPreferencesChangedEventArgs>? PreferencesChanged;
}

/// <summary>
/// Аргументы события изменения настроек
/// </summary>
public class UserPreferencesChangedEventArgs : EventArgs
{
    public string ChangedKey { get; init; } = string.Empty;
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
    public DateTime ChangeTime { get; init; } = DateTime.UtcNow;
}