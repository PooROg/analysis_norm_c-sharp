// Analysis/RouteProcessor.cs
// Обработчик HTML файлов с маршрутами
// Мигрировано из: analysis/html_route_processor.py (~1800 строк!)
// ЧАТ 3: Основная структура + ключевые методы
// TODO Чат 4: Сложные регулярные выражения и детальный парсинг

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Data.Analysis;
using Serilog;
using AnalysisNorm.Models;

namespace AnalysisNorm.Analysis
{
    /// <summary>
    /// Процессор для парсинга HTML файлов с маршрутами РЖД
    /// Python: class HTMLRouteProcessor, html_route_processor.py
    /// 
    /// УПРОЩЕНИЯ ДЛЯ ЧАТА 3:
    /// - Основная структура и flow
    /// - Базовые методы парсинга
    /// - Сложные регулярки → заглушки с TODO
    /// - Объединение участков → упрощенная версия
    /// </summary>
    public class RouteProcessor : HtmlProcessorBase
    {
        #region Поля

        /// <summary>
        /// Результирующий DataFrame с маршрутами
        /// Python: self.routes_df
        /// </summary>
        public DataFrame? RoutesDataFrame { get; private set; }

        /// <summary>
        /// Статистика обработки
        /// Python: self.processing_stats
        /// </summary>
        public ProcessingStats ProcessingStats { get; private set; }

        #endregion

        #region Конструктор

        public RouteProcessor()
        {
            ProcessingStats = new ProcessingStats();
            Log.Information("RouteProcessor инициализирован");
        }

        #endregion

        #region Главный метод обработки

        /// <summary>
        /// Обрабатывает список HTML файлов и возвращает DataFrame с маршрутами
        /// Python: def process_html_files(html_files), line 560
        /// </summary>
        public DataFrame ProcessHtmlFiles(List<string> htmlFiles)
        {
            Log.Information("Начинаем обработку {Count} HTML файлов маршрутов", htmlFiles.Count);

            // Сброс статистики
            ProcessingStats = new ProcessingStats();
            var allRecords = new List<RouteRecord>();

            for (int i = 0; i < htmlFiles.Count; i++)
            {
                string filePath = htmlFiles[i];
                LogProgress(i + 1, htmlFiles.Count, "Обработка файлов");

                try
                {
                    // Читаем HTML
                    string content = ReadHtmlFile(filePath);

                    // Находим блок с маршрутами
                    // Python: content[start_marker:end_marker]
                    string extracted = ExtractRoutesBlock(content);

                    if (string.IsNullOrEmpty(extracted))
                    {
                        Log.Warning("Маршруты не найдены в файле: {File}", filePath);
                        ProcessingStats.RoutesSkipped++;
                        continue;
                    }

                    // Очистка HTML
                    extracted = SplitRoutesToLines(extracted);
                    extracted = RemoveVchtRoutes(extracted);
                    extracted = CleanHtmlContent(extracted);

                    // Парсинг маршрутов
                    var records = ProcessRoutesFromHtml(extracted);
                    allRecords.AddRange(records);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка обработки файла: {File}", filePath);
                    ProcessingStats.RoutesSkipped++;
                }
            }

            // Создаем DataFrame из записей
            RoutesDataFrame = CreateDataFrameFromRecords(allRecords);

            Log.Information("Обработка завершена. Получено записей: {Count}", allRecords.Count);
            Log.Information("Статистика: {Stats}", ProcessingStats);

            return RoutesDataFrame;
        }

        #endregion

        #region Извлечение блока маршрутов

        /// <summary>
        /// Извлекает блок с маршрутами из HTML
        /// Python: часть process_html_files(), определение start_pos и end_pos
        /// </summary>
        private string ExtractRoutesBlock(string content)
        {
            // Ищем начало: <td class=main_win>
            // Python: start_marker = '<td class=main_win>'
            const string startMarker = "<td class=main_win>";
            int startPos = content.IndexOf(startMarker);

            if (startPos == -1)
            {
                Log.Warning("Начальный маркер не найден");
                return string.Empty;
            }

            // Ищем конец: </form> после начала
            // Python: form_pattern = r'</form>(?=\n|$)'
            var formPattern = new Regex(@"</form>(?=\n|$)", RegexOptions.Multiline);
            var formMatch = formPattern.Match(content, startPos);

            if (!formMatch.Success)
            {
                Log.Warning("Конечный маркер не найден");
                return string.Empty;
            }

            int formEndPos = formMatch.Index + formMatch.Length;

            // Ищем </td></tr> после </form>
            string remaining = content.Substring(formEndPos);
            var lines = remaining.Split('\n');
            int endPos = formEndPos;

            foreach (var line in lines)
            {
                if (line.Trim().Contains("</td></tr>"))
                {
                    endPos = formEndPos + remaining.IndexOf(line) + line.Length;
                    break;
                }
            }

            return content.Substring(startPos, endPos - startPos + 1);
        }

        #endregion

        #region Разбиение и фильтрация маршрутов

        /// <summary>
        /// Разбивает HTML на отдельные строки по маршрутам
        /// Python: def _split_routes_to_lines(content), line 195
        /// </summary>
        private string SplitRoutesToLines(string content)
        {
            Log.Debug("Разбиваем маршруты по отдельным строкам...");

            // Python: route_pattern = r'(<table[^>]*><tr><th class=thl_common><font class=filter_key>\s*Маршрут\s*№:.*?<br><br><br>)'
            var routePattern = new Regex(
                @"(<table[^>]*><tr><th class=thl_common><font class=filter_key>\s*Маршрут\s*№:.*?<br><br><br>)",
                RegexOptions.Singleline);

            var routes = routePattern.Matches(content);

            if (routes.Count == 0)
            {
                Log.Warning("Маршруты не найдены для разделения");
                return content;
            }

            // Разделяем на: до маршрутов, маршруты, после маршрутов
            int firstRouteStart = routes[0].Index;
            string beforeRoutes = content.Substring(0, firstRouteStart);

            var lastRoute = routes[routes.Count - 1];
            int lastRouteEnd = lastRoute.Index + lastRoute.Length;
            string afterRoutes = content.Substring(lastRouteEnd);

            var resultLines = new List<string>();

            if (!string.IsNullOrWhiteSpace(beforeRoutes))
                resultLines.Add(beforeRoutes.TrimEnd());

            resultLines.Add("<!-- НАЧАЛО_ПЕРВОГО_МАРШРУТА -->");

            foreach (Match route in routes)
                resultLines.Add(route.Value);

            resultLines.Add("<!-- КОНЕЦ_ПОСЛЕДНЕГО_МАРШРУТА -->");

            if (!string.IsNullOrWhiteSpace(afterRoutes))
                resultLines.Add(afterRoutes.TrimStart());

            Log.Debug("Маршруты разделены на {Count} строк", routes.Count);
            return string.Join("\n", resultLines);
        }

        /// <summary>
        /// Удаляет строки с маршрутами ВЧТ (весовой контроль тяги)
        /// Python: def _remove_vcht_routes(content), line 225
        /// </summary>
        private string RemoveVchtRoutes(string content)
        {
            Log.Debug("Удаляем маршруты с ' ВЧТ '...");

            var lines = content.Split('\n');
            var filtered = new List<string>();
            int removed = 0;

            foreach (var line in lines)
            {
                // Python: if '<td class = itog2>" ВЧТ "</td>' in line:
                if (line.Contains("<td class = itog2>\" ВЧТ \"</td>"))
                {
                    removed++;
                    continue;
                }
                filtered.Add(line);
            }

            if (removed > 0)
                Log.Information("Удалено {Count} маршрутов с ' ВЧТ '", removed);

            return string.Join("\n", filtered);
        }

        #endregion

        #region Извлечение маршрутов

        /// <summary>
        /// Извлекает маршруты из HTML контента
        /// Python: def extract_routes_from_html(html_content), line 245
        /// </summary>
        private List<(string Html, RouteMetadata Metadata)> ExtractRoutesFromHtml(string htmlContent)
        {
            Log.Information("Извлекаем маршруты из HTML");

            // Определяем секцию с маршрутами
            const string startMarker = "<!-- НАЧАЛО_ПЕРВОГО_МАРШРУТА -->";
            const string endMarker = "<!-- КОНЕЦ_ПОСЛЕДНЕГО_МАРШРУТА -->";

            int startPos = htmlContent.IndexOf(startMarker);
            int endPos = htmlContent.IndexOf(endMarker);

            string routesSection = (startPos == -1 || endPos == -1)
                ? htmlContent
                : htmlContent.Substring(startPos + startMarker.Length, endPos - startPos - startMarker.Length);

            var lines = routesSection.Trim().Split('\n');
            var routes = new List<(string, RouteMetadata)>();

            foreach (var line in lines)
            {
                string s = line.Trim();
                if (string.IsNullOrEmpty(s))
                    continue;

                // Проверяем, что это строка с маршрутом
                // Python: if re.search(r'<table width=\d+%', s) and ('Маршрут №' in s or 'Маршрут' in s):
                if (Regex.IsMatch(s, @"<table width=\d+%") &&
                    (s.Contains("Маршрут №") || s.Contains("Маршрут")))
                {
                    var metadata = ExtractRouteHeaderFromHtml(s);
                    if (metadata != null)
                    {
                        routes.Add((s, metadata));
                        Log.Debug("Найден маршрут: №{Number}", metadata.Number);
                    }
                }
            }

            Log.Information("Найдено маршрутов: {Count}", routes.Count);
            return routes;
        }

        /// <summary>
        /// Извлекает метаданные из заголовка маршрута
        /// Python: def extract_route_header_from_html(route_html), line 265
        /// 
        /// ЧАТ 3: УПРОЩЕННАЯ ВЕРСИЯ
        /// TODO Чат 4: Полная реализация с всеми регулярками
        /// </summary>
        private RouteMetadata? ExtractRouteHeaderFromHtml(string routeHtml)
        {
            try
            {
                var metadata = new RouteMetadata();

                // Извлечение номера маршрута
                // Python: route_num_match = re.search(r'Маршрут\s*№:\s*(\d+)', route_html)
                var routeNumMatch = Regex.Match(routeHtml, @"Маршрут\s*№:\s*(\d+)");
                if (routeNumMatch.Success)
                    metadata.Number = routeNumMatch.Groups[1].Value;

                // TODO Чат 4: Извлечение остальных полей
                // - Дата поездки (сложное регулярное выражение)
                // - Серия и номер локомотива
                // - Табельный машиниста
                // - Депо
                // - Идентификатор

                // ЗАГЛУШКА для Чата 3:
                metadata.TripDate = "UNKNOWN";
                metadata.DriverTab = "UNKNOWN";
                metadata.Identifier = $"Route_{metadata.Number}_{Guid.NewGuid():N}";

                return metadata;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка извлечения метаданных маршрута");
                return null;
            }
        }

        #endregion

        #region Обработка маршрутов

        /// <summary>
        /// Обрабатывает все маршруты из очищенного HTML
        /// Python: def _process_routes(html_content), line 474
        /// 
        /// ЧАТ 3: Основная структура
        /// TODO Чат 4: Детальная обработка участков и группировка дубликатов
        /// </summary>
        private List<RouteRecord> ProcessRoutesFromHtml(string htmlContent)
        {
            var routes = ExtractRoutesFromHtml(htmlContent);

            var stats = new ProcessingStats
            {
                TotalRoutesFound = routes.Count
            };

            if (routes.Count == 0)
            {
                Log.Error("Маршруты не найдены в HTML");
                return new List<RouteRecord>();
            }

            // Группировка маршрутов по: номер + дата + табельный
            var routeGroups = new Dictionary<string, List<(string Html, RouteMetadata Meta)>>();

            foreach (var (html, meta) in routes)
            {
                // Фильтр Ю6 (пока упрощенный)
                if (CheckYu6Filter(html))
                {
                    stats.RoutesSkipped++;
                    continue;
                }

                if (!string.IsNullOrEmpty(meta.Number) &&
                    !string.IsNullOrEmpty(meta.TripDate) &&
                    !string.IsNullOrEmpty(meta.DriverTab))
                {
                    string key = $"{meta.Number}_{meta.TripDate}_{meta.DriverTab}";
                    if (!routeGroups.ContainsKey(key))
                        routeGroups[key] = new List<(string, RouteMetadata)>();

                    routeGroups[key].Add((html, meta));
                }
                else
                {
                    stats.RoutesSkipped++;
                }
            }

            stats.UniqueRoutes = routeGroups.Count;
            var allRecords = new List<RouteRecord>();

            // Обрабатываем каждую группу
            foreach (var (key, group) in routeGroups)
            {
                Log.Information("Обработка маршрута {Key}, версий: {Count}", key, group.Count);

                if (group.Count > 1)
                {
                    stats.DuplicatesTotal += group.Count - 1;
                    stats.DuplicateDetails[key] = new DuplicateInfo
                    {
                        Versions = group.Count,
                        Duplicates = group.Count - 1,
                        Identifiers = group.Select(g => g.Meta.Identifier ?? "").ToList()
                    };
                }

                // Проверка на равенство расходов
                int equalCount = group.Count(r => CheckRashodEqual(r.Html));
                if (equalCount > 0)
                    stats.RoutesWithEqualRashod += equalCount;

                // Выбор лучшего маршрута из группы
                var bestRoute = SelectBestRoute(group);
                if (bestRoute == null)
                {
                    stats.RoutesSkipped++;
                    continue;
                }

                // TODO Чат 4: Полный парсинг участков
                // Пока создаем простую запись-заглушку
                var record = CreateSimpleRouteRecord(bestRoute.Value.Meta);
                allRecords.Add(record);
                stats.RoutesProcessed++;
            }

            stats.OutputRows = allRecords.Count;

            // Обновляем общую статистику
            ProcessingStats.TotalRoutesFound += stats.TotalRoutesFound;
            ProcessingStats.UniqueRoutes += stats.UniqueRoutes;
            ProcessingStats.DuplicatesTotal += stats.DuplicatesTotal;
            ProcessingStats.RoutesWithEqualRashod += stats.RoutesWithEqualRashod;
            ProcessingStats.RoutesSkipped += stats.RoutesSkipped;
            ProcessingStats.RoutesProcessed += stats.RoutesProcessed;
            ProcessingStats.OutputRows += stats.OutputRows;

            return allRecords;
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Проверка фильтра Ю6
        /// Python: def check_yu6_filter(route_html), line 366
        /// ЧАТ 3: Упрощенная версия
        /// </summary>
        private bool CheckYu6Filter(string routeHtml)
        {
            // TODO Чат 4: Полная реализация с поиском таблиц ТУ3 и Ю7
            return false; // Пока не фильтруем
        }

        /// <summary>
        /// Проверка равенства расходов (Н=Ф)
        /// Python: def check_rashod_equal_html(route_html), line 320
        /// ЧАТ 3: Упрощенная версия
        /// </summary>
        private bool CheckRashodEqual(string routeHtml)
        {
            // TODO Чат 4: Полная реализация
            return false;
        }

        /// <summary>
        /// Выбор лучшего маршрута из группы дубликатов
        /// Python: def select_best_route(group), line 535
        /// ЧАТ 3: Берем первый
        /// </summary>
        private (string Html, RouteMetadata Meta)? SelectBestRoute(
            List<(string Html, RouteMetadata Meta)> group)
        {
            if (group.Count == 0)
                return null;

            // TODO Чат 4: Выбор по критериям (приоритет Н!=Ф)
            return group[0];
        }

        /// <summary>
        /// Создает простую запись маршрута (для Чата 3)
        /// TODO Чат 4: Полный парсинг участков
        /// </summary>
        private RouteRecord CreateSimpleRouteRecord(RouteMetadata meta)
        {
            return new RouteRecord
            {
                RouteNumber = meta.Number,
                TripDate = meta.TripDate,
                LocomotiveSeries = meta.LocomotiveSeries ?? "UNKNOWN",
                LocomotiveNumber = meta.LocomotiveNumber ?? "UNKNOWN",
                Depot = meta.Depot ?? "UNKNOWN",
                DriverTab = meta.DriverTab,
                SectionName = "ВСЕ_УЧАСТКИ", // Заглушка
                DuplicatesCount = 0
            };
        }

        #endregion

        #region Создание DataFrame

        /// <summary>
        /// Создает DataFrame из списка записей
        /// Python: pd.DataFrame(all_rows)
        /// </summary>
        private DataFrame CreateDataFrameFromRecords(List<RouteRecord> records)
        {
            if (records.Count == 0)
            {
                Log.Warning("Нет записей для создания DataFrame");
                return new DataFrame();
            }

            // TODO Чат 4: Полное создание DataFrame со всеми колонками
            // Пока создаем минимальный DataFrame

            var columns = new List<DataFrameColumn>
            {
                new StringDataFrameColumn("Маршрут №", records.Select(r => r.RouteNumber)),
                new StringDataFrameColumn("Дата поездки", records.Select(r => r.TripDate)),
                new StringDataFrameColumn("Серия", records.Select(r => r.LocomotiveSeries)),
                new StringDataFrameColumn("Номер", records.Select(r => r.LocomotiveNumber)),
                new StringDataFrameColumn("Депо", records.Select(r => r.Depot)),
                new StringDataFrameColumn("Участок", records.Select(r => r.SectionName))
            };

            var df = new DataFrame(columns);
            Log.Information("DataFrame создан: {Rows} строк, {Cols} колонок", df.Rows.Count, df.Columns.Count);

            return df;
        }

        #endregion

        #region Публичные методы для получения данных

        /// <summary>
        /// Получает список уникальных участков из обработанных маршрутов
        /// </summary>
        public List<string> GetSections()
        {
            if (RoutesDataFrame == null || RoutesDataFrame.Rows.Count == 0)
                return new List<string>();

            var sectionColumn = RoutesDataFrame.Columns["Участок"] as StringDataFrameColumn;
            if (sectionColumn == null)
                return new List<string>();

            return sectionColumn.Distinct().Where(s => !string.IsNullOrEmpty(s)).ToList();
        }

        /// <summary>
        /// Получает DataFrame для дальнейшей обработки
        /// </summary>
        public DataFrame GetDataFrame()
        {
            return RoutesDataFrame ?? new DataFrame();
        }

        #endregion
    }
}
