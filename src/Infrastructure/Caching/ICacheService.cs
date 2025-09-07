// Infrastructure/Caching/ICacheService.cs
namespace AnalysisNorm.Infrastructure.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task RemoveAsync(string key);
    Task ClearAsync();
}

// Infrastructure/Caching/MemoryCacheService.cs
using Microsoft.Extensions.Caching.Memory;

namespace AnalysisNorm.Infrastructure.Caching;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;

    public MemoryCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        _memoryCache.TryGetValue(key, out var value);
        return Task.FromResult(value as T);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        var options = new MemoryCacheEntryOptions();
        if (expiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiration.Value;
        }
        
        _memoryCache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        if (_memoryCache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0);
        }
        return Task.CompletedTask;
    }
}

// Infrastructure/Logging/IApplicationLogger.cs
namespace AnalysisNorm.Infrastructure.Logging;

public interface IApplicationLogger
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(Exception exception, string message, params object[] args);
    void LogError(string message, params object[] args);
    void LogDebug(string message, params object[] args);
    void LogCritical(Exception exception, string message, params object[] args);
}

// Infrastructure/Logging/SerilogLogger.cs
using Serilog;

namespace AnalysisNorm.Infrastructure.Logging;

public class SerilogLogger : IApplicationLogger
{
    private readonly ILogger _logger;

    public SerilogLogger()
    {
        _logger = Log.Logger;
    }

    public void LogInformation(string message, params object[] args) =>
        _logger.Information(message, args);

    public void LogWarning(string message, params object[] args) =>
        _logger.Warning(message, args);

    public void LogError(Exception exception, string message, params object[] args) =>
        _logger.Error(exception, message, args);

    public void LogError(string message, params object[] args) =>
        _logger.Error(message, args);

    public void LogDebug(string message, params object[] args) =>
        _logger.Debug(message, args);

    public void LogCritical(Exception exception, string message, params object[] args) =>
        _logger.Fatal(exception, message, args);
}

// Infrastructure/Mathematics/InterpolationEngine.cs
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Infrastructure.Mathematics;

/// <summary>
/// Движок интерполяции для создания функций норм
/// Поддерживает гиперболическую интерполяцию как в Python версии
/// </summary>
public class InterpolationEngine
{
    /// <summary>
    /// Создать функцию интерполяции из точек нормы
    /// </summary>
    public Func<decimal, decimal> CreateInterpolationFunction(IReadOnlyList<DataPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        
        if (points.Count == 0)
            throw new ArgumentException("Need at least one point for interpolation");

        var sortedPoints = points.OrderBy(p => p.X).ToArray();

        return points.Count switch
        {
            1 => CreateConstantFunction(sortedPoints[0].Y),
            2 => CreateHyperbolicFunction(sortedPoints[0], sortedPoints[1]),
            _ => CreateSplineFunction(sortedPoints)
        };
    }

    private static Func<decimal, decimal> CreateConstantFunction(decimal value)
    {
        return _ => value;
    }

    private static Func<decimal, decimal> CreateHyperbolicFunction(DataPoint p1, DataPoint p2)
    {
        if (Math.Abs(p2.X - p1.X) < 0.001m)
            return CreateConstantFunction((p1.Y + p2.Y) / 2);

        // y = A/x + B формула
        var a = (p1.Y - p2.Y) * p1.X * p2.X / (p2.X - p1.X);
        var b = (p2.Y * p2.X - p1.Y * p1.X) / (p2.X - p1.X);

        return x => x <= 0 ? p1.Y : a / x + b;
    }

    private static Func<decimal, decimal> CreateSplineFunction(DataPoint[] points)
    {
        // Упрощенная сплайн интерполяция для множественных точек
        return x =>
        {
            if (x <= points[0].X) return points[0].Y;
            if (x >= points[^1].X) return points[^1].Y;

            // Линейная интерполяция между ближайшими точками
            for (int i = 0; i < points.Length - 1; i++)
            {
                if (x >= points[i].X && x <= points[i + 1].X)
                {
                    var t = (x - points[i].X) / (points[i + 1].X - points[i].X);
                    return points[i].Y + t * (points[i + 1].Y - points[i].Y);
                }
            }

            return points[^1].Y;
        };
    }
}

// Services/Implementation/StubHtmlParser.cs (заглушка для будущих чатов)
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Services.Implementation;

public class StubHtmlParser : IHtmlParser
{
    public Task<ProcessingResult<IEnumerable<Route>>> ParseRoutesAsync(Stream htmlContent, object? options = null)
    {
        var emptyResult = ProcessingResult<IEnumerable<Route>>.Success(Array.Empty<Route>());
        return Task.FromResult(emptyResult);
    }

    public Task<ProcessingResult<IEnumerable<Norm>>> ParseNormsAsync(Stream htmlContent, object? options = null)
    {
        var emptyResult = ProcessingResult<IEnumerable<Norm>>.Success(Array.Empty<Norm>());
        return Task.FromResult(emptyResult);
    }
}

// Services/Implementation/StubExcelExporter.cs (заглушка для будущих чатов)
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

public class StubExcelExporter : IExcelExporter
{
    public Task<bool> ExportRoutesAsync(IEnumerable<object> routes, string filePath)
    {
        return Task.FromResult(false);
    }

    public Task<bool> ExportAnalysisAsync(object analysisResult, string filePath)
    {
        return Task.FromResult(false);
    }
}

// Services/Implementation/FileService.cs
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

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
        return await Task.FromResult(File.OpenRead(filePath));
    }

    public async Task<byte[]> ReadAllBytesAsync(string filePath)
    {
        return await File.ReadAllBytesAsync(filePath);
    }

    public async Task WriteAllBytesAsync(string filePath, byte[] data)
    {
        await File.WriteAllBytesAsync(filePath, data);
    }

    public async Task<string[]> GetFilesAsync(string directory, string searchPattern = "*")
    {
        return await Task.FromResult(Directory.GetFiles(directory, searchPattern));
    }
}

// Services/Interfaces/IFileService.cs
namespace AnalysisNorm.Services.Interfaces;

public interface IFileService
{
    Task<bool> FileExistsAsync(string filePath);
    Task<Stream> OpenReadAsync(string filePath);
    Task<byte[]> ReadAllBytesAsync(string filePath);
    Task WriteAllBytesAsync(string filePath, byte[] data);
    Task<string[]> GetFilesAsync(string directory, string searchPattern = "*");
}

// Services/Interfaces/IHtmlParser.cs (заглушка)
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Services.Interfaces;

public interface IHtmlParser
{
    Task<ProcessingResult<IEnumerable<Route>>> ParseRoutesAsync(Stream htmlContent, object? options = null);
    Task<ProcessingResult<IEnumerable<Norm>>> ParseNormsAsync(Stream htmlContent, object? options = null);
}

// Services/Interfaces/IExcelExporter.cs (заглушка)
namespace AnalysisNorm.Services.Interfaces;

public interface IExcelExporter
{
    Task<bool> ExportRoutesAsync(IEnumerable<object> routes, string filePath);
    Task<bool> ExportAnalysisAsync(object analysisResult, string filePath);
}