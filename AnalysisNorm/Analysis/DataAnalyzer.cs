// Analysis/DataAnalyzer.cs
// Анализатор данных маршрутов с интерполяцией норм
// Мигрировано из: analysis/data_analyzer.py
// ЧАТ 4: Полная реализация

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;
using Serilog;
using AnalysisNorm.Core;

namespace AnalysisNorm.Analysis
{
    /// <summary>
    /// Анализатор данных маршрутов с интерполяцией норм
    /// Python: class RouteDataAnalyzer, data_analyzer.py
    /// </summary>
    public class RouteDataAnalyzer
    {
        private readonly NormStorage _normStorage;

        public RouteDataAnalyzer(NormStorage normStorage)
        {
            _normStorage = normStorage ?? throw new ArgumentNullException(nameof(normStorage));
            Log.Information("RouteDataAnalyzer инициализирован");
        }

        #region Основной анализ

        /// <summary>
        /// Анализирует данные участка с интерполяцией норм
        /// Python: analyze_section_data(), data_analyzer.py, line 19
        /// </summary>
        /// <param name="sectionName">Название участка</param>
        /// <param name="routesData">DataFrame с маршрутами</param>
        /// <param name="specificNormId">Конкретная норма (опционально)</param>
        /// <returns>Проанализированный DataFrame и функции норм</returns>
        public (DataFrame AnalyzedData, Dictionary<string, Func<double, double>> NormFunctions) 
            AnalyzeSectionData(
                string sectionName, 
                DataFrame routesData, 
                string? specificNormId = null)
        {
            Log.Debug("Анализ участка {Section}, строк: {Count}", sectionName, routesData.Rows.Count);

            // Определяем нормы для анализа
            List<string> normNumbers;
            if (!string.IsNullOrEmpty(specificNormId))
            {
                normNumbers = new List<string> { specificNormId };
            }
            else
            {
                // Получаем уникальные нормы из DataFrame
                var normCol = routesData.Columns["Номер нормы"];
                normNumbers = new List<string>();
                
                for (long i = 0; i < routesData.Rows.Count; i++)
                {
                    var normValue = normCol[i]?.ToString();
                    if (!string.IsNullOrEmpty(normValue) && !normNumbers.Contains(normValue))
                    {
                        normNumbers.Add(normValue);
                    }
                }
            }

            // Создаем функции норм
            var normFunctions = CreateNormFunctions(normNumbers, routesData);
            
            if (normFunctions.Count == 0)
            {
                Log.Warning("Не найдено функций норм для участка {Section}", sectionName);
                return (routesData, normFunctions);
            }

            // Применяем интерполяцию и расчеты
            var analyzedData = InterpolateAndCalculate(routesData, normFunctions);

            Log.Information("Проанализировано {Count} строк для участка {Section}", 
                analyzedData.Rows.Count, sectionName);

            return (analyzedData, normFunctions);
        }

        #endregion

        #region Создание функций норм

        /// <summary>
        /// Создает функции интерполяции для норм
        /// Python: _create_norm_functions(), data_analyzer.py, line 32
        /// </summary>
        private Dictionary<string, Func<double, double>> CreateNormFunctions(
            List<string> normNumbers, 
            DataFrame routesData)
        {
            Log.Debug("Создаем функции для {Count} норм", normNumbers.Count);

            var normFunctions = new Dictionary<string, Func<double, double>>();

            // Получаем колонки для работы
            var normCol = routesData.Columns["Номер нормы"];
            var osesCol = routesData.Columns["ОСИ"];

            // Группируем данные по нормам для определения диапазона осей
            var normOsesRanges = new Dictionary<string, (double Min, double Max)>();

            for (long i = 0; i < routesData.Rows.Count; i++)
            {
                var normId = normCol[i]?.ToString();
                if (string.IsNullOrEmpty(normId))
                    continue;

                var osesValue = osesCol[i];
                if (osesValue == null)
                    continue;

                double oses;
                if (osesValue is double d)
                    oses = d;
                else if (!double.TryParse(osesValue.ToString(), out oses))
                    continue;

                if (!normOsesRanges.ContainsKey(normId))
                {
                    normOsesRanges[normId] = (oses, oses);
                }
                else
                {
                    var current = normOsesRanges[normId];
                    normOsesRanges[normId] = (
                        Math.Min(current.Min, oses),
                        Math.Max(current.Max, oses)
                    );
                }
            }

            // Создаем функции для каждой нормы
            foreach (var normId in normNumbers)
            {
                try
                {
                    // Получаем функцию из хранилища
                    var normFunc = _normStorage.GetNormFunction(normId);
                    
                    if (normFunc != null)
                    {
                        normFunctions[normId] = normFunc;
                        Log.Debug("Функция нормы {NormId} создана", normId);
                        
                        // Логируем диапазон, если он есть
                        if (normOsesRanges.TryGetValue(normId, out var range))
                        {
                            Log.Debug("Норма {NormId}: диапазон осей [{Min:F1} - {Max:F1}]", 
                                normId, range.Min, range.Max);
                        }
                    }
                    else
                    {
                        Log.Warning("Не удалось создать функцию для нормы {NormId}", normId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка создания функции для нормы {NormId}", normId);
                }
            }

            Log.Information("Создано {Count} функций норм", normFunctions.Count);
            return normFunctions;
        }

        #endregion

        #region Интерполяция и расчеты

        /// <summary>
        /// Применяет интерполяцию норм и выполняет расчеты
        /// Python: _interpolate_and_calculate(), data_analyzer.py, line 45
        /// </summary>
        private DataFrame InterpolateAndCalculate(
            DataFrame routesData, 
            Dictionary<string, Func<double, double>> normFunctions)
        {
            Log.Debug("Применяем интерполяцию для {Count} строк", routesData.Rows.Count);

            // Создаем копию DataFrame
            var result = routesData.Clone();

            // Добавляем новые колонки если их нет
            if (!result.Columns.Any(c => c.Name == "Норма интерполированная"))
            {
                result.Columns.Add(new DoubleDataFrameColumn("Норма интерполированная"));
            }
            if (!result.Columns.Any(c => c.Name == "Отклонение, %"))
            {
                result.Columns.Add(new DoubleDataFrameColumn("Отклонение, %"));
            }
            if (!result.Columns.Any(c => c.Name == "Статус"))
            {
                result.Columns.Add(new StringDataFrameColumn("Статус"));
            }

            // Получаем колонки
            var normCol = result.Columns["Номер нормы"];
            var osesCol = result.Columns["ОСИ"];
            var factUdCol = result.Columns["Факт уд"];
            var normInterpolCol = result.Columns["Норма интерполированная"];
            var deviationCol = result.Columns["Отклонение, %"];
            var statusCol = result.Columns["Статус"];

            int interpolatedCount = 0;

            // Обрабатываем каждую строку
            for (long i = 0; i < result.Rows.Count; i++)
            {
                var normId = normCol[i]?.ToString();
                if (string.IsNullOrEmpty(normId) || !normFunctions.ContainsKey(normId))
                    continue;

                // Получаем значение осей
                var osesValue = osesCol[i];
                if (osesValue == null)
                    continue;

                double oses;
                if (osesValue is double d)
                    oses = d;
                else if (!double.TryParse(osesValue.ToString(), out oses))
                    continue;

                // Получаем фактическое значение
                var factUdValue = factUdCol[i];
                if (factUdValue == null)
                    continue;

                double factUd;
                if (factUdValue is double fd)
                    factUd = fd;
                else if (!double.TryParse(factUdValue.ToString(), out factUd))
                    continue;

                // Интерполируем норму
                try
                {
                    var normFunc = normFunctions[normId];
                    double interpolatedNorm = normFunc(oses);

                    // Сохраняем интерполированную норму
                    normInterpolCol[i] = interpolatedNorm;

                    // Рассчитываем отклонение
                    double deviation = interpolatedNorm > 0 
                        ? ((factUd - interpolatedNorm) / interpolatedNorm) * 100.0 
                        : 0.0;

                    deviationCol[i] = deviation;

                    // Определяем статус
                    string status = ClassifyStatus(deviation);
                    statusCol[i] = status;

                    interpolatedCount++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Ошибка интерполяции для нормы {NormId}, оси {Oses}", 
                        normId, oses);
                }
            }

            Log.Information("Интерполировано {Count} записей", interpolatedCount);
            return result;
        }

        /// <summary>
        /// Классифицирует статус на основе отклонения
        /// Python: StatusClassifier.classify_status(), utils.py
        /// </summary>
        private string ClassifyStatus(double deviationPercent)
        {
            // Пороги: -5% и +5%
            const double economyThreshold = -5.0;
            const double overrunThreshold = 5.0;

            if (deviationPercent < economyThreshold)
                return "Экономия";
            else if (deviationPercent > overrunThreshold)
                return "Перерасход";
            else
                return "Норма";
        }

        #endregion

        #region Статистика

        /// <summary>
        /// Рассчитывает статистику для проанализированных данных
        /// Python: calculate_statistics(), data_analyzer.py, line 60
        /// </summary>
        public Dictionary<string, object> CalculateStatistics(DataFrame analyzedData)
        {
            Log.Debug("Расчет статистики для {Count} строк", analyzedData.Rows.Count);

            var stats = new Dictionary<string, object>();

            try
            {
                // Общее количество
                stats["Всего записей"] = analyzedData.Rows.Count;

                // Статистика по статусам
                var statusCol = analyzedData.Columns["Статус"];
                var statusCounts = new Dictionary<string, int>();

                for (long i = 0; i < analyzedData.Rows.Count; i++)
                {
                    var status = statusCol[i]?.ToString() ?? "Неизвестно";
                    if (!statusCounts.ContainsKey(status))
                        statusCounts[status] = 0;
                    statusCounts[status]++;
                }

                stats["По статусам"] = statusCounts;

                // Статистика отклонений
                var deviationCol = analyzedData.Columns["Отклонение, %"] as DoubleDataFrameColumn;
                if (deviationCol != null)
                {
                    var deviations = new List<double>();
                    for (long i = 0; i < deviationCol.Length; i++)
                    {
                        var val = deviationCol[i];
                        if (val.HasValue && !double.IsNaN(val.Value) && !double.IsInfinity(val.Value))
                        {
                            deviations.Add(val.Value);
                        }
                    }

                    if (deviations.Count > 0)
                    {
                        stats["Среднее отклонение, %"] = deviations.Average();
                        stats["Мин отклонение, %"] = deviations.Min();
                        stats["Макс отклонение, %"] = deviations.Max();
                    }
                }

                Log.Debug("Статистика рассчитана: {Count} метрик", stats.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка расчета статистики");
            }

            return stats;
        }

        #endregion
    }

    /// <summary>
    /// Применитель коэффициентов к данным маршрутов
    /// Python: class CoefficientsApplier, data_analyzer.py
    /// 
    /// TODO Чат 5: Полная реализация с фильтром локомотивов
    /// </summary>
    public class CoefficientsApplier
    {
        private readonly CoefficientsManager _coefficientsManager;

        public CoefficientsApplier(CoefficientsManager coefficientsManager)
        {
            _coefficientsManager = coefficientsManager ?? 
                throw new ArgumentNullException(nameof(coefficientsManager));
            Log.Information("CoefficientsApplier инициализирован");
        }

        /// <summary>
        /// Применяет коэффициенты к DataFrame
        /// TODO Чат 5: Реализация с фильтром локомотивов
        /// </summary>
        public DataFrame ApplyCoefficients(DataFrame routesData, LocomotiveFilter? filter = null)
        {
            Log.Debug("Применение коэффициентов (заглушка для Чата 5)");
            
            // TODO Чат 5: Реальная реализация
            return routesData;
        }
    }
}
