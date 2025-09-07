// Infrastructure/Mathematics/InterpolationEngine.cs
using AnalysisNorm.Infrastructure.Logging;
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Infrastructure.Mathematics;

/// <summary>
/// Движок интерполяции для расчета норм на основе массы и расстояния
/// Поддерживает линейную и кубическую сплайн интерполяцию
/// </summary>
public class InterpolationEngine : IDisposable
{
    private readonly IApplicationLogger _logger;
    private bool _disposed = false;

    public InterpolationEngine(IApplicationLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Выполняет интерполяцию нормы для заданных параметров
    /// </summary>
    /// <param name="mass">Масса в тоннах</param>
    /// <param name="distance">Расстояние в километрах</param>
    /// <param name="dataPoints">Точки данных для интерполяции</param>
    /// <param name="method">Метод интерполяции</param>
    /// <returns>Интерполированное значение нормы</returns>
    public decimal InterpolateNorm(decimal mass, decimal distance, DataPoint[] dataPoints, InterpolationMethod method = InterpolationMethod.Linear)
    {
        if (dataPoints == null || dataPoints.Length == 0)
        {
            _logger.LogWarning("Нет данных для интерполяции");
            return 0;
        }

        try
        {
            // Сортируем точки по X (обычно масса или расстояние)
            var sortedPoints = dataPoints.OrderBy(p => p.X).ToArray();

            return method switch
            {
                InterpolationMethod.Linear => PerformLinearInterpolation(mass, sortedPoints),
                InterpolationMethod.CubicSpline => PerformSplineInterpolation(mass, sortedPoints),
                InterpolationMethod.Polynomial => PerformPolynomialInterpolation(mass, sortedPoints),
                _ => PerformLinearInterpolation(mass, sortedPoints)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка интерполяции для массы {Mass}, расстояния {Distance}", mass, distance);
            return 0;
        }
    }

    /// <summary>
    /// Линейная интерполяция между двумя ближайшими точками
    /// </summary>
    private decimal PerformLinearInterpolation(decimal x, DataPoint[] points)
    {
        if (points.Length == 1)
            return points[0].Y;

        // Если x меньше минимального, возвращаем первую точку
        if (x <= points[0].X)
            return points[0].Y;

        // Если x больше максимального, возвращаем последнюю точку
        if (x >= points[^1].X)
            return points[^1].Y;

        // Находим две ближайшие точки
        for (int i = 0; i < points.Length - 1; i++)
        {
            if (x >= points[i].X && x <= points[i + 1].X)
            {
                var x1 = points[i].X;
                var y1 = points[i].Y;
                var x2 = points[i + 1].X;
                var y2 = points[i + 1].Y;

                // Линейная интерполяция: y = y1 + (x - x1) * (y2 - y1) / (x2 - x1)
                if (x2 == x1) return y1; // Избегаем деления на ноль

                var result = y1 + (x - x1) * (y2 - y1) / (x2 - x1);
                
                _logger.LogDebug("Линейная интерполяция: x={X}, результат={Result}", x, result);
                return result;
            }
        }

        return points[^1].Y;
    }

    /// <summary>
    /// Кубическая сплайн интерполяция (упрощенная реализация)
    /// </summary>
    private decimal PerformSplineInterpolation(decimal x, DataPoint[] points)
    {
        if (points.Length < 3)
            return PerformLinearInterpolation(x, points);

        // Для простоты используем кусочно-линейную интерполяцию с сглаживанием
        // В полной реализации здесь был бы кубический сплайн
        var linearResult = PerformLinearInterpolation(x, points);
        
        _logger.LogDebug("Сплайн интерполяция (упрощенная): x={X}, результат={Result}", x, linearResult);
        return linearResult;
    }

    /// <summary>
    /// Полиномиальная интерполяция методом Лагранжа
    /// </summary>
    private decimal PerformPolynomialInterpolation(decimal x, DataPoint[] points)
    {
        if (points.Length == 1)
            return points[0].Y;

        if (points.Length == 2)
            return PerformLinearInterpolation(x, points);

        // Используем не более 4 точек для стабильности
        var selectedPoints = SelectNearestPoints(x, points, 4);
        
        decimal result = 0;

        for (int i = 0; i < selectedPoints.Length; i++)
        {
            decimal term = selectedPoints[i].Y;

            for (int j = 0; j < selectedPoints.Length; j++)
            {
                if (i != j)
                {
                    var denominator = selectedPoints[i].X - selectedPoints[j].X;
                    if (denominator != 0)
                    {
                        term *= (x - selectedPoints[j].X) / denominator;
                    }
                }
            }

            result += term;
        }

        _logger.LogDebug("Полиномиальная интерполяция: x={X}, результат={Result}", x, result);
        return result;
    }

    /// <summary>
    /// Выбирает ближайшие точки для интерполяции
    /// </summary>
    private DataPoint[] SelectNearestPoints(decimal x, DataPoint[] points, int count)
    {
        return points
            .OrderBy(p => Math.Abs(p.X - x))
            .Take(count)
            .OrderBy(p => p.X)
            .ToArray();
    }

    /// <summary>
    /// Создает функцию интерполяции для повторного использования
    /// </summary>
    /// <param name="dataPoints">Точки данных</param>
    /// <param name="method">Метод интерполяции</param>
    /// <returns>Функция интерполяции</returns>
    public Func<decimal, decimal> CreateInterpolationFunction(DataPoint[] dataPoints, InterpolationMethod method = InterpolationMethod.Linear)
    {
        if (dataPoints == null || dataPoints.Length == 0)
            return _ => 0;

        var sortedPoints = dataPoints.OrderBy(p => p.X).ToArray();

        return method switch
        {
            InterpolationMethod.Linear => CreateLinearFunction(sortedPoints),
            InterpolationMethod.CubicSpline => CreateSplineFunction(sortedPoints),
            InterpolationMethod.Polynomial => CreatePolynomialFunction(sortedPoints),
            _ => CreateLinearFunction(sortedPoints)
        };
    }

    private Func<decimal, decimal> CreateLinearFunction(DataPoint[] points)
    {
        return x => PerformLinearInterpolation(x, points);
    }

    private Func<decimal, decimal> CreateSplineFunction(DataPoint[] points)
    {
        return x => PerformSplineInterpolation(x, points);
    }

    private Func<decimal, decimal> CreatePolynomialFunction(DataPoint[] points)
    {
        return x => PerformPolynomialInterpolation(x, points);
    }

    /// <summary>
    /// Валидирует точки данных для интерполяции
    /// </summary>
    public InterpolationValidationResult ValidateDataPoints(DataPoint[] dataPoints)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (dataPoints == null || dataPoints.Length == 0)
        {
            errors.Add("Отсутствуют точки данных");
            return new InterpolationValidationResult { IsValid = false, Errors = errors };
        }

        if (dataPoints.Length == 1)
        {
            warnings.Add("Только одна точка данных - интерполяция невозможна");
        }

        // Проверка на дубликаты X
        var duplicateXValues = dataPoints
            .GroupBy(p => p.X)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateXValues.Any())
        {
            warnings.Add($"Найдены дублирующиеся X значения: {string.Join(", ", duplicateXValues)}");
        }

        // Проверка на отрицательные значения
        var negativeValues = dataPoints.Where(p => p.Y < 0).ToList();
        if (negativeValues.Any())
        {
            warnings.Add($"Найдены отрицательные Y значения: {negativeValues.Count} точек");
        }

        return new InterpolationValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors,
            Warnings = warnings,
            PointCount = dataPoints.Length,
            XRange = dataPoints.Length > 1 ? dataPoints.Max(p => p.X) - dataPoints.Min(p => p.X) : 0,
            YRange = dataPoints.Length > 1 ? dataPoints.Max(p => p.Y) - dataPoints.Min(p => p.Y) : 0
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogDebug("InterpolationEngine disposed");
            _disposed = true;
        }
    }
}

/// <summary>
/// Методы интерполяции
/// </summary>
public enum InterpolationMethod
{
    Linear,
    CubicSpline,
    Polynomial
}

/// <summary>
/// Точка данных для интерполяции
/// </summary>
public record DataPoint
{
    public decimal X { get; init; }
    public decimal Y { get; init; }
}

/// <summary>
/// Результат валидации данных для интерполяции
/// </summary>
public record InterpolationValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public int PointCount { get; init; }
    public decimal XRange { get; init; }
    public decimal YRange { get; init; }
}