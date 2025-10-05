// Analysis/PlotBuilder.cs
// Построение интерактивных графиков анализа норм
// Мигрировано из: analysis/visualization.py
// ЧАТ 4: Базовая версия с ScottPlot (вместо Plotly)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.Data.Analysis;
using ScottPlot;
using Serilog;

namespace AnalysisNorm.Analysis
{
    /// <summary>
    /// Построитель графиков для анализа норм
    /// Python: class PlotBuilder, visualization.py
    /// 
    /// ВАЖНО: Вместо Plotly используем ScottPlot
    /// Упрощения для Чата 4:
    /// - Базовый двухпанельный график
    /// - Стандартная интерактивность ScottPlot
    /// - Без модальных окон (будет в Чате 6)
    /// </summary>
    public class PlotBuilder
    {
        #region Константы

        // Цвета статусов (как в Python)
        private static readonly Color ColorEconomy = Color.FromArgb(0, 128, 0);      // Зеленый
        private static readonly Color ColorNormal = Color.FromArgb(255, 165, 0);     // Оранжевый  
        private static readonly Color ColorOverrun = Color.FromArgb(255, 0, 0);      // Красный

        // Размеры маркеров
        private const float MarkerSize = 8f;
        private const float LineWidth = 2f;

        #endregion

        #region Основной метод создания графика

        /// <summary>
        /// Создает интерактивный график для анализа участка
        /// Python: create_interactive_plot(), visualization.py, line 45
        /// </summary>
        /// <param name="sectionName">Название участка</param>
        /// <param name="analyzedData">Проанализированные данные</param>
        /// <param name="normFunctions">Функции норм</param>
        /// <param name="specificNormId">Конкретная норма (опционально)</param>
        /// <param name="singleSectionOnly">Только один участок</param>
        /// <returns>ScottPlot.Plot объект</returns>
        public Plot CreateInteractivePlot(
            string sectionName,
            DataFrame analyzedData,
            Dictionary<string, Func<double, double>> normFunctions,
            string? specificNormId = null,
            bool singleSectionOnly = false)
        {
            Log.Information("Создание графика для участка {Section}", sectionName);

            try
            {
                // Создаем основной plot
                var plt = new Plot();

                // Заголовок
                string title = BuildPlotTitle(sectionName, specificNormId, singleSectionOnly);
                plt.Title(title);
                plt.XLabel("Нагрузка на ось (тонн)");
                plt.YLabel("Удельный расход (кВт·ч/1000 ткм брутто)");

                // Добавляем кривые норм
                AddNormCurves(plt, normFunctions, analyzedData);

                // Добавляем точки маршрутов
                AddRoutePoints(plt, analyzedData);

                // Настройка легенды
                plt.Legend(location: Alignment.UpperRight);

                // Включение интерактивности
                plt.Grid(enable: true);

                Log.Information("График создан: {Count} точек", analyzedData.Rows.Count);
                return plt;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка создания графика для участка {Section}", sectionName);
                
                // Возвращаем пустой график с сообщением об ошибке
                var errorPlot = new Plot();
                errorPlot.Title($"Ошибка: {ex.Message}");
                return errorPlot;
            }
        }

        #endregion

        #region Построение заголовка

        /// <summary>
        /// Строит заголовок графика
        /// Python: часть create_interactive_plot(), visualization.py
        /// </summary>
        private string BuildPlotTitle(string sectionName, string? normId, bool singleSectionOnly)
        {
            string title = $"Участок: {sectionName}";

            if (!string.IsNullOrEmpty(normId))
            {
                title += $" | Норма: {normId}";
            }

            if (singleSectionOnly)
            {
                title += " | Только один участок";
            }

            return title;
        }

        #endregion

        #region Добавление кривых норм

        /// <summary>
        /// Добавляет кривые норм на график
        /// Python: часть создания traces для норм, visualization.py
        /// </summary>
        private void AddNormCurves(
            Plot plt,
            Dictionary<string, Func<double, double>> normFunctions,
            DataFrame analyzedData)
        {
            Log.Debug("Добавление {Count} кривых норм", normFunctions.Count);

            // Определяем диапазон осей из данных
            var (minOses, maxOses) = GetOsesRange(analyzedData);

            if (minOses >= maxOses)
            {
                Log.Warning("Некорректный диапазон осей: [{Min}, {Max}]", minOses, maxOses);
                return;
            }

            // Расширяем диапазон на 10% с каждой стороны
            double range = maxOses - minOses;
            minOses -= range * 0.1;
            maxOses += range * 0.1;

            // Создаем массив точек для кривых
            int numPoints = 100;
            double[] osesArray = new double[numPoints];
            for (int i = 0; i < numPoints; i++)
            {
                osesArray[i] = minOses + (maxOses - minOses) * i / (numPoints - 1);
            }

            // Добавляем кривую для каждой нормы
            int normIndex = 0;
            foreach (var (normId, normFunc) in normFunctions)
            {
                try
                {
                    // Вычисляем значения нормы
                    double[] normValues = new double[numPoints];
                    for (int i = 0; i < numPoints; i++)
                    {
                        normValues[i] = normFunc(osesArray[i]);
                    }

                    // Выбираем цвет (циклически)
                    Color lineColor = GetNormColor(normIndex);

                    // Добавляем линию
                    var signal = plt.AddScatter(osesArray, normValues, lineColor, lineWidth: LineWidth);
                    signal.Label = $"Норма {normId}";
                    signal.MarkerSize = 0;  // Только линия, без маркеров

                    normIndex++;
                    Log.Debug("Кривая нормы {NormId} добавлена", normId);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Ошибка добавления кривой нормы {NormId}", normId);
                }
            }
        }

        /// <summary>
        /// Возвращает диапазон значений осей из данных
        /// </summary>
        private (double Min, double Max) GetOsesRange(DataFrame data)
        {
            try
            {
                var osesCol = data.Columns["ОСИ"];
                double min = double.MaxValue;
                double max = double.MinValue;

                for (long i = 0; i < data.Rows.Count; i++)
                {
                    var osesValue = osesCol[i];
                    if (osesValue == null)
                        continue;

                    double oses;
                    if (osesValue is double d)
                        oses = d;
                    else if (!double.TryParse(osesValue.ToString(), out oses))
                        continue;

                    if (oses < min) min = oses;
                    if (oses > max) max = oses;
                }

                if (min == double.MaxValue || max == double.MinValue)
                {
                    // Возвращаем дефолтный диапазон
                    return (10.0, 30.0);
                }

                return (min, max);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка определения диапазона осей");
                return (10.0, 30.0);
            }
        }

        /// <summary>
        /// Возвращает цвет для кривой нормы
        /// </summary>
        private Color GetNormColor(int index)
        {
            // Палитра цветов для кривых норм
            Color[] palette = new[]
            {
                Color.Blue,
                Color.Purple,
                Color.DarkCyan,
                Color.DarkOrange,
                Color.Brown,
                Color.DarkGreen
            };

            return palette[index % palette.Length];
        }

        #endregion

        #region Добавление точек маршрутов

        /// <summary>
        /// Добавляет точки маршрутов на график
        /// Python: часть создания scatter traces, visualization.py
        /// </summary>
        private void AddRoutePoints(Plot plt, DataFrame data)
        {
            Log.Debug("Добавление точек маршрутов: {Count}", data.Rows.Count);

            try
            {
                // Группируем точки по статусам
                var pointsByStatus = GroupPointsByStatus(data);

                // Добавляем точки для каждого статуса
                foreach (var (status, points) in pointsByStatus)
                {
                    if (points.Count == 0)
                        continue;

                    // Получаем цвет для статуса
                    Color color = GetStatusColor(status);

                    // Преобразуем в массивы
                    double[] x = points.Select(p => p.Oses).ToArray();
                    double[] y = points.Select(p => p.FactUd).ToArray();

                    // Добавляем scatter
                    var scatter = plt.AddScatter(x, y, color, markerSize: MarkerSize);
                    scatter.Label = $"{status} ({points.Count})";
                    scatter.LineWidth = 0;  // Только маркеры, без линий

                    Log.Debug("Добавлено {Count} точек со статусом {Status}", points.Count, status);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка добавления точек маршрутов");
            }
        }

        /// <summary>
        /// Группирует точки по статусам
        /// </summary>
        private Dictionary<string, List<RoutePoint>> GroupPointsByStatus(DataFrame data)
        {
            var groups = new Dictionary<string, List<RoutePoint>>
            {
                ["Экономия"] = new List<RoutePoint>(),
                ["Норма"] = new List<RoutePoint>(),
                ["Перерасход"] = new List<RoutePoint>()
            };

            try
            {
                var osesCol = data.Columns["ОСИ"];
                var factUdCol = data.Columns["Факт уд"];
                var statusCol = data.Columns["Статус"];
                var routeNumCol = data.Columns["Маршрут №"];
                var tripDateCol = data.Columns["Дата поездки"];

                for (long i = 0; i < data.Rows.Count; i++)
                {
                    // Извлекаем значения
                    var osesValue = osesCol[i];
                    var factUdValue = factUdCol[i];
                    var status = statusCol[i]?.ToString() ?? "Норма";

                    if (osesValue == null || factUdValue == null)
                        continue;

                    // Парсим числа
                    if (!double.TryParse(osesValue.ToString(), out double oses))
                        continue;
                    if (!double.TryParse(factUdValue.ToString(), out double factUd))
                        continue;

                    // Создаем точку
                    var point = new RoutePoint
                    {
                        Oses = oses,
                        FactUd = factUd,
                        Status = status,
                        RouteNumber = routeNumCol[i]?.ToString() ?? "N/A",
                        TripDate = tripDateCol[i]?.ToString() ?? "N/A"
                    };

                    // Добавляем в группу
                    if (groups.ContainsKey(status))
                    {
                        groups[status].Add(point);
                    }
                    else
                    {
                        // Неизвестный статус → в "Норма"
                        groups["Норма"].Add(point);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка группировки точек по статусам");
            }

            return groups;
        }

        /// <summary>
        /// Возвращает цвет для статуса
        /// Python: STATUS_COLORS, visualization.py
        /// </summary>
        private Color GetStatusColor(string status)
        {
            return status switch
            {
                "Экономия" => ColorEconomy,
                "Перерасход" => ColorOverrun,
                _ => ColorNormal
            };
        }

        #endregion

        #region Вспомогательные классы

        /// <summary>
        /// Точка маршрута для графика
        /// </summary>
        private class RoutePoint
        {
            public double Oses { get; set; }
            public double FactUd { get; set; }
            public string Status { get; set; } = string.Empty;
            public string RouteNumber { get; set; } = string.Empty;
            public string TripDate { get; set; } = string.Empty;
        }

        #endregion
    }
}
