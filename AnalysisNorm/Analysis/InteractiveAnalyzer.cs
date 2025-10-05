// Analysis/InteractiveAnalyzer.cs
// Интерактивный анализатор норм - координатор компонентов
// Мигрировано из: analysis/analyzer.py
// ЧАТ 4: Основная координация + анализ участков

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;
using Serilog;
using AnalysisNorm.Core;
using AnalysisNorm.Models;

namespace AnalysisNorm.Analysis
{
    /// <summary>
    /// Интерактивный анализатор норм расхода электроэнергии
    /// Python: class InteractiveNormsAnalyzer, analyzer.py
    /// 
    /// Координирует работу всех компонентов:
    /// - RouteProcessor (парсинг HTML маршрутов)
    /// - NormProcessor (парсинг HTML норм)
    /// - DataAnalyzer (анализ данных)
    /// - PlotBuilder (построение графиков)
    /// </summary>
    public class InteractiveAnalyzer
    {
        #region Поля

        private readonly RouteProcessor _routeProcessor;
        private readonly NormProcessor _normProcessor;
        private readonly NormStorage _normStorage;
        private readonly RouteDataAnalyzer _dataAnalyzer;
        private PlotBuilder? _plotBuilder;  // Будет установлен после создания

        /// <summary>
        /// Основной DataFrame с маршрутами
        /// Python: self.routes_df
        /// </summary>
        public DataFrame? RoutesData { get; private set; }

        /// <summary>
        /// Результаты анализа по ключам "участок_норма_режим"
        /// Python: self.analyzed_results
        /// </summary>
        public Dictionary<string, AnalysisResult> AnalyzedResults { get; }

        /// <summary>
        /// Карта участков → список норм
        /// Python: self.sections_norms_map
        /// </summary>
        public Dictionary<string, List<string>> SectionsNormsMap { get; }

        #endregion

        #region Конструктор

        public InteractiveAnalyzer()
        {
            _routeProcessor = new RouteProcessor();
            _normProcessor = new NormProcessor();
            _normStorage = new NormStorage();
            _dataAnalyzer = new RouteDataAnalyzer(_normStorage);

            AnalyzedResults = new Dictionary<string, AnalysisResult>();
            SectionsNormsMap = new Dictionary<string, List<string>>();

            Log.Information("InteractiveAnalyzer инициализирован");
        }

        /// <summary>
        /// Устанавливает PlotBuilder после его создания
        /// </summary>
        public void SetPlotBuilder(PlotBuilder plotBuilder)
        {
            _plotBuilder = plotBuilder ?? throw new ArgumentNullException(nameof(plotBuilder));
            Log.Debug("PlotBuilder установлен");
        }

        #endregion

        #region Загрузка данных

        /// <summary>
        /// Загружает маршруты из HTML файлов
        /// Python: load_routes_from_html(), analyzer.py, line 44
        /// </summary>
        public bool LoadRoutesFromHtml(List<string> htmlFiles)
        {
            Log.Information("Загрузка маршрутов из {Count} HTML файлов", htmlFiles.Count);

            try
            {
                RoutesData = _routeProcessor.ProcessHtmlFiles(htmlFiles);
                
                if (RoutesData == null || RoutesData.Rows.Count == 0)
                {
                    Log.Error("Не удалось загрузить маршруты");
                    return false;
                }

                Log.Information("Загружено записей: {Count}", RoutesData.Rows.Count);
                BuildSectionsNormsMap();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки маршрутов");
                return false;
            }
        }

        /// <summary>
        /// Загружает нормы из HTML файлов
        /// Python: load_norms_from_html(), analyzer.py, line 62
        /// </summary>
        public bool LoadNormsFromHtml(List<string> htmlFiles)
        {
            Log.Information("Загрузка норм из {Count} HTML файлов", htmlFiles.Count);

            try
            {
                var normsData = _normProcessor.ProcessHtmlFiles(htmlFiles, _normStorage);
                
                if (normsData == null || normsData.Count == 0)
                {
                    Log.Warning("Нормы не загружены");
                    return false;
                }

                Log.Information("Загружено норм: {Count}", normsData.Count);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки норм");
                return false;
            }
        }

        #endregion

        #region Построение карт

        /// <summary>
        /// Строит карту участков → нормы
        /// Python: _build_sections_norms_map(), analyzer.py, line 80
        /// </summary>
        private void BuildSectionsNormsMap()
        {
            Log.Debug("Построение карты участков → нормы");

            SectionsNormsMap.Clear();

            if (RoutesData == null || RoutesData.Rows.Count == 0)
            {
                Log.Warning("Нет данных для построения карты");
                return;
            }

            try
            {
                var sectionCol = RoutesData.Columns["Наименование участка"];
                var normCol = RoutesData.Columns["Номер нормы"];

                for (long i = 0; i < RoutesData.Rows.Count; i++)
                {
                    var section = sectionCol[i]?.ToString();
                    var norm = normCol[i]?.ToString();

                    if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(norm))
                        continue;

                    if (!SectionsNormsMap.ContainsKey(section))
                    {
                        SectionsNormsMap[section] = new List<string>();
                    }

                    if (!SectionsNormsMap[section].Contains(norm))
                    {
                        SectionsNormsMap[section].Add(norm);
                    }
                }

                Log.Information("Карта построена: {SectionCount} участков, {NormCount} уникальных норм",
                    SectionsNormsMap.Count, SectionsNormsMap.Values.SelectMany(n => n).Distinct().Count());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка построения карты участков → нормы");
            }
        }

        /// <summary>
        /// Возвращает список участков
        /// Python: get_available_sections(), analyzer.py, line 98
        /// </summary>
        public List<string> GetAvailableSections()
        {
            if (RoutesData == null || RoutesData.Rows.Count == 0)
                return new List<string>();

            try
            {
                var sectionCol = RoutesData.Columns["Наименование участка"];
                var sections = new HashSet<string>();

                for (long i = 0; i < RoutesData.Rows.Count; i++)
                {
                    var section = sectionCol[i]?.ToString();
                    if (!string.IsNullOrEmpty(section))
                    {
                        sections.Add(section);
                    }
                }

                return sections.OrderBy(s => s).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка получения списка участков");
                return new List<string>();
            }
        }

        /// <summary>
        /// Возвращает список норм для участка
        /// Python: get_norms_for_section(), analyzer.py, line 104
        /// </summary>
        public List<string> GetNormsForSection(string sectionName)
        {
            if (SectionsNormsMap.TryGetValue(sectionName, out var norms))
            {
                return norms.OrderBy(n => n).ToList();
            }

            return new List<string>();
        }

        #endregion

        #region Анализ участка

        /// <summary>
        /// Анализирует участок маршрута
        /// Python: analyze_section(), analyzer.py, line 115
        /// </summary>
        /// <param name="sectionName">Название участка</param>
        /// <param name="normId">ID нормы (опционально)</param>
        /// <param name="singleSectionOnly">Только один участок</param>
        /// <param name="locomotiveFilter">Фильтр локомотивов (опционально)</param>
        /// <returns>Результат анализа с графиком и статистикой</returns>
        public (object? Figure, Dictionary<string, object>? Statistics, string? Error) AnalyzeSection(
            string sectionName,
            string? normId = null,
            bool singleSectionOnly = false,
            LocomotiveFilter? locomotiveFilter = null)
        {
            Log.Information("Анализ участка: {Section}, норма: {Norm}, только один участок: {SingleSection}",
                sectionName, normId ?? "все", singleSectionOnly);

            if (RoutesData == null || RoutesData.Rows.Count == 0)
            {
                return (null, null, "Данные маршрутов не загружены");
            }

            try
            {
                // Фильтрация данных
                var sectionRoutes = PrepareSectionData(
                    sectionName, normId, singleSectionOnly, locomotiveFilter);

                if (sectionRoutes == null || sectionRoutes.Rows.Count == 0)
                {
                    string message = GetEmptyDataMessage(sectionName, normId, singleSectionOnly);
                    return (null, null, message);
                }

                // Анализ данных
                var (analyzedData, normFunctions) = _dataAnalyzer.AnalyzeSectionData(
                    sectionName, sectionRoutes, normId);

                if (analyzedData == null || analyzedData.Rows.Count == 0)
                {
                    return (null, null, $"Не удалось проанализировать участок {sectionName}");
                }

                // Построение графика
                object? figure = null;
                if (_plotBuilder != null)
                {
                    figure = _plotBuilder.CreateInteractivePlot(
                        sectionName, analyzedData, normFunctions, normId, singleSectionOnly);
                }
                else
                {
                    Log.Warning("PlotBuilder не установлен, график не будет построен");
                }

                // Статистика
                var statistics = _dataAnalyzer.CalculateStatistics(analyzedData);

                // Сохраняем результат
                string resultKey = $"{sectionName}_{normId ?? "all"}_{singleSectionOnly}";
                AnalyzedResults[resultKey] = new AnalysisResult
                {
                    Routes = analyzedData,
                    NormFunctions = normFunctions,
                    Statistics = statistics
                };

                Log.Information("Анализ участка {Section} завершен", sectionName);
                return (figure, statistics, null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка анализа участка {Section}", sectionName);
                return (null, null, $"Ошибка анализа: {ex.Message}");
            }
        }

        #endregion

        #region Подготовка данных

        /// <summary>
        /// Подготавливает данные участка для анализа
        /// Python: _prepare_section_data(), analyzer.py, line 140
        /// </summary>
        private DataFrame? PrepareSectionData(
            string sectionName,
            string? normId,
            bool singleSectionOnly,
            LocomotiveFilter? locomotiveFilter)
        {
            if (RoutesData == null)
                return null;

            Log.Debug("Подготовка данных участка {Section}", sectionName);

            try
            {
                var sectionCol = RoutesData.Columns["Наименование участка"];
                var filtered = new List<long>();

                // Фильтрация по участку
                for (long i = 0; i < RoutesData.Rows.Count; i++)
                {
                    var section = sectionCol[i]?.ToString();
                    if (section == sectionName)
                    {
                        filtered.Add(i);
                    }
                }

                if (filtered.Count == 0)
                {
                    Log.Warning("Нет данных для участка {Section}", sectionName);
                    return null;
                }

                // Создаем отфильтрованный DataFrame
                // TODO: Реализовать более эффективную фильтрацию
                var result = CreateFilteredDataFrame(RoutesData, filtered);

                // TODO Чат 5: Применение фильтра локомотивов
                // TODO Чат 5: Применение коэффициентов

                Log.Debug("Подготовлено {Count} записей для участка {Section}", 
                    result.Rows.Count, sectionName);

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка подготовки данных участка {Section}", sectionName);
                return null;
            }
        }

        /// <summary>
        /// Создает отфильтрованный DataFrame из списка индексов
        /// </summary>
        private DataFrame CreateFilteredDataFrame(DataFrame source, List<long> indices)
        {
            var result = new DataFrame();

            // Копируем структуру колонок
            foreach (var column in source.Columns)
            {
                DataFrameColumn newColumn;
                
                if (column is StringDataFrameColumn)
                    newColumn = new StringDataFrameColumn(column.Name);
                else if (column is DoubleDataFrameColumn)
                    newColumn = new DoubleDataFrameColumn(column.Name);
                else if (column is Int32DataFrameColumn)
                    newColumn = new Int32DataFrameColumn(column.Name);
                else
                    newColumn = new PrimitiveDataFrameColumn<object>(column.Name);

                result.Columns.Add(newColumn);
            }

            // Копируем данные по индексам
            foreach (var index in indices)
            {
                var row = new List<object?>();
                
                foreach (var column in source.Columns)
                {
                    row.Add(column[index]);
                }

                result.Append(row, inPlace: true);
            }

            return result;
        }

        /// <summary>
        /// Возвращает сообщение для случая отсутствия данных
        /// Python: _get_empty_data_message(), analyzer.py, line 165
        /// </summary>
        private string GetEmptyDataMessage(string sectionName, string? normId, bool singleSectionOnly)
        {
            if (singleSectionOnly)
            {
                return $"Нет данных для участка '{sectionName}' с фильтром 'Только один участок'";
            }
            else if (!string.IsNullOrEmpty(normId))
            {
                return $"Нет данных для участка '{sectionName}' с нормой '{normId}'";
            }
            else
            {
                return $"Нет данных для участка '{sectionName}'";
            }
        }

        #endregion

        #region Утилиты и экспорт

        /// <summary>
        /// Экспортирует маршруты в Excel
        /// Python: export_routes_to_excel(), analyzer.py, line 180
        /// TODO Чат 7: Полная реализация экспорта
        /// </summary>
        public bool ExportRoutesToExcel(string outputFile)
        {
            if (RoutesData == null || RoutesData.Rows.Count == 0)
            {
                Log.Warning("Нет данных для экспорта");
                return false;
            }

            // TODO Чат 7: Реализация через RouteProcessor.ExportToExcel()
            Log.Information("Экспорт в Excel: {File} (заглушка для Чата 7)", outputFile);
            return true;
        }

        /// <summary>
        /// Возвращает копию данных маршрутов
        /// Python: get_routes_data(), analyzer.py, line 185
        /// </summary>
        public DataFrame? GetRoutesData()
        {
            return RoutesData?.Clone();
        }

        /// <summary>
        /// Возвращает информацию о хранилище норм
        /// Python: get_norm_storage_info(), analyzer.py, line 188
        /// </summary>
        public Dictionary<string, object> GetNormStorageInfo()
        {
            return _normStorage.GetStorageInfo();
        }

        #endregion
    }

    /// <summary>
    /// Результат анализа участка
    /// Python: словарь в analyzed_results, analyzer.py
    /// </summary>
    public class AnalysisResult
    {
        public DataFrame? Routes { get; set; }
        public Dictionary<string, Func<double, double>>? NormFunctions { get; set; }
        public Dictionary<string, object>? Statistics { get; set; }
    }
}
