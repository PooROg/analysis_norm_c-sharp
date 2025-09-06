// СОЗДАТЬ НОВЫЙ ФАЙЛ: Services/Interfaces/IFileService.cs

using AnalysisNorm.Infrastructure.Logging;

namespace AnalysisNorm.Services.Interfaces;

/// <summary>
/// Интерфейс для работы с файловой системой
/// Асинхронные операции для производительности
/// </summary>
public interface IFileService
{
    Task<bool> FileExistsAsync(string filePath);
    Task<Stream> OpenReadAsync(string filePath);
    Task<byte[]> ReadAllBytesAsync(string filePath);
    Task WriteAllBytesAsync(string filePath, byte[] data);
    Task<string[]> GetFilesAsync(string directory, string searchPattern = "*");
}