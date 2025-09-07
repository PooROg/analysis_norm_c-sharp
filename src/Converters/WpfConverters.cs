// Converters/WpfConverters.cs
using System.Globalization;
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
        if (value == null) return "⏳";

        return value.ToString()?.ToLower() switch
        {
            "completed" => "✅",
            "active" => "🔄",
            "error" => "❌",
            "pending" => "⏳",
            "warning" => "⚠️",
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
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return Brushes.Gray;

        var colorBrush = value.ToString()?.ToLower() switch
        {
            "completed" => new SolidColorBrush(Colors.Green),
            "active" => new SolidColorBrush(Colors.Blue),
            "error" => new SolidColorBrush(Colors.Red),
            "pending" => new SolidColorBrush(Colors.Gray),
            "warning" => new SolidColorBrush(Colors.Orange),
            _ => new SolidColorBrush(Colors.Gray)
        };

        colorBrush.Freeze(); // Оптимизация производительности
        return colorBrush;
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

        return "ℹ️";
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
    private static readonly SolidColorBrush InfoBrush = new(Colors.LightBlue);
    private static readonly SolidColorBrush WarningBrush = new(Colors.Orange);
    private static readonly SolidColorBrush ErrorBrush = new(Colors.Red);
    private static readonly SolidColorBrush CriticalBrush = new(Colors.DarkRed);
    private static readonly SolidColorBrush DefaultBrush = new(Colors.Gray);

    static DiagnosticAlertLevelToColorConverter()
    {
        // Замораживаем кисти для оптимизации
        InfoBrush.Freeze();
        WarningBrush.Freeze();
        ErrorBrush.Freeze();
        CriticalBrush.Freeze();
        DefaultBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AlertLevel level)
        {
            return level switch
            {
                AlertLevel.Info => InfoBrush,
                AlertLevel.Warning => WarningBrush,
                AlertLevel.Error => ErrorBrush,
                AlertLevel.Critical => CriticalBrush,
                _ => DefaultBrush
            };
        }

        return DefaultBrush;
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
    private static readonly SolidColorBrush ExcellentBrush = new(Color.FromRgb(46, 125, 50));   // DarkGreen
    private static readonly SolidColorBrush GoodBrush = new(Color.FromRgb(76, 175, 80));        // Green
    private static readonly SolidColorBrush AcceptableBrush = new(Color.FromRgb(255, 152, 0));  // Orange
    private static readonly SolidColorBrush PoorBrush = new(Color.FromRgb(244, 67, 54));        // Red
    private static readonly SolidColorBrush CriticalBrush = new(Color.FromRgb(183, 28, 28));    // DarkRed
    private static readonly SolidColorBrush UnknownBrush = new(Color.FromRgb(158, 158, 158));   // Gray

    static DeviationStatusToColorConverter()
    {
        ExcellentBrush.Freeze();
        GoodBrush.Freeze();
        AcceptableBrush.Freeze();
        PoorBrush.Freeze();
        CriticalBrush.Freeze();
        UnknownBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DeviationStatus status)
        {
            return status switch
            {
                DeviationStatus.Excellent => ExcellentBrush,
                DeviationStatus.Good => GoodBrush,
                DeviationStatus.Acceptable => AcceptableBrush,
                DeviationStatus.Poor => PoorBrush,
                DeviationStatus.Critical => CriticalBrush,
                _ => UnknownBrush
            };
        }

        return UnknownBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер булевого значения в видимость (True = Visible, False = Collapsed)
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is System.Windows.Visibility visibility)
        {
            return visibility == System.Windows.Visibility.Visible;
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
            return !boolValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is System.Windows.Visibility visibility)
        {
            return visibility != System.Windows.Visibility.Visible;
        }
        return true;
    }
}