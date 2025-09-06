// Infrastructure/Mathematics/InterpolationEngine.cs
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Infrastructure.Mathematics;

/// <summary>
/// Математический движок интерполяции - совместим с существующей структурой проекта
/// Гиперболическая интерполяция как в Python, но без внешних зависимостей
/// </summary>
public class InterpolationEngine
{
    /// <summary>
    /// Создает функцию интерполяции из точек нормы
    /// Точная копия Python scipy алгоритма с гиперболической интерполяцией
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
            _ => CreateMultiPointHyperbolic(sortedPoints)
        };
    }

    /// <summary>
    /// Константная функция для одной точки
    /// </summary>
    private static Func<decimal, decimal> CreateConstantFunction(decimal value)
    {
        return _ => value;
    }

    /// <summary>
    /// Двухточечная гиперболическая интерполяция: y = a/x + b
    /// Решаем систему: y1 = a/x1 + b, y2 = a/x2 + b
    /// </summary>
    private static Func<decimal, decimal> CreateHyperbolicFunction(DataPoint p1, DataPoint p2)
    {
        // Проверяем деление на ноль
        if (p1.X == 0 || p2.X == 0)
            return x => p1.X != 0 ? p1.Y : p2.Y;

        // Проверяем близость точек по X
        if (Math.Abs(p2.X - p1.X) < 0.001m)
            return CreateConstantFunction((p1.Y + p2.Y) / 2);

        // Решаем систему уравнений для y = a/x + b
        // y1 = a/x1 + b  =>  a = (y1 - b) * x1
        // y2 = a/x2 + b  =>  a = (y2 - b) * x2
        // (y1 - b) * x1 = (y2 - b) * x2
        // y1*x1 - b*x1 = y2*x2 - b*x2
        // b*(x2 - x1) = y2*x2 - y1*x1
        // b = (y2*x2 - y1*x1) / (x2 - x1)

        decimal denominator = p2.X - p1.X;
        decimal b = (p2.Y * p2.X - p1.Y * p1.X) / denominator;
        decimal a = (p1.Y - b) * p1.X;

        return x =>
        {
            if (x == 0) return b; // Предотвращаем деление на ноль
            return a / x + b;
        };
    }

    /// <summary>
    /// Многоточечная гиперболическая интерполяция методом наименьших квадратов
    /// Минимизируем сумму квадратов отклонений для y = a/x + b
    /// </summary>
    private static Func<decimal, decimal> CreateMultiPointHyperbolic(DataPoint[] points)
    {
        // Фильтруем точки с X != 0 для гиперболической функции
        var validPoints = points.Where(p => p.X != 0).ToArray();
        if (validPoints.Length < 2)
            return CreateLinearFallback(points);

        try
        {
            int n = validPoints.Length;
            decimal sumInvX = 0, sumY = 0, sumInvXY = 0, sumInvX2 = 0;

            // Вычисляем суммы для метода наименьших квадратов
            foreach (var point in validPoints)
            {
                decimal invX = 1 / point.X;
                sumInvX += invX;
                sumY += point.Y;
                sumInvXY += invX * point.Y;
                sumInvX2 += invX * invX;
            }

            // Решаем нормальные уравнения для a и b
            // n*b + a*sumInvX = sumY
            // b*sumInvX + a*sumInvX2 = sumInvXY
            
            decimal denominator = n * sumInvX2 - sumInvX * sumInvX;
            if (Math.Abs(denominator) < 0.0001m)
                return CreateLinearFallback(points);

            decimal a = (n * sumInvXY - sumInvX * sumY) / denominator;
            decimal b = (sumY * sumInvX2 - sumInvX * sumInvXY) / denominator;

            return x =>
            {
                if (x == 0) return b;
                return a / x + b;
            };
        }
        catch
        {
            // При любых математических ошибках переходим к линейной интерполяции
            return CreateLinearFallback(points);
        }
    }

    /// <summary>
    /// Fallback к линейной интерполяции при ошибках гиперболической
    /// </summary>
    private static Func<decimal, decimal> CreateLinearFallback(DataPoint[] points)
    {
        var sortedPoints = points.OrderBy(p => p.X).ToArray();
        
        return x =>
        {
            if (x <= sortedPoints[0].X) return sortedPoints[0].Y;
            if (x >= sortedPoints[^1].X) return sortedPoints[^1].Y;

            // Найти соседние точки для линейной интерполяции
            for (int i = 0; i < sortedPoints.Length - 1; i++)
            {
                if (x >= sortedPoints[i].X && x <= sortedPoints[i + 1].X)
                {
                    decimal t = (x - sortedPoints[i].X) / (sortedPoints[i + 1].X - sortedPoints[i].X);
                    return sortedPoints[i].Y + t * (sortedPoints[i + 1].Y - sortedPoints[i].Y);
                }
            }

            return sortedPoints[^1].Y;
        };
    }
}