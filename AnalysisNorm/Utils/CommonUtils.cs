using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AnalysisNorm.Utils
{
    #region TextUtils - Утилиты работы с текстом

    /// <summary>
    /// Утилиты для работы с текстом и числами
    /// Мигрировано из: core/utils.py (lines 20-115)
    /// </summary>
    public static class TextUtils
    {
        /// <summary>
        /// Нормализация текста: удаление лишних пробелов, неразрывных пробелов
        /// Python: normalize_text(), line 20
        /// </summary>
        /// <param name="text">Исходный текст</param>
        /// <returns>Очищенный текст</returns>
        public static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Python: text = text.replace('\xa0', ' ').replace('&nbsp;', ' ')
            text = text.Replace('\u00A0', ' ').Replace("&nbsp;", " ");

            // Python: text = re.sub(r'\s+', ' ', text)
            text = Regex.Replace(text, @"\s+", " ");

            // Python: return text.strip()
            return text.Trim();
        }

        /// <summary>
        /// Безопасное преобразование к float с обработкой различных входных типов
        /// Python: safe_float(), line 28
        /// </summary>
        public static double SafeFloat(object value, double defaultValue = 0.0)
        {
            // Python: if value is None or (isinstance(value, float) and pd.isna(value)):
            if (value == null || value == DBNull.Value)
                return defaultValue;

            // Python: if isinstance(value, (int, float)):
            if (value is double d)
                return d;
            if (value is int i)
                return i;
            if (value is float f)
                return f;

            // Python: if isinstance(value, str):
            if (value is string str)
            {
                // Python: cleaned = value.strip().replace(' ', '').replace('\xa0', '')
                string cleaned = str.Trim().Replace(" ", "").Replace("\u00A0", "");

                // Python: if cleaned.endswith('.'): cleaned = cleaned[:-1]
                if (cleaned.EndsWith("."))
                    cleaned = cleaned.Substring(0, cleaned.Length - 1);

                // Python: cleaned = cleaned.replace(',', '.')
                cleaned = cleaned.Replace(',', '.');

                // Python: if not cleaned or cleaned.lower() in ('nan', 'none', '-', 'n/a'):
                if (string.IsNullOrWhiteSpace(cleaned) ||
                    cleaned.Equals("nan", StringComparison.OrdinalIgnoreCase) ||
                    cleaned.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                    cleaned == "-" ||
                    cleaned.Equals("n/a", StringComparison.OrdinalIgnoreCase))
                {
                    return defaultValue;
                }

                // Python: return float(cleaned)
                if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                    return result;

                return defaultValue;
            }

            // Python: return float(value)
            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Безопасное преобразование к int
        /// Python: safe_int(), line 65
        /// </summary>
        public static int SafeInt(object value, int defaultValue = 0)
        {
            // Python: float_val = safe_float(value, float(default))
            double floatVal = SafeFloat(value, defaultValue);

            // Python: return int(float_val) if float_val == int(float_val) else default
            if (Math.Abs(floatVal - Math.Round(floatVal)) < 0.0001)
                return (int)Math.Round(floatVal);

            return defaultValue;
        }

        /// <summary>
        /// Безопасное деление с проверкой на ноль
        /// Python: safe_divide(), line 85
        /// </summary>
        public static double SafeDivide(object numerator, object denominator, double defaultValue = 0.0)
        {
            // Python: num = safe_float(numerator)
            double num = SafeFloat(numerator);

            // Python: den = safe_float(denominator)
            double den = SafeFloat(denominator);

            // Python: if den == 0: return default
            if (Math.Abs(den) < double.Epsilon)
                return defaultValue;

            // Python: return abs(num / den)
            return Math.Abs(num / den);
        }

        /// <summary>
        /// Безопасное форматирование числа
        /// Python: format_number(), line 100
        /// </summary>
        public static string FormatNumber(object value, int decimals = 1, string fallback = "N/A")
        {
            try
            {
                // Python: num = safe_float(value)
                double num = SafeFloat(value);

                // Python: if num == 0 and value in (None, "", "N/A"): return fallback
                if (Math.Abs(num) < double.Epsilon &&
                    (value == null || value.ToString() == "" || value.ToString() == "N/A"))
                {
                    return fallback;
                }

                // Python: return f"{num:.{decimals}f}"
                return num.ToString($"F{decimals}", CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }
    }

    #endregion

    #region StatusClassifier - Классификатор статусов

    /// <summary>
    /// Классификатор статусов по отклонениям от нормы
    /// Мигрировано из: core/utils.py, class StatusClassifier (lines 120-175)
    /// </summary>
    public static class StatusClassifier
    {
        #region Пороги отклонений (в процентах)

        // Python: THRESHOLDS = { 'strong_economy': -30, ... }
        public const double StrongEconomy = -30;
        public const double MediumEconomy = -20;
        public const double WeakEconomy = -5;
        public const double NormalUpper = 5;
        public const double WeakOverrun = 20;
        public const double MediumOverrun = 30;

        #endregion

        /// <summary>
        /// Определяет статус по отклонению в процентах
        /// Python: get_status(), line 125
        /// </summary>
        /// <param name="deviation">Отклонение в процентах</param>
        /// <returns>Статус отклонения</returns>
        public static string GetStatus(double deviation)
        {
            // Python: match deviation:
            //     case d if d < cls.THRESHOLDS['strong_economy']:
            //         return "Экономия сильная"
            //     ...

            if (deviation < StrongEconomy)
                return "Экономия сильная";

            if (deviation < MediumEconomy)
                return "Экономия средняя";

            if (deviation < WeakEconomy)
                return "Экономия слабая";

            if (deviation <= NormalUpper)
                return "Норма";

            if (deviation <= WeakOverrun)
                return "Перерасход слабый";

            if (deviation <= MediumOverrun)
                return "Перерасход средний";

            return "Перерасход сильный";
        }

        /// <summary>
        /// Возвращает цвет для статуса (для UI)
        /// Python: get_status_color(), line 150
        /// </summary>
        public static string GetStatusColor(string status)
        {
            // Python: color_map = { "Экономия сильная": "darkgreen", ... }
            return status switch
            {
                "Экономия сильная" => "DarkGreen",
                "Экономия средняя" => "Green",
                "Экономия слабая" => "LightGreen",
                "Норма" => "Blue",
                "Перерасход слабый" => "Orange",
                "Перерасход средний" => "DarkOrange",
                "Перерасход сильный" => "Red",
                _ => "Gray"
            };
        }
    }

    #endregion
}