// Analysis/NormProcessor.cs
// Обработчик HTML файлов с нормами
// Мигрировано из: analysis/html_norm_processor.py
// ЧАТ 3: Основная функциональность

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Serilog;
using AnalysisNorm.Core;

namespace AnalysisNorm.Analysis
{
    /// <summary>
    /// Процессор для парсинга HTML файлов с нормами
    /// Python: class HTMLNormProcessor, html_norm_processor.py
    /// </summary>
    public class NormProcessor : HtmlProcessorBase
    {
        #region Поля

        private readonly NormStorage _normStorage;

        #endregion

        #region Конструктор

        public NormProcessor(NormStorage normStorage)
        {
            _normStorage = normStorage ?? throw new ArgumentNullException(nameof(normStorage));
            Log.Information("NormProcessor инициализирован");
        }

        #endregion

        #region Главный метод обработки

        /// <summary>
        /// Обрабатывает список HTML файлов с нормами
        /// Python: def process_html_files(html_files), html_norm_processor.py line 25
        /// </summary>
        public bool ProcessHtmlFiles(List<string> htmlFiles)
        {
            Log.Information("Начинаем обработку {Count} HTML файлов с нормами", htmlFiles.Count);

            int processedCount = 0;
            int errorCount = 0;

            foreach (var filePath in htmlFiles)
            {
                try
                {
                    Log.Information("Обработка файла: {File}", System.IO.Path.GetFileName(filePath));

                    // Читаем HTML
                    string content = ReadHtmlFile(filePath);

                    // Парсим нормы
                    var norms = ParseNormsFromHtml(content);

                    // Сохраняем в NormStorage
                    foreach (var (normId, points) in norms)
                    {
                        _normStorage.AddOrUpdateNorm(normId, points);
                        Log.Debug("Норма {NormId} добавлена: {Count} точек", normId, points.Count);
                    }

                    processedCount++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка обработки файла: {File}", filePath);
                    errorCount++;
                }
            }

            Log.Information("Обработка завершена. Успешно: {Success}, Ошибок: {Errors}",
                processedCount, errorCount);

            // Сохраняем хранилище
            if (processedCount > 0)
            {
                _normStorage.SaveToFile();
            }

            return processedCount > 0;
        }

        #endregion

        #region Парсинг норм

        /// <summary>
        /// Парсит нормы из HTML контента
        /// Python: парсинг таблиц в process_html_file()
        /// </summary>
        private Dictionary<string, List<(double Load, double Norm)>> ParseNormsFromHtml(string htmlContent)
        {
            var norms = new Dictionary<string, List<(double, double)>>();

            // Загружаем HTML документ
            var doc = LoadHtmlDocument(htmlContent);

            // Ищем все таблицы с нормами
            // Python: soup.find_all('table')
            var tables = doc.DocumentNode.SelectNodes("//table");

            if (tables == null || tables.Count == 0)
            {
                Log.Warning("Таблицы не найдены в HTML");
                return norms;
            }

            foreach (var table in tables)
            {
                try
                {
                    // Проверяем, что это таблица с нормами
                    // Python: проверка заголовков таблицы
                    if (!IsNormTable(table))
                        continue;

                    // Извлекаем ID нормы из заголовка таблицы
                    string normId = ExtractNormIdFromTable(table);
                    if (string.IsNullOrEmpty(normId))
                    {
                        Log.Debug("Не удалось извлечь ID нормы из таблицы");
                        continue;
                    }

                    // Парсим точки нормы
                    var points = ParseNormPoints(table);
                    if (points.Count > 0)
                    {
                        norms[normId] = points;
                        Log.Debug("Норма {NormId}: {Count} точек", normId, points.Count);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Ошибка парсинга таблицы нормы");
                }
            }

            Log.Information("Найдено норм: {Count}", norms.Count);
            return norms;
        }

        /// <summary>
        /// Проверяет, является ли таблица таблицей норм
        /// </summary>
        private bool IsNormTable(HtmlNode table)
        {
            // Ищем заголовки: "Нагрузка на ось", "Норма" и подобные
            var headers = table.SelectNodes(".//th");
            if (headers == null || headers.Count < 2)
                return false;

            var headerTexts = headers.Select(h => NormalizeText(h.InnerText).ToLower()).ToList();

            bool hasLoadColumn = headerTexts.Any(h =>
                h.Contains("нагрузка") || h.Contains("на ось") || h.Contains("load"));

            bool hasNormColumn = headerTexts.Any(h =>
                h.Contains("норма") || h.Contains("norm"));

            return hasLoadColumn && hasNormColumn;
        }

        /// <summary>
        /// Извлекает ID нормы из таблицы
        /// Python: извлечение из заголовка или caption
        /// </summary>
        private string ExtractNormIdFromTable(HtmlNode table)
        {
            // Ищем caption или th с названием нормы
            // Пример: "Норма для участка МОСКВА-ПЕТЕРБУРГ"

            // Проверяем caption
            var caption = table.SelectSingleNode(".//caption");
            if (caption != null)
            {
                string text = NormalizeText(caption.InnerText);
                // Извлекаем участок из текста
                // TODO Чат 4: Более сложная логика извлечения
                return ExtractSectionNameFromText(text);
            }

            // Проверяем первый th
            var firstHeader = table.SelectSingleNode(".//th");
            if (firstHeader != null)
            {
                string text = NormalizeText(firstHeader.InnerText);
                return ExtractSectionNameFromText(text);
            }

            // Генерируем ID
            return $"NORM_{Guid.NewGuid():N}";
        }

        /// <summary>
        /// Извлекает название участка из текста
        /// </summary>
        private string ExtractSectionNameFromText(string text)
        {
            // Простая реализация: ищем текст после "для" или "участок"
            // TODO Чат 4: Улучшить парсинг

            text = text.ToUpper();

            // Пример: "Норма для участка МОСКВА-ПЕТЕРБУРГ"
            var match = Regex.Match(text, @"(?:ДЛЯ|УЧАСТОК|SECTION)\s+(.+)");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return text;
        }

        /// <summary>
        /// Парсит точки нормы из таблицы
        /// Python: парсинг строк таблицы
        /// </summary>
        private List<(double Load, double Norm)> ParseNormPoints(HtmlNode table)
        {
            var points = new List<(double, double)>();

            // Ищем все строки данных (tr с td)
            var rows = table.SelectNodes(".//tr[td]");
            if (rows == null)
                return points;

            foreach (var row in rows)
            {
                try
                {
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 2)
                        continue;

                    // Первая колонка - нагрузка на ось
                    string loadText = GetNormalizedText(cells[0]);
                    var load = TryConvertToDouble(loadText);

                    // Вторая колонка - норма
                    string normText = GetNormalizedText(cells[1]);
                    var norm = TryConvertToDouble(normText);

                    if (load.HasValue && norm.HasValue)
                    {
                        points.Add((load.Value, norm.Value));
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Ошибка парсинга строки таблицы нормы");
                }
            }

            return points;
        }

        #endregion

        #region Публичные методы

        /// <summary>
        /// Получает список загруженных норм
        /// </summary>
        public List<string> GetLoadedNorms()
        {
            return _normStorage.GetAllNormIds();
        }

        /// <summary>
        /// Получает информацию о хранилище норм
        /// </summary>
        public string GetStorageInfo()
        {
            var normIds = _normStorage.GetAllNormIds();
            return $"Загружено норм: {normIds.Count}";
        }

        #endregion
    }
}
