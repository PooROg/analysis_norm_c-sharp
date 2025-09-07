// СОЗДАТЬ НОВЫЙ ФАЙЛ: Services/Implementation/FileService.cs

using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Infrastructure.Logging;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Реализация сервиса работы с файлами
/// Простая обертка над File.* методами с логированием
/// </summary>
public class FileService : IFileService
{
    private readonly IApplicationLogger _logger;

    public FileService(IApplicationLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        return await Task.FromResult(File.Exists(filePath));
    }

    public async Task<Stream> OpenReadAsync(string filePath)
    {
        _logger.LogDebug("Открытие файла для чтения: {FilePath}", filePath);
        return await Task.FromResult(File.OpenRead(filePath));
    }

    public async Task<byte[]> ReadAllBytesAsync(string filePath)
    {
        _logger.LogDebug("Чтение всего файла: {FilePath}", filePath);
        return await File.ReadAllBytesAsync(filePath);
    }

    public async Task WriteAllBytesAsync(string filePath, byte[] data)
    {
        _logger.LogDebug("Запись файла: {FilePath}, размер: {Size} байт", filePath, data.Length);
        await File.WriteAllBytesAsync(filePath, data);
    }

    public async Task<string[]> GetFilesAsync(string directory, string searchPattern = "*")
    {
        _logger.LogDebug("Поиск файлов в {Directory} по маске {Pattern}", directory, searchPattern);
        return await Task.FromResult(Directory.GetFiles(directory, searchPattern));
    }
}