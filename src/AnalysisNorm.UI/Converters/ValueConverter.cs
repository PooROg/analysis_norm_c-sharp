// === AnalysisNorm.UI/Converters/ValueConverters.cs ===
using System.Globalization;
using System.Windows.Data;

namespace AnalysisNorm.UI.Converters;

/// <summary>
/// Инвертирует boolean значение
/// Используется в XAML для инверсии IsEnabled/Visibility и других свойств
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public class InvertBooleanConverter : IValueConverter
{
    /// <summary>
    /// Инвертирует boolean значение: true -> false, false -> true
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        
        return false; // Default для non-boolean значений
    }

    /// <summary>
    /// Обратное преобразование для двустороннего binding
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        
        return false;
    }
}

/// <summary>
/// Конвертирует null или пустую строку в Visibility
/// null/empty -> Collapsed, непустая строка -> Visible
/// </summary>
[ValueConversion(typeof(string), typeof(System.Windows.Visibility))]
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        var invert = parameter?.ToString()?.ToLower() == "invert";
        
        var isVisible = !string.IsNullOrEmpty(str);
        if (invert) isVisible = !isVisible;
        
        return isVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертирует числовые значения в строки с форматированием
/// Полезно для отображения decimal/double значений в UI
/// </summary>
[ValueConversion(typeof(decimal), typeof(string))]
public class DecimalToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal decValue)
        {
            var format = parameter?.ToString() ?? "F2";
            return decValue.ToString(format, CultureInfo.CurrentCulture);
        }
        
        if (value is double doubleValue)
        {
            var format = parameter?.ToString() ?? "F2";
            return doubleValue.ToString(format, CultureInfo.CurrentCulture);
        }
        
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && decimal.TryParse(str, out decimal result))
        {
            return result;
        }
        
        return 0m;
    }
}

/// <summary>
/// Конвертирует процентные значения для отображения в прогресс-барах
/// Принимает значения 0-100 и нормализует для WPF (0-1)
/// </summary>
[ValueConversion(typeof(double), typeof(double))]
public class PercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            // Если значение больше 1, считаем что это проценты (0-100)
            return doubleValue > 1 ? doubleValue / 100.0 : doubleValue;
        }
        
        if (value is decimal decValue)
        {
            var doubleVal = (double)decValue;
            return doubleVal > 1 ? doubleVal / 100.0 : doubleVal;
        }
        
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return doubleValue * 100;
        }
        
        return 0.0;
    }
}