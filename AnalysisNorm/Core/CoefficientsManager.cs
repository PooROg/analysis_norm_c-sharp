#nullable disable  // Фикс CS8978: отключаем nullable для этого файла (ClosedXML legacy)


// Core/CoefficientsManager.cs
// Миграция из: core/coefficients.py
// Менеджер коэффициентов расхода локомотивов из Excel файлов

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Serilog;

namespace AnalysisNorm.Core
{
    /// <summary>
    /// Запись о локомотиве с коэффициентом
    /// Python: dict с полями series, series_normalized, number, coefficient, etc.
    /// </summary>
    public class LocomotiveRecord
    {
        public string Series { get; set; }              // Исходная серия (например, "ВЛ80")
        public string SeriesNormalized { get; set; }    // Нормализованная серия (например, "ВЛ80")
        public int Number { get; set; }                 // Заводской номер
        public double Coefficient { get; set; }         // Коэффициент расхода
        public double DeviationPercent { get; set; }    // Отклонение от нормы в %
        public double WorkTotal { get; set; }           // Общая работа локомотива

        public LocomotiveRecord()
        {
            Series = "";
            SeriesNormalized = "";
            Coefficient = 1.0;
        }
    }

    /// <summary>
    /// Статистика по коэффициентам
    /// Python: dict из get_statistics()
    /// </summary>
    public class CoefficientsStatistics
    {
        public int TotalLocomotives { get; set; }
        public int SeriesCount { get; set; }
        public double AvgCoefficient { get; set; }
        public double MinCoefficient { get; set; }
        public double MaxCoefficient { get; set; }
        public double AvgDeviationPercent { get; set; }
        public int LocomotivesAboveNorm { get; set; }
        public int LocomotivesBelowNorm { get; set; }
        public int LocomotivesAtNorm { get; set; }
    }

    /// <summary>
    /// Менеджер коэффициентов расхода локомотивов
    /// Python класс: LocomotiveCoefficientsManager (coefficients.py)
    /// 
    /// Загружает коэффициенты из Excel файла (все листы = серии локомотивов)
    /// Нормализует названия серий, фильтрует по минимальной работе
    /// </summary>
    public class CoefficientsManager
    {
        private string _file;
        
        // Ключ: (нормализованная серия, номер), значение: коэффициент
        private Dictionary<(string series, int number), double> _coefficients;
        
        // Данные по сериям: ключ — нормализованная серия, значение — список записей
        private Dictionary<string, List<LocomotiveRecord>> _dataBySeries;

        public CoefficientsManager()
        {
            _file = null;
            _coefficients = new Dictionary<(string, int), double>();
            _dataBySeries = new Dictionary<string, List<LocomotiveRecord>>();
        }

        #region Нормализация серий
        // Python: normalize_series()

        /// <summary>
        /// Нормализация названия серии локомотива
        /// Убираем все кроме букв/цифр, приводим к верхнему регистру
        /// Python: normalize_series()
        /// </summary>
        public static string NormalizeSeries(string series)
        {
            if (string.IsNullOrWhiteSpace(series))
                return "";

            // Удаляем все символы кроме букв (русских/английских) и цифр
            var normalized = Regex.Replace(series.ToUpper(), @"[^А-ЯA-ZА-ЯA-Z0-9]", "");
            return normalized;
        }

        #endregion

        #region Загрузка коэффициентов из Excel
        // Python: load_coefficients()

        /// <summary>
        /// Загрузка коэффициентов из Excel файла (все листы)
        /// Python: load_coefficients()
        /// 
        /// Каждый лист Excel = одна серия локомотивов
        /// Ищет колонки: "Заводской номер", "Коэффициент" или "Процент", "Работа"
        /// </summary>
        /// <param name="filePath">Путь к Excel файлу</param>
        /// <param name="minWorkThreshold">Минимальная работа для фильтрации (по умолчанию 0)</param>
        /// <returns>True если загрузка успешна</returns>
        public bool LoadCoefficients(string filePath, double minWorkThreshold = 0.0)
        {
            _file = filePath;
            _coefficients.Clear();
            _dataBySeries.Clear();

            try
            {
                using var workbook = new XLWorkbook(filePath);
                int totalProcessed = 0;

                foreach (var worksheet in workbook.Worksheets)
                {
                    string sheetName = worksheet.Name.Trim();
                    
                    // Извлекаем серию из имени листа: буквы + цифры (+буквы)
                    // Пример: "ВЛ80", "2ЭС6", "ЭП1М"
                    var match = Regex.Match(sheetName, @"[А-ЯA-Z]+[\d]+[А-ЯA-Z]*");
                    string seriesName = match.Success ? match.Value : sheetName;
                    string seriesNormalized = NormalizeSeries(seriesName);

                    try
                    {
                        int processed = ProcessWorksheet(worksheet, seriesName, seriesNormalized, minWorkThreshold);
                        totalProcessed += processed;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Лист '{sheetName}': ошибка обработки: {ex.Message}");
                    }
                }

                Log.Information($"Загружено всего {totalProcessed} локомотивов из {_dataBySeries.Count} серий");
                return totalProcessed > 0;
            }
            catch (Exception ex)
            {
                Log.Error($"Не удалось открыть Excel: {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Обработка одного листа Excel (одной серии)
        /// Python: внутренняя логика в load_coefficients()
        /// </summary>
        private int ProcessWorksheet(IXLWorksheet worksheet, string seriesName, string seriesNormalized, double minWorkThreshold)
        {
            // Ищем строку заголовков среди первых 10 строк
            int headerRow = FindHeaderRow(worksheet);
            if (headerRow == 0)
            {
                Log.Information($"Лист '{worksheet.Name}': не найдена строка заголовков — пропуск");
                return 0;
            }

            // Определяем индексы нужных колонок
            var columnIndices = FindColumnIndices(worksheet, headerRow);
            if (!columnIndices.HasValue)
            {
                Log.Information($"Лист '{worksheet.Name}': не найдены необходимые колонки — пропуск");
                return 0;
            }

            var (locomotiveCol, coefficientCol, percentCol, workCol, isPercentSource) = columnIndices.Value;

            // Парсим строки данных (ФИКС строка ~179: вынесли в переменную)
            IXLRow lastRowUsed = worksheet.LastRowUsed();  // Явный вызов
            int lastRowNumber = lastRowUsed != null ? lastRowUsed.RowNumber() : 0;
            int count = 0;
            var records = new List<LocomotiveRecord>();

            for (int row = headerRow + 1; row <= lastRowNumber; row++)
            {
                try
                {
                    var record = ParseRow(
                        worksheet, row,
                        locomotiveCol, coefficientCol, percentCol, workCol,
                        isPercentSource, seriesName, seriesNormalized,
                        minWorkThreshold
                    );

                    if (record != null)
                    {
                        records.Add(record);

                        // Сохраняем коэффициент (последний побеждает)
                        var key = (seriesNormalized, record.Number);
                        _coefficients[key] = record.Coefficient;
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug($"Лист '{worksheet.Name}', строка {row}: ошибка парсинга: {ex.Message}");
                }
            }

            if (records.Count > 0)
            {
                if (!_dataBySeries.ContainsKey(seriesNormalized))
                    _dataBySeries[seriesNormalized] = new List<LocomotiveRecord>();

                _dataBySeries[seriesNormalized].AddRange(records);

                string source = isPercentSource ? "percent" : "coefficient";
                Log.Debug($"Лист '{worksheet.Name}': загружено {records.Count} локомотивов " +
                         $"(серия {seriesName}, источник={source})");
            }

            return count;
        }

        /// <summary>
        /// Находит строку с заголовками (содержит "завод" и "номер")
        /// Python: сканирование первых 10 строк
        /// </summary>
        private int FindHeaderRow(IXLWorksheet worksheet)
        {
            // ФИКС строка ~227: вынесли в переменную + явная проверка null
            IXLRow lastRowUsed = worksheet.LastRowUsed();
            int lastRowNumber = lastRowUsed != null ? lastRowUsed.RowNumber() : 0;
            int maxScan = Math.Min(10, lastRowNumber);
            
            for (int row = 1; row <= maxScan; row++)
            {
                bool foundZavod = false;
                bool foundNomer = false;

                // ФИКС строка ~234: вынесли в переменную + явная проверка null
                IXLColumn lastColUsed = worksheet.LastColumnUsed();
                int lastColNumber = lastColUsed != null ? lastColUsed.ColumnNumber() : 0;
                for (int col = 1; col <= lastColNumber; col++)
                {
                    string cellValue = worksheet.Cell(row, col).GetString().ToLower();
                    
                    if (cellValue.Contains("завод") && cellValue.Contains("номер"))
                    {
                        return row;
                    }
                    if (cellValue.Contains("завод")) foundZavod = true;
                    if (cellValue.Contains("номер")) foundNomer = true;
                }

                if (foundZavod && foundNomer)
                    return row;
            }

            return 0;
        }

        /// <summary>
        /// Находит индексы нужных колонок
        /// Python: поиск locomotive_col, coefficient_col, percent_col, work_col
        /// </summary>
        private (int locomotive, int? coefficient, int? percent, int? work, bool isPercent)? FindColumnIndices(
            IXLWorksheet worksheet, int headerRow)
        {
            var headers = new Dictionary<int, string>();
            
            // ФИКС ~строка 200: вынесли в переменную + явная проверка null
            IXLRow row = worksheet.Row(headerRow);
            IXLCell lastCellUsed = row.LastCellUsed();
            int lastCol = 0;
            if (lastCellUsed != null && lastCellUsed.Address != null)
            {
                lastCol = lastCellUsed.Address.ColumnNumber;
            }

            for (int col = 1; col <= lastCol; col++)
            {
                string header = worksheet.Cell(headerRow, col).GetString().ToLower().Trim();
                headers[col] = header;
            }

            // Ищем колонку "Заводской номер локомотива"
            int locomotiveCol = headers
                .FirstOrDefault(kv => kv.Value.Contains("завод") && kv.Value.Contains("номер"))
                .Key;

            if (locomotiveCol == 0)
                return null;

            // Ищем колонку "Коэффициент" (приоритет)
            int coefficientColTemp = headers
                .FirstOrDefault(kv => kv.Value.Contains("коэффициент") || kv.Value.Contains("коэфф"))
                .Key;
            int? coefficientCol = coefficientColTemp == 0 ? null : (int?)coefficientColTemp;

            // Ищем колонку "Процент" или "%"
            int? percentCol = headers
                .FirstOrDefault(kv => kv.Value.Contains("процент") || kv.Value.Contains("%"))
                .Key;

            if (percentCol == 0) percentCol = null;

            // Ищем колонку "Работа"
            int? workCol = headers
                .FirstOrDefault(kv => kv.Value.Contains("работа"))
                .Key;

            if (workCol == 0) workCol = null;

            // Должна быть хотя бы одна: коэффициент или процент
            if (coefficientCol == null && percentCol == null)
                return null;

            bool isPercentSource = coefficientCol == null;
            return (locomotiveCol, coefficientCol, percentCol, workCol, isPercentSource);
        }

        /// <summary>
        /// Парсит одну строку данных
        /// Python: обработка каждой строки в DataFrame
        /// </summary>
        private LocomotiveRecord ParseRow(
            IXLWorksheet worksheet, int row,
            int locomotiveCol, int? coefficientCol, int? percentCol, int? workCol,
            bool isPercentSource, string seriesName, string seriesNormalized,
            double minWorkThreshold)
        {
            // Парсим номер локомотива
            string numberStr = worksheet.Cell(row, locomotiveCol).GetString().Trim();
            if (string.IsNullOrWhiteSpace(numberStr))
                return null;

            if (!int.TryParse(numberStr, out int number) || number <= 0)
                return null;

            // Парсим коэффициент
            double coefficient;
            
            if (isPercentSource && percentCol.HasValue)
            {
                // Источник: колонка "Процент"
                string percentStr = worksheet.Cell(row, percentCol.Value).GetString()
                    .Replace("%", "").Replace(",", ".").Trim();
                
                if (!double.TryParse(percentStr, out double percent))
                    return null;

                // Если значение > 10, считаем что это проценты (например, 105%), делим на 100
                coefficient = percent > 10 ? percent / 100.0 : percent;
            }
            else if (coefficientCol.HasValue)
            {
                // Источник: колонка "Коэффициент"
                string coeffStr = worksheet.Cell(row, coefficientCol.Value).GetString()
                    .Replace(",", ".").Trim();
                
                if (!double.TryParse(coeffStr, out coefficient))
                    return null;
            }
            else
            {
                return null;
            }

            // Парсим работу (опционально)
            double workTotal = 0.0;
            if (workCol.HasValue)
            {
                string workStr = worksheet.Cell(row, workCol.Value).GetString().Trim();
                double.TryParse(workStr.Replace(",", "."), out workTotal);
            }

            // Фильтрация по минимальной работе
            if (minWorkThreshold > 0 && workTotal < minWorkThreshold)
                return null;

            return new LocomotiveRecord
            {
                Series = seriesName,
                SeriesNormalized = seriesNormalized,
                Number = number,
                Coefficient = coefficient,
                DeviationPercent = (coefficient - 1.0) * 100.0,
                WorkTotal = workTotal
            };
        }

        #endregion

        #region Получение коэффициентов
        // Python: get_coefficient(), get_locomotives_by_series()

        /// <summary>
        /// Возвращает коэффициент для локомотива
        /// Python: get_coefficient()
        /// </summary>
        /// <returns>Коэффициент или 1.0 если не найден</returns>
        public double GetCoefficient(string series, int number)
        {
            var key = (NormalizeSeries(series), number);
            return _coefficients.TryGetValue(key, out double coef) ? coef : 1.0;
        }

        /// <summary>
        /// Возвращает список записей по серии
        /// Python: get_locomotives_by_series()
        /// </summary>
        public List<LocomotiveRecord> GetLocomotivesBySeries(string series)
        {
            string normalized = NormalizeSeries(series);
            return _dataBySeries.TryGetValue(normalized, out var records) 
                ? new List<LocomotiveRecord>(records) 
                : new List<LocomotiveRecord>();
        }

        /// <summary>
        /// Возвращает все серии
        /// </summary>
        public List<string> GetAllSeries()
        {
            return _dataBySeries.Keys.OrderBy(s => s).ToList();
        }

        #endregion

        #region Статистика
        // Python: get_statistics()

        /// <summary>
        /// Сводная статистика по коэффициентам
        /// Python: get_statistics()
        /// </summary>
        public CoefficientsStatistics GetStatistics()
        {
            if (_coefficients.Count == 0)
                return null;

            var coefficients = _coefficients.Values.ToList();

            return new CoefficientsStatistics
            {
                TotalLocomotives = coefficients.Count,
                SeriesCount = _dataBySeries.Count,
                AvgCoefficient = coefficients.Average(),
                MinCoefficient = coefficients.Min(),
                MaxCoefficient = coefficients.Max(),
                AvgDeviationPercent = coefficients.Average(c => (c - 1.0) * 100.0),
                LocomotivesAboveNorm = coefficients.Count(c => c > 1.0),
                LocomotivesBelowNorm = coefficients.Count(c => c < 1.0),
                LocomotivesAtNorm = coefficients.Count(c => Math.Abs(c - 1.0) < 1e-3)
            };
        }

        #endregion
    }
}