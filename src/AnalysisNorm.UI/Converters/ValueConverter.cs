using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AnalysisNorm.UI.Converters;

/// <summary>
/// Конвертер Boolean в Visibility - стандартный WPF конвертер
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
            return visibility == Visibility.Visible;
        
        return false;
    }
}

/// <summary>
/// Конвертер для инвертирования Boolean значения
/// </summary>
public class InvertBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        
        return false;
    }
}

/// <summary>
/// Конвертер для форматирования чисел с культурно-специфичным форматированием
/// Аналог Python format_number utility
/// </summary>
public class NumberFormattingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var format = parameter as string ?? "F1";
        
        return value switch
        {
            double doubleValue => doubleValue.ToString(format, CultureInfo.CurrentCulture),
            float floatValue => floatValue.ToString(format, CultureInfo.CurrentCulture),
            decimal decimalValue => decimalValue.ToString(format, CultureInfo.CurrentCulture),
            int intValue when format.Contains('F') => intValue.ToString("F0", CultureInfo.CurrentCulture),
            int intValue => intValue.ToString("N0", CultureInfo.CurrentCulture),
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue && double.TryParse(stringValue, out var result))
            return result;
        
        return 0.0;
    }
}

/// <summary>
/// Конвертер для форматирования процентных отклонений со знаком
/// Аналог Python deviation formatting
/// </summary>
public class DeviationPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double deviation)
        {
            var sign = deviation >= 0 ? "+" : "";
            return $"{sign}{deviation:F1}%";
        }
        
        return "0.0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue)
        {
            var cleanValue = stringValue.Replace("%", "").Replace("+", "");
            if (double.TryParse(cleanValue, out var result))
                return result;
        }
        
        return 0.0;
    }
}