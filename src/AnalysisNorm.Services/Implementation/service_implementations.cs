using Microsoft.Extensions.Logging;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Утилитарные хелперы, которые не имеют отдельных файлов реализации
/// 
/// ВАЖНО: Все основные сервисы (FileEncodingDetector, TextNormalizer, ApplicationSettings)
/// теперь находятся в Utils/utility_classes.cs для устранения дубликатов
/// 
/// УДАЛЕНЫ все дублирующиеся классы: DataAnalysisService, NormInterpolationService, 
/// AnalysisCacheService, NormStorageService, TextNormalizer, FileEncodingDetector,
/// ApplicationSettings - они уже реализованы в отдельных файлах
/// </summary>

/// <summary>
/// Статический хелпер для валидации данных
/// Используется различными сервисами для проверки входящих данных
/// </summary>
public static class DataValidationHelper
{
    /// <summary>
    /// Проверяет является ли значение пустым или эквивалентным пустому
    /// Расширенная версия для поддержки различных форматов "пустых" значений
    /// Соответствует проверкам на None/NaN из Python
    /// </summary>
    /// <param name="input">Входное значение</param>
    /// <returns>true если значение считается пустым</returns>
    public static bool IsEmpty(object? input)
    {
        if (input == null) return true;

        if (input is string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return true;

            var normalized = str.Trim().ToLowerInvariant();
            var emptyValues = new[] { "-", "—", "н/д", "n/a", "nan", "none", "null", "не указано", "не определено" };

            return emptyValues.Contains(normalized);
        }

        // Проверка для числовых типов
        return input switch
        {
            decimal d => d == 0,
            double db => double.IsNaN(db) || db == 0,
            float f => float.IsNaN(f) || f == 0,
            int i => i == 0,
            long l => l == 0,
            _ => false
        };
    }

    /// <summary>
    /// Проверяет валидность числового диапазона
    /// </summary>
    /// <param name="value">Значение для проверки</param>
    /// <param name="min">Минимальное значение</param>
    /// <param name="max">Максимальное значение</param>
    /// <returns>true если значение в допустимом диапазоне</returns>
    public static bool IsInRange(decimal value, decimal min, decimal max)
    {
        return value >= min && value <= max;
    }

    /// <summary>
    /// Проверяет валидность строки как идентификатора
    /// </summary>
    /// <param name="id">Строка идентификатора</param>
    /// <returns>true если строка может использоваться как ID</returns>
    public static bool IsValidId(string? id)
    {
        return !string.IsNullOrWhiteSpace(id) &&
               id.Length <= 50 &&
               !IsEmpty(id);
    }
}

// ПРИМЕЧАНИЕ: Все основные классы (FileEncodingDetector, TextNormalizer, ApplicationSettings)
// теперь находятся в Utils/utility_classes.cs и не должны дублироваться здесь.