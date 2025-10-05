// Analysis/PlotBuilder.cs
// Построение интерактивных графиков анализа норм
// Мигрировано из: analysis/visualization.py
// ЧАТ 4: Базовая версия с ScottPlot 5.x (ИСПРАВЛЕНО)

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
    /// ВАЖНО: Используем ScottPlot 5.x API
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
            try
            {
                Log.Debug("Создание графика для участка: {Section}", sectionName);

                // Создаем новый Plot (ScottPlot 5.x)
                var plot = new Plot();

                // Заголовок
                string title = BuildPlotTitle(sectionName, specificNormId, singleSectionOnly);
                plot.Title(title);

                // ИСПРАВЛЕНО: ScottPlot 5.x - настройка осей
                plot.Axes.Bottom.Label.Text = "ОСЭС (тыс. тонно-км брутто)";
                plot.Axes.Left.Label.Text = "Расход (кг)";

                // Добавляем кривые норм
                AddNormCurves(plot, analyzedData, normFunctions, specificNormId);

                // Добавляем точки маршрутов
                AddRoutePoints(plot, analyzedData);

                // Автоматическое масштабирование
                plot.Axes.AutoScale();

                Log.Information("График создан успешно");
                return plot;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка создания графика");
                throw;
            }
        }

        #endregion

        #region Построение заголовка

        /// <summary>
        /// Строит заголовок графика
        /// Python: часть create_interactive_plot(), visualization.py, line 50
        /// </summary>
        private string BuildPlotTitle(string sectionName, string? specificNormId, bool singleSectionOnly)
        {
            string title = $"Анализ норм для участка: {sectionName}";

            if (!string.IsNullOrEmpty(specificNormId))
            {
                title += $" (Норма: {specificNormId})";
            }

            if (singleSectionOnly)
            {
                title += " [Только один участок]";
            }

            return title;
        }

        #endregion

        #region Добавление кривых норм

        /// <summary>
        /// Добавляет кривые норм на график
        /// Python: создание traces для норм, visualization.py, line 80
        /// </summary>
        private void AddNormCurves(
            Plot plot,
            DataFrame analyzedData,
            Dictionary<string, Func<double, double>> normFunctions,
            string? specificNormId)
        {
            if (normFunctions == null || normFunctions.Count == 0)
            {
                Log.Warning("Нет функций норм для отображения");
                return;
            }

            // Определяем диапазон ОСЭС
            var osesColumn = analyzedData.Columns["ОСЭС"];
            double minOses = osesColumn.Cast<double>().Min();
            double maxOses = osesColumn.Cast<double>().Max();

            // Генерируем 100 точек для плавной кривой
            double[] osesArray = new double[100];
            for (int i = 0; i < 100; i++)
            {
                osesArray[i] = minOses + (maxOses - minOses) * i / 99;
            }

            // Цвета для разных норм
            Color[] colors = { Color.Blue, Color.Purple, Color.Magenta, Color.Cyan };
            int colorIndex = 0;

            // Для каждой нормы
            foreach (var kvp in normFunctions)
            {
                string normId = kvp.Key;
                Func<double, double> normFunc = kvp.Value;

                // Фильтр по конкретной норме
                if (!string.IsNullOrEmpty(specificNormId) && normId != specificNormId)
                {
                    continue;
                }

                // Вычисляем значения нормы
                double[] normValues = new double[100];
                for (int i = 0; i < 100; i++)
                {
                    normValues[i] = normFunc(osesArray[i]);
                }

                // ИСПРАВЛЕНО: ScottPlot 5.x API
                Color lineColor = colors[colorIndex % colors.Length];
                var scatter = plot.Add.Scatter(osesArray, normValues);
                scatter.Color = lineColor;
                scatter.LineWidth = LineWidth;
                scatter.LegendText = $"Норма: {normId}";

                colorIndex++;
            }

            Log.Debug("Добавлено {Count} кривых норм", colorIndex);
        }

        #endregion

        #region Добавление точек маршрутов

        /// <summary>
        /// Добавляет точки маршрутов на график с цветовым кодированием по статусу
        /// Python: создание scatter traces, visualization.py, line 120
        /// </summary>
        private void AddRoutePoints(Plot plot, DataFrame analyzedData)
        {
            // Группируем точки по статусу
            var groupedPoints = GroupPointsByStatus(analyzedData);

            foreach (var group in groupedPoints)
            {
                string status = group.Key;
                List<RoutePoint> points = group.Value;

                if (points.Count == 0)
                {
                    continue;
                }

                // Извлекаем координаты
                double[] xData = points.Select(p => p.Oses).ToArray();
                double[] yData = points.Select(p => p.Rashod).ToArray();

                // Определяем цвет по статусу
                Color color = GetStatusColor(status);

                // ИСПРАВЛЕНО: ScottPlot 5.x API
                var scatter = plot.Add.Scatter(xData, yData);
                scatter.Color = color;
                scatter.MarkerSize = MarkerSize;
                scatter.LineWidth = 0; // Только точки, без линий
                scatter.LegendText = $"{status} ({points.Count} точек)";

                Log.Debug("Добавлено {Count} точек со статусом '{Status}'", points.Count, status);
            }
        }

        #endregion

        #region Группировка точек по статусу

        /// <summary>
        /// Группирует точки маршрутов по статусу
        /// Python: группировка через groupby, visualization.py, line 140
        /// </summary>
        private Dictionary<string, List<RoutePoint>> GroupPointsByStatus(DataFrame analyzedData)
        {
            var groups = new Dictionary<string, List<RoutePoint>>
            {
                ["Экономия"] = new List<RoutePoint>(),
                ["Норма"] = new List<RoutePoint>(),
                ["Перерасход"] = new List<RoutePoint>()
            };

            // ИСПРАВЛЕНО: Явное приведение long -> int
            int rowCount = (int)analyzedData.Rows.Count;

            for (int i = 0; i < rowCount; i++)
            {
                try
                {
                    var status = analyzedData["Статус"][i]?.ToString() ?? "Норма";
                    var oses = Convert.ToDouble(analyzedData["ОСЭС"][i]);
                    var rashod = Convert.ToDouble(analyzedData["Расход"][i]);

                    var point = new RoutePoint
                    {
                        Oses = oses,
                        Rashod = rashod,
                        Status = status
                    };

                    if (groups.ContainsKey(status))
                    {
                        groups[status].Add(point);
                    }
                    else
                    {
                        groups["Норма"].Add(point);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Ошибка обработки строки {Row}", i);
                }
            }

            return groups;
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Возвращает цвет для статуса
        /// Python: STATUS_COLORS, visualization.py, line 30
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
        /// Точка маршрута для построения графика
        /// </summary>
        private class RoutePoint
        {
            public double Oses { get; set; }
            public double Rashod { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        #endregion
    }
}