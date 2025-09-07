// Models/Domain/ProcessingResult.cs
namespace AnalysisNorm.Models.Domain;

/// <summary>
/// Универсальный результат операции с типизированными данными
/// Используется повсеместно в проекте для возврата результатов с информацией об успехе/ошибке
/// </summary>
public record ProcessingResult<T>
{
    public T? Data { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> Warnings { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Создать успешный результат с данными
    /// </summary>
    public static ProcessingResult<T> Success(T data) => new()
    {
        Data = data,
        IsSuccess = true
    };

    /// <summary>
    /// Создать результат с ошибкой
    /// </summary>
    public static ProcessingResult<T> Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };

    /// <summary>
    /// Создать успешный результат с предупреждениями
    /// </summary>
    public static ProcessingResult<T> SuccessWithWarnings(T data, List<string> warnings) => new()
    {
        Data = data,
        IsSuccess = true,
        Warnings = warnings
    };

    /// <summary>
    /// Создать успешный результат с метаданными
    /// </summary>
    public static ProcessingResult<T> SuccessWithMetadata(T data, Dictionary<string, object> metadata) => new()
    {
        Data = data,
        IsSuccess = true,
        Metadata = metadata
    };
}