// === src/AnalysisNorm.Services/Interfaces/ITextNormalizer.cs ===
using System.Globalization;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Нормализатор текста и безопасные конверторы
/// Соответствует Python utils.py функциям
/// </summary>
public interface ITextNormalizer
{
    /// <summary>
    /// Нормализует текст - убирает лишние пробелы, HTML entities
    /// </summary>
    /// <param name="text">Исходный текст</param>
    /// <returns>Нормализованный текст</returns>
    string NormalizeText(string text);
    
    /// <summary>
    /// Безопасное преобразование к decimal
    /// </summary>
    /// <param name="input">Входное значение</param>
    /// <param name="defaultValue">Значение по умолчанию</param>
    /// <returns>Decimal значение или defaultValue</returns>
    decimal SafeDecimal(object? input, decimal defaultValue = 0);

    /// <summary>
    /// Безопасное преобразование к int
    /// </summary>
    /// <param name="input">Входное значение</param>
    /// <param name="defaultValue">Значение по умолчанию</param>
    /// <returns>Int значение или defaultValue</returns>
    int SafeInt(object? input, int defaultValue = 0);

    /// <summary>
    /// Безопасное преобразование к DateTime
    /// </summary>
    /// <param name="input">Входное значение</param>
    /// <param name="defaultValue">Значение по умолчанию</param>
    /// <returns>DateTime значение или defaultValue</returns>
    DateTime SafeDateTime(object? input, DateTime defaultValue = default);

    /// <summary>
    /// Проверяет является ли значение пустым/null
    /// </summary>
    /// <param name="input">Входное значение</param>
    /// <returns>true если значение считается пустым</returns>
    bool IsEmpty(object? input);

    /// <summary>
    /// Форматирует decimal с заданной точностью
    /// </summary>
    /// <param name="value">Значение для форматирования</param>
    /// <param name="decimals">Количество десятичных знаков</param>
    /// <param name="fallback">Строка в случае ошибки</param>
    /// <returns>Отформатированная строка</returns>
    string FormatDecimal(object? value, int decimals = 1, string fallback = "N/A");

    /// <summary>
    /// Нормализует название серии локомотива
    /// </summary>
    /// <param name="series">Исходное название серии</param>
    /// <returns>Нормализованное название</returns>
    string NormalizeLocomotiveSeries(string series);
}