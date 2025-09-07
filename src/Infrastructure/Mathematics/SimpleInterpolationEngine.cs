// Infrastructure/Mathematics/SimpleInterpolationEngine.cs
using AnalysisNorm.Infrastructure.Logging;

namespace AnalysisNorm.Infrastructure.Mathematics;

/// <summary>
/// Упрощенный движок интерполяции без Math.NET зависимости
/// Использует только встроенную математику .NET 9
/// </summary>
public class SimpleInterpolationEngine : IDisposable
{
    private readonly IApplicationLogger _logger;
    private bool _disposed = false;

    public SimpleInterpolationEngine(IApplicationLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Простая линейная интерполяция между двумя точками
    /// </summary>
    public decimal LinearInterpolate(decimal x1, decimal y1, decimal x2, decimal y2, decimal x)
    {
        if (x1 == x2) return y1; // Избегаем деления на ноль

        // Формула: y = y1 + (x - x1) * (y2 - y1) / (x2 - x1)
        var result = y1 + (x - x1) * (y2 - y1) / (x2 - x1);
        
        _logger.LogDebug("Линейная интерполяция: ({X1},{Y1}) -> ({X2},{Y2}) для X={X} = {Result}", 
            x1, y1, x2, y2, x, result);
        
        return result;
    }

    /// <summary>
    /// Интерполяция по массиву точек
    /// </summary>
    public decimal InterpolateFromPoints(DataPoint[] points, decimal x)
    {
        if (points == null || points.Length == 0)
        {
            _logger.LogWarning("Нет точек для интерполяции");
            return 0;
        }

        if (points.Length == 1)
            return points[0].Y;

        // Сортируем точки по X
        var sortedPoints = points.OrderBy(p => p.X).ToArray();

        // Если X вне диапазона, возвращаем ближайшую точку
        if (x <= sortedPoints[0].X)
            return sortedPoints[0].Y;
        
        if (x >= sortedPoints[^1].X)
            return sortedPoints[^1].Y;

        // Находим две ближайшие точки
        for (int i = 0; i < sortedPoints.Length - 1; i++)
        {
            if (x >= sortedPoints[i].X && x <= sortedPoints[i + 1].X)
            {
                return LinearInterpolate(
                    sortedPoints[i].X, sortedPoints[i].Y,
                    sortedPoints[i + 1].X, sortedPoints[i + 1].Y,
                    x);
            }
        }

        return sortedPoints[^1].Y;
    }

    /// <summary>
    /// Вычисление среднего значения
    /// </summary>
    public decimal CalculateAverage(IEnumerable<decimal> values)
    {
        var valueList = values.ToList();
        if (!valueList.Any()) return 0;
        
        return valueList.Sum() / valueList.Count;
    }

    /// <summary>
    /// Вычисление стандартного отклонения
    /// </summary>
    public decimal CalculateStandardDeviation(IEnumerable<decimal> values)
    {
        var valueList = values.ToList();
        if (valueList.Count < 2) return 0;

        var average = CalculateAverage(valueList);
        var sumOfSquares = valueList.Sum(x => (x - average) * (x - average));
        var variance = sumOfSquares / (valueList.Count - 1);

        // Используем Math.Sqrt и приводим к decimal
        var stdDev = (decimal)Math.Sqrt((double)variance);
        
        _logger.LogDebug("Стандартное отклонение для {Count} значений: {StdDev}", 
            valueList.Count, stdDev);
        
        return stdDev;
    }

    /// <summary>
    /// Процентное отклонение
    /// </summary>
    public decimal CalculatePercentageDeviation(decimal actual, decimal expected)
    {
        if (expected == 0) return 0;
        
        var deviation = ((actual - expected) / expected) * 100;
        
        _logger.LogDebug("Отклонение: факт={Actual}, норма={Expected}, отклонение={Deviation}%", 
            actual, expected, deviation);
        
        return deviation;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogDebug("SimpleInterpolationEngine disposed");
            _disposed = true;
        }
    }
}

/// <summary>
/// Простая точка данных
/// </summary>
public record DataPoint
{
    public decimal X { get; init; }
    public decimal Y { get; init; }
}

/// <summary>
/// Методы интерполяции (упрощенные)
/// </summary>
public enum InterpolationMethod
{
    Linear = 1,      // Только линейная интерполяция
    Average = 2      // Среднее значение
}