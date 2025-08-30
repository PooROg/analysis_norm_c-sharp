using System.Text;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Детектор кодировки файлов
/// Соответствует Python read_text функциональности
/// </summary>
public interface IFileEncodingDetector
{
    /// <summary>
    /// Определяет кодировку файла методом проб
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <returns>Имя кодировки (cp1251, utf-8, etc.)</returns>
    Task<string> DetectEncodingAsync(string filePath);
    
    /// <summary>
    /// Читает файл с автоматическим определением кодировки
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <returns>Содержимое файла в правильной кодировке</returns>
    Task<string> ReadTextWithEncodingDetectionAsync(string filePath);

    /// <summary>
    /// Получает Encoding по имени
    /// </summary>
    /// <param name="encodingName">Имя кодировки</param>
    /// <returns>Объект Encoding</returns>
    Encoding GetEncoding(string encodingName);

    /// <summary>
    /// Проверяет текст на ошибки декодирования
    /// </summary>
    /// <param name="text">Текст для проверки</param>
    /// <returns>true если есть ошибки декодирования</returns>
    bool HasDecodingErrors(string text);
}