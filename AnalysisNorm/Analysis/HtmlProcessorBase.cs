// Analysis/HtmlProcessorBase.cs
// Базовые утилиты для HTML парсинга
// Мигрировано из: analysis/html_route_processor.py и html_norm_processor.py (общие методы)
// ЧАТ 3

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Serilog;

namespace AnalysisNorm.Analysis
{
    /// <summary>
    /// Базовый класс для обработки HTML файлов с маршрутами и нормами
    /// Содержит общие утилиты для парсинга HTML
    /// 
    /// Python: методы из html_route_processor.py и html_norm_processor.py
    /// </summary>
    public abstract class HtmlProcessorBase
    {
        #region Константы

        /// <summary>
        /// Кодировка файлов HTML (обычно Windows-1251 для РЖД)
        /// Python: encoding = 'windows-1251'
        /// </summary>
        protected const string HtmlEncoding = "windows-1251";

        #endregion

        #region Чтение HTML файлов

        /// <summary>
        /// Читает HTML файл с правильной кодировкой
        /// Python: def read_html_file(file_path), html_route_processor.py line 85
        /// </summary>
        /// <param name="filePath">Путь к HTML файлу</param>
        /// <returns>Содержимое файла в виде строки</returns>
        protected string ReadHtmlFile(string filePath)
        {
            try
            {
                Log.Debug("Чтение HTML файла: {File}", Path.GetFileName(filePath));

                // Python: with open(file_path, 'r', encoding='windows-1251') as f:
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var encoding = Encoding.GetEncoding(HtmlEncoding);

                string content = File.ReadAllText(filePath, encoding);

                Log.Debug("Файл прочитан, размер: {Size} байт", content.Length);
                return content;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка чтения HTML файла: {File}", filePath);
                throw;
            }
        }

        #endregion

        #region Нормализация текста

        /// <summary>
        /// Нормализует текст: удаляет лишние пробелы, переводы строк
        /// Python: def clean_text(text), html_route_processor.py line 98
        /// </summary>
        /// <param name="text">Исходный текст</param>
        /// <returns>Очищенный текст</returns>
        protected string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Python: text = text.strip()
            text = text.Trim();

            // Python: text = re.sub(r'\s+', ' ', text)
            text = Regex.Replace(text, @"\s+", " ");

            // Python: text = text.replace('\xa0', ' ')
            text = text.Replace('\u00A0', ' '); // Неразрывный пробел

            return text.Trim();
        }

        /// <summary>
        /// Удаляет все пробелы из строки
        /// </summary>
        protected string RemoveAllSpaces(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return Regex.Replace(text, @"\s+", "");
        }

        #endregion

        #region Преобразование типов

        /// <summary>
        /// Безопасное преобразование строки в число (int или double)
        /// Python: def try_convert_to_number(text), html_route_processor.py line 105
        /// </summary>
        /// <param name="text">Текст для преобразования</param>
        /// <returns>Число (int или double) или null</returns>
        protected object? TryConvertToNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Очищаем текст
            text = NormalizeText(text);

            // Удаляем запятые в больших числах: "1,234" → "1234"
            // Python: text = text.replace(',', '')
            text = text.Replace(",", "");

            // Заменяем запятую на точку для дробей: "12,5" → "12.5"
            text = text.Replace(".", ","); // Временно
            text = text.Replace(",", ".");

            // Пробуем int
            if (int.TryParse(text, out int intValue))
                return intValue;

            // Пробуем double
            if (double.TryParse(text, 
                System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, 
                out double doubleValue))
                return doubleValue;

            return null;
        }

        /// <summary>
        /// Безопасное преобразование в int
        /// </summary>
        protected int? TryConvertToInt(string text)
        {
            var result = TryConvertToNumber(text);
            if (result == null)
                return null;

            if (result is int i)
                return i;

            if (result is double d)
                return (int)Math.Round(d);

            return null;
        }

        /// <summary>
        /// Безопасное преобразование в double
        /// </summary>
        protected double? TryConvertToDouble(string text)
        {
            var result = TryConvertToNumber(text);
            if (result == null)
                return null;

            if (result is int i)
                return (double)i;

            if (result is double d)
                return d;

            return null;
        }

        #endregion

        #region Работа с HtmlAgilityPack

        /// <summary>
        /// Загружает HTML документ через HtmlAgilityPack
        /// </summary>
        /// <param name="htmlContent">HTML контент</param>
        /// <returns>HtmlDocument</returns>
        protected HtmlDocument LoadHtmlDocument(string htmlContent)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
            return doc;
        }

        /// <summary>
        /// Извлекает текст из HTML узла с нормализацией
        /// </summary>
        protected string GetNormalizedText(HtmlNode node)
        {
            if (node == null)
                return string.Empty;

            return NormalizeText(node.InnerText);
        }

        /// <summary>
        /// Безопасное получение текста из ячейки таблицы по индексу
        /// </summary>
        protected string GetCellText(HtmlNode row, int cellIndex)
        {
            var cells = row.SelectNodes(".//td");
            if (cells == null || cellIndex >= cells.Count)
                return string.Empty;

            return GetNormalizedText(cells[cellIndex]);
        }

        /// <summary>
        /// Безопасное получение числа из ячейки таблицы
        /// </summary>
        protected double? GetCellNumber(HtmlNode row, int cellIndex)
        {
            var text = GetCellText(row, cellIndex);
            return TryConvertToDouble(text);
        }

        #endregion

        #region Очистка HTML (для стабильного парсинга)

        /// <summary>
        /// Очищает HTML-код от лишних элементов для стабильного парсинга
        /// Python: def _clean_html_content(content), html_route_processor.py line 175
        /// </summary>
        /// <param name="content">HTML контент</param>
        /// <returns>Очищенный HTML</returns>
        protected string CleanHtmlContent(string content)
        {
            Log.Debug("Очищаем HTML код от лишних элементов...");
            int originalSize = content.Length;

            // Python: удаляем различные служебные элементы
            // re.sub(r'<font class = rcp12 ><center>Дата получения:.*?</font>\s*<br>', '', content, flags=re.DOTALL)
            content = Regex.Replace(content, 
                @"<font class = rcp12 ><center>Дата получения:.*?</font>\s*<br>", 
                "", 
                RegexOptions.Singleline);

            content = Regex.Replace(content, 
                @"<font class = rcp12 ><center>Номер маршрута:.*?</font><br>", 
                "", 
                RegexOptions.Singleline);

            content = Regex.Replace(content, 
                @"<tr class=tr_numline>.*?</tr>", 
                "", 
                RegexOptions.Singleline);

            // Удаляем атрибуты выравнивания (они не нужны для парсинга)
            content = Regex.Replace(content, @"\s+ALIGN=center", "", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @"\s+align=left", "", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @"\s+align=right", "", RegexOptions.IgnoreCase);

            // Удаляем теги <center>, <pre>
            content = content.Replace("<center>", "");
            content = content.Replace("</center>", "");
            content = content.Replace("<pre>", "");
            content = content.Replace("</pre>", "");

            // Схлопываем пробелы между тегами: ">   <" → "><"
            content = Regex.Replace(content, @">[ \t]+<", "><");

            int removedBytes = originalSize - content.Length;
            Log.Debug("Удалено {Bytes} байт лишнего кода ({Percent:F1}%)", 
                removedBytes, 
                removedBytes / (double)Math.Max(originalSize, 1) * 100);

            return content;
        }

        #endregion

        #region Безопасные математические операции

        /// <summary>
        /// Безопасное вычитание (игнорирует null значения)
        /// Python: def safe_subtract(...), html_route_processor.py line 452
        /// </summary>
        protected double? SafeSubtract(double? value, params double?[] toSubtract)
        {
            if (value == null)
                return null;

            double result = value.Value;

            foreach (var v in toSubtract)
            {
                if (v.HasValue)
                    result -= v.Value;
            }

            return result;
        }

        /// <summary>
        /// Безопасное деление (возвращает null если делитель 0)
        /// Python: def safe_divide(a, b), html_route_processor.py line 460
        /// </summary>
        protected double? SafeDivide(double? numerator, double? denominator)
        {
            if (numerator == null || denominator == null || Math.Abs(denominator.Value) < 0.0001)
                return null;

            return numerator.Value / denominator.Value;
        }

        #endregion

        #region Логирование прогресса

        /// <summary>
        /// Логирует прогресс обработки файлов
        /// </summary>
        protected void LogProgress(int current, int total, string message)
        {
            if (current % Math.Max(1, total / 10) == 0 || current == total)
            {
                double percent = (current / (double)total) * 100;
                Log.Information("{Message}: {Current}/{Total} ({Percent:F1}%)", 
                    message, current, total, percent);
            }
        }

        #endregion
    }
}
