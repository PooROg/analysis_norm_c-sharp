// Converters/ValueConverters.cs - Все конвертеры для CHAT 3-4
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AnalysisNorm.Models.Domain;

namespace AnalysisNorm.Converters;

/// <summary>
/// Конвертер статуса шага обработки в иконку
/// </summary>
public class ProcessingStepStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToLower() switch
        {
            "completed" => "✅",
            "inprogress" => "🔄", 
            "active" => "🔄",
            "error" => "❌",
            "pending" => "⏳",
            "warning" => "⚠️",
            "skipped" => "⏭️",
            _ => "⏳"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер статуса шага обработки в цвет
/// </summary>
public class ProcessingStepStatusToColorConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> ColorCache = new()
    {
        { "completed", new SolidColorBrush(Colors.Green) },
        { "inprogress", new SolidColorBrush(Colors.Blue) },
        { "active", new SolidColorBrush(Colors.Blue) },
        { "error", new SolidColorBrush(Colors.Red) },
        { "pending", new SolidColorBrush(Colors.Gray) },
        { "warning", new SolidColorBrush(Colors.Orange) },
        { "skipped", new SolidColorBrush(Colors.LightGray) }
    };

    static ProcessingStepStatusToColorConverter()
    {
        // Замораживаем кисти для производительности
        foreach (var brush in ColorCache.Values)
        {
            brush.Freeze();
        }
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.ToLower() ?? "pending";
        return ColorCache.TryGetValue(status, out var brush) ? brush : ColorCache["pending"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер уровня диагностического алерта в иконку
/// </summary>
public class DiagnosticAlertLevelToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AlertLevel level)
        {
            return level switch
            {
                AlertLevel.Info => "ℹ️",
                AlertLevel.Warning => "⚠️", 
                AlertLevel.Error => "❌",
                AlertLevel.Critical => "🔴",
                _ => "ℹ️"
            };
        }

        // Поддержка строкового значения
        return value?.ToString()?.ToLower() switch
        {
            "info" => "ℹ️",
            "warning" => "⚠️",
            "error" => "❌", 
            "critical" => "🔴",
            _ => "ℹ️"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер уровня диагностического алерта в цвет
/// </summary>
public class DiagnosticAlertLevelToColorConverter : IValueConverter
{
    private static readonly Dictionary<AlertLevel, SolidColorBrush> ColorMap = new()
    {
        { AlertLevel.Info, new SolidColorBrush(Color.FromRgb(33, 150, 243)) },      // Blue
        { AlertLevel.Warning, new SolidColorBrush(Color.FromRgb(255, 152, 0)) },    // Orange
        { AlertLevel.Error, new SolidColorBrush(Color.FromRgb(244, 67, 54)) },      // Red
        { AlertLevel.Critical, new SolidColorBrush(Color.FromRgb(183, 28, 28)) }    // Dark Red
    };

    static DiagnosticAlertLevelToColorConverter()
    {
        foreach (var brush in ColorMap.Values)
        {
            brush.Freeze();
        }
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AlertLevel level && ColorMap.TryGetValue(level, out var brush))
        {
            return brush;
        }

        // Поддержка строкового значения
        var stringValue = value?.ToString()?.ToLower();
        return stringValue switch
        {
            "info" => ColorMap[AlertLevel.Info],
            "warning" => ColorMap[AlertLevel.Warning], 
            "error" => ColorMap[AlertLevel.Error],
            "critical" => ColorMap[AlertLevel.Critical],
            _ => ColorMap[AlertLevel.Info]
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер статуса отклонения в цвет
/// </summary>
public class DeviationStatusToColorConverter : IValueConverter
{
    private static readonly Dictionary<DeviationStatus, SolidColorBrush> StatusColors = new()
    {
        { DeviationStatus.Excellent, new SolidColorBrush(Color.FromRgb(46, 125, 50)) },   // Dark Green
        { DeviationStatus.Good, new SolidColorBrush(Color.FromRgb(76, 175, 80)) },        // Green  
        { DeviationStatus.Acceptable, new SolidColorBrush(Color.FromRgb(255, 152, 0)) },  // Orange
        { DeviationStatus.Poor, new SolidColorBrush(Color.FromRgb(244, 67, 54)) },        // Red
        { DeviationStatus.Critical, new SolidColorBrush(Color.FromRgb(183, 28, 28)) },    // Dark Red
        { DeviationStatus.Unknown, new SolidColorBrush(Color.FromRgb(158, 158, 158)) }    // Gray
    };

    static DeviationStatusToColorConverter()
    {
        foreach (var brush in StatusColors.Values)
        {
            brush.Freeze();
        }
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DeviationStatus status && StatusColors.TryGetValue(status, out var brush))
        {
            return brush;
        }

        return StatusColors[DeviationStatus.Unknown];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер булевого значения в видимость
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Конвертер инвертированного булевого значения в видимость
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return true;
    }
}

/// <summary>
/// Конвертер числа в форматированную строку
/// </summary>
public class NumberToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return "0";

        var format = parameter?.ToString() ?? "N0";
        
        return value switch
        {
            int intVal => intVal.ToString(format),
            decimal decVal => decVal.ToString(format), 
            double doubleVal => doubleVal.ToString(format),
            float floatVal => floatVal.ToString(format),
            _ => value.ToString() ?? "0"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value?.ToString() is string str && decimal.TryParse(str, out var result))
        {
            return result;
        }
        return 0;
    }
}

/// <summary>
/// Конвертер нулевого значения в видимость
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString()?.ToLower() == "invert";
        var isNull = value == null;
        
        if (invert)
        {
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }
        
        return isNull ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер коллекции в количество элементов
/// </summary>
public class CollectionCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is System.Collections.ICollection collection)
        {
            return collection.Count;
        }
        
        if (value is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object>().Count();
        }

        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}