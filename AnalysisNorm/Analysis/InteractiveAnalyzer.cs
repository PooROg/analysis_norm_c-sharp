// Analysis/InteractiveAnalyzer.cs
// Интерактивный анализатор норм - координатор компонентов
// Мигрировано из: analysis/analyzer.py
// ЧАТ 4: Основная координация + анализ участков
// ИСПРАВЛЕНО: CS1503 - фильтрация DataFrame

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
        private PlotBuilder? _plotBuilder;

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
            _normStorage = new NormStorage();
            _routeProcessor = new RouteProcessor();
            _normProcessor = new NormProcessor(_normStorage);
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
                bool success = _normProcessor.ProcessHtmlFiles(htmlFiles);

                if (!success)
                {
                    Log.Warning("Нормы не загружены");
                    return false;
                }

                int normsCount = _normStorage.GetAllNorms().Count;
                Log.Information("Загружено норм: {Count}", normsCount);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки норм");
                return false;
            }
        }

        #endregion

        #region Построение карты участков и норм

        /// <summary>
        /// Строит карту участков → нормы
        /// Python: _build_sections_norms_map(), analyzer.py, line 80
        /// </summary>
        private void BuildSectionsNormsMap()
        {
            SectionsNormsMap.Clear();

            if (RoutesData == null || RoutesData.Rows.Count == 0)
            {
                Log.Debug("Нет данных для построения карты участков");
                return;
            }

            try
            {
                var sectionCol = RoutesData.Columns["Участок"] as StringDataFrameColumn;
                var normIdCol = RoutesData.Columns["Норма_ID"] as StringDataFrameColumn;

                if (sectionCol == null || normIdCol == null)
                {
                    Log.Warning("Не найдены колонки 'Участок' или 'Норма_ID'");
                    return;
                }

                for (long i = 0; i < RoutesData.Rows.Count; i++)
                {
                    var section = sectionCol[i];
                    var normId = normIdCol[i];

                    if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(normId))
                        continue;

                    if (!SectionsNormsMap.ContainsKey(section))
                    {
                        SectionsNormsMap[section] = new List<string>();
                    }

                    if (!SectionsNormsMap[section].Contains(normId))
                    {
                        SectionsNormsMap[section].Add(normId);
                    }
                }

                Log.Information("Карта участков построена: {Count} участков", SectionsNormsMap.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка построения карты участков");
            }
        }

        #endregion

        #region Получение списков участков и норм

        /// <summary>
        /// Возвращает список доступных участков
        /// Python: get_available_sections(), analyzer.py, line 98
        /// </summary>
        public List<string> GetAvailableSections()
        {
            return SectionsNormsMap.Keys.OrderBy(s => s).ToList();
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
        /// Анализирует данные участка
        /// Python: analyze_section(), analyzer.py, line 115
        /// </summary>
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
                var sectionRoutes = PrepareSectionData(
                    sectionName, normId, singleSectionOnly, locomotiveFilter);

                if (sectionRoutes == null || sectionRoutes.Rows.Count == 0)
                {
                    string message = GetEmptyDataMessage(sectionName, normId, singleSectionOnly);
                    return (null, null, message);
                }

                var (analyzedData, normFunctions) = _dataAnalyzer.AnalyzeSectionData(
                    sectionName, sectionRoutes, normId);

                if (analyzedData == null || analyzedData.Rows.Count == 0)
                {
                    return (null, null, $"Не удалось проанализировать участок {sectionName}");
                }

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

                var statistics = _dataAnalyzer.CalculateStatistics(analyzedData);

                string resultKey = $"{sectionName}_{normId ?? "all"}_{(singleSectionOnly ? "single" : "multi")}";
                AnalyzedResults[resultKey] = new AnalysisResult
                {
                    Routes = analyzedData,
                    NormFunctions = normFunctions,
                    Statistics = statistics
                };

                Log.Information("Анализ участка завершен успешно");
                return (figure, statistics, null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка анализа участка {Section}", sectionName);
                return (null, null, $"Ошибка анализа: {ex.Message}");
            }
        }

        /// <summary>
        /// Подготавливает данные участка с фильтрацией
        /// Python: _prepare_section_data(), analyzer.py, line 140
        /// ИСПРАВЛЕНО CS1503: DataFrame.Filter требует PrimitiveDataFrameColumn<bool>
        /// </summary>
        private DataFrame? PrepareSectionData(
            string sectionName,
            string? normId,
            bool singleSectionOnly,
            LocomotiveFilter? locomotiveFilter)
        {
            if (RoutesData == null)
                return null;

            try
            {
                // Фильтрация по участку
                var sectionCol = RoutesData.Columns["Участок"] as StringDataFrameColumn;
                if (sectionCol == null)
                    return null;

                // ИСПРАВЛЕНО CS1503: Создаем булеву колонку вместо List<long>
                var filterColumn = new PrimitiveDataFrameColumn<bool>("SectionFilter", RoutesData.Rows.Count);

                for (long i = 0; i < RoutesData.Rows.Count; i++)
                {
                    var section = sectionCol[i];
                    filterColumn[i] = (section == sectionName);
                }

                // Применяем фильтр
                var result = RoutesData.Filter(filterColumn);

                if (result == null || result.Rows.Count == 0)
                    return null;

                // TODO: Фильтрация по normId
                // TODO: Фильтрация по locomotiveFilter
                // TODO: Фильтрация singleSectionOnly

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка подготовки данных участка");
                return null;
            }
        }

        /// <summary>
        /// Генерирует сообщение об отсутствии данных
        /// Python: _get_empty_data_message(), analyzer.py, line 165
        /// </summary>
        private string GetEmptyDataMessage(string sectionName, string? normId, bool singleSectionOnly)
        {
            var parts = new List<string> { $"Нет данных для участка '{sectionName}'" };

            if (normId != null)
            {
                parts.Add($"с нормой '{normId}'");
            }

            if (singleSectionOnly)
            {
                parts.Add("(только один участок)");
            }

            return string.Join(" ", parts);
        }

        #endregion

        #region Экспорт и информация

        /// <summary>
        /// Экспортирует маршруты в Excel
        /// Python: export_routes_to_excel(), analyzer.py, line 180
        /// TODO Чат 7: Полная реализация
        /// </summary>
        public void ExportRoutesToExcel(string filePath)
        {
            Log.Debug("Экспорт в Excel (заглушка для Чата 7)");
            throw new NotImplementedException("Экспорт в Excel будет реализован в Чате 7");
        }

        /// <summary>
        /// Возвращает DataFrame маршрутов
        /// Python: get_routes_data(), analyzer.py, line 185
        /// </summary>
        public DataFrame? GetRoutesData()
        {
            return RoutesData;
        }

        /// <summary>
        /// Возвращает информацию о хранилище норм
        /// Python: get_norm_storage_info(), analyzer.py, line 188
        /// </summary>
        public StorageInfo GetNormStorageInfo()
        {
            return _normStorage.GetStorageInfo();
        }

        /// <summary>
        /// Конвертирует StorageInfo в Dictionary для обратной совместимости
        /// </summary>
        public Dictionary<string, object> GetNormStorageInfoAsDictionary()
        {
            var info = _normStorage.GetStorageInfo();
            return new Dictionary<string, object>
            {
                ["StorageFile"] = info.StorageFile ?? "N/A",
                ["FileSizeMB"] = info.FileSizeMB,
                ["Version"] = info.Version ?? "N/A",
                ["TotalNorms"] = info.TotalNorms,
                ["LastUpdated"] = info.LastUpdated?.ToString() ?? "N/A",
                ["NormTypes"] = info.NormTypes ?? new Dictionary<string, int>(),
                ["CachedFunctions"] = info.CachedFunctions
            };
        }

        #endregion
    }

    #region Вспомогательные классы

    /// <summary>
    /// Результат анализа участка
    /// Python: dict с ключами routes, norms, statistics
    /// </summary>
    public class AnalysisResult
    {
        public DataFrame? Routes { get; set; }
        public Dictionary<string, Func<double, double>>? NormFunctions { get; set; }
        public Dictionary<string, object>? Statistics { get; set; }
    }

    /// <summary>
    /// Фильтр локомотивов
    /// TODO Чат 5: Полная реализация
    /// </summary>
    public class LocomotiveFilter
    {
        public List<string>? SelectedSeries { get; set; }
        public List<string>? SelectedNumbers { get; set; }
        public bool ApplyCoefficients { get; set; }
    }

    #endregion
}