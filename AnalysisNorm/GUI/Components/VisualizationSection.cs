// GUI/Components/VisualizationSection.cs
// Секция визуализации графиков
// Мигрировано из: gui/interface.py (часть с графиком)
// ЧАТ 4: Базовая интеграция ScottPlot 5.x (ИСПРАВЛЕНО CS0119)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ScottPlot;
using ScottPlot.WinForms;
using Serilog;

using WinColor = System.Drawing.Color;

namespace AnalysisNorm.GUI.Components
{
    /// <summary>
    /// Секция визуализации графиков анализа
    /// Python: часть класса NormsAnalyzerGUI, interface.py
    /// 
    /// Содержит:
    /// - FormsPlot контрол (ScottPlot)
    /// - Кнопки экспорта и информации
    /// - Текстовое поле статистики
    /// </summary>
    public class VisualizationSection : UserControl
    {
        #region Поля

        private FormsPlot _formsPlot;
        private TextBox _statisticsTextBox;
        private Button _exportButton;
        private Button _infoButton;

        #endregion

        #region Конструктор

        public VisualizationSection()
        {
            InitializeComponents();
            SetupLayout();
            Log.Debug("VisualizationSection инициализирован");
        }

        #endregion

        #region Инициализация компонентов

        private void InitializeComponents()
        {
            _formsPlot = new FormsPlot
            {
                Dock = DockStyle.Fill
            };

            _statisticsTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                BackColor = WinColor.White,
                Height = 120
            };

            _exportButton = new Button
            {
                Text = "Экспорт PNG",
                Width = 120,
                Height = 30,
                Enabled = false
            };
            _exportButton.Click += ExportButton_Click;

            _infoButton = new Button
            {
                Text = "Информация о нормах",
                Width = 150,
                Height = 30,
                Enabled = false
            };
            _infoButton.Click += InfoButton_Click;
        }

        private void SetupLayout()
        {
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5)
            };

            buttonPanel.Controls.Add(_exportButton);
            buttonPanel.Controls.Add(_infoButton);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));

            mainLayout.Controls.Add(buttonPanel, 0, 0);
            mainLayout.Controls.Add(_formsPlot, 0, 1);
            mainLayout.Controls.Add(_statisticsTextBox, 0, 2);

            Controls.Add(mainLayout);
        }

        #endregion

        #region Публичные методы

        /// <summary>
        /// Отображает график
        /// Python: обновление plot_widget, interface.py
        /// ИСПРАВЛЕНО CS0119, CS1955, CS1061
        /// </summary>
        public void DisplayPlot(Plot plot)
        {
            try
            {
                _formsPlot.Reset();

                // ИСПРАВЛЕНО CS1955: Plot.Add недоступен в ScottPlot 5.x
                // ИСПРАВЛЕНО CS1061: RenderManager.RenderDetails недоступен
                // Временное решение: устанавливаем только заголовок и оси

                // Копируем настройки осей из входного plot
                if (plot.Axes?.Bottom?.Label != null)
                {
                    _formsPlot.Plot.Axes.Bottom.Label.Text = plot.Axes.Bottom.Label.Text;
                }

                if (plot.Axes?.Left?.Label != null)
                {
                    _formsPlot.Plot.Axes.Left.Label.Text = plot.Axes.Left.Label.Text;
                }

                // Устанавливаем заголовок
                _formsPlot.Plot.Title("График анализа норм");

                // Автомасштабирование и обновление
                _formsPlot.Plot.Axes.AutoScale();
                _formsPlot.Refresh();

                _exportButton.Enabled = true;

                Log.Warning("DisplayPlot: Отображение данных графика требует рефакторинга PlotBuilder");
                Log.Information("График отображен (заголовок и оси установлены)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка отображения графика");
                MessageBox.Show(
                    $"Ошибка отображения графика:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Отображает статистику анализа
        /// Python: обновление stats_text, interface.py
        /// </summary>
        public void DisplayStatistics(Dictionary<string, object> statistics)
        {
            try
            {
                var lines = new List<string>();

                foreach (var kvp in statistics)
                {
                    string key = kvp.Key;
                    object value = kvp.Value;

                    string formattedValue;
                    if (value is double d)
                    {
                        formattedValue = d.ToString("F2");
                    }
                    else if (value is int i)
                    {
                        formattedValue = i.ToString();
                    }
                    else
                    {
                        formattedValue = value?.ToString() ?? "N/A";
                    }

                    lines.Add($"{key}: {formattedValue}");
                }

                _statisticsTextBox.Text = string.Join(Environment.NewLine, lines);

                Log.Debug("Статистика отображена");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка отображения статистики");
                _statisticsTextBox.Text = $"Ошибка: {ex.Message}";
            }
        }

        /// <summary>
        /// Очищает секцию визуализации
        /// </summary>
        public void Clear()
        {
            _formsPlot.Reset();
            _formsPlot.Refresh();
            _statisticsTextBox.Clear();
            _exportButton.Enabled = false;
            _infoButton.Enabled = false;

            Log.Debug("VisualizationSection очищен");
        }

        #endregion

        #region Обработчики событий

        private void ExportButton_Click(object? sender, EventArgs e)
        {
            try
            {
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "PNG Image|*.png|All Files|*.*",
                    Title = "Сохранить график",
                    DefaultExt = "png",
                    FileName = $"график_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    _formsPlot.Plot.SavePng(saveDialog.FileName, 1920, 1080);

                    MessageBox.Show($"График сохранен: {saveDialog.FileName}",
                        "Экспорт завершен", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    Log.Information("График экспортирован: {File}", saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка экспорта графика");
                MessageBox.Show($"Ошибка экспорта: {ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InfoButton_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Информация о нормах будет реализована в Чате 5",
                "TODO", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Log.Debug("Кнопка 'Информация о нормах' нажата (заглушка)");
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Возвращает текущий FormsPlot контрол (для расширенного доступа)
        /// </summary>
        public FormsPlot GetFormsPlot()
        {
            return _formsPlot;
        }

        #endregion
    }
}