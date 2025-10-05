// GUI/Components/VisualizationSection.cs
// Секция визуализации графиков
// Мигрировано из: gui/interface.py (часть с графиком)
// ЧАТ 4: Базовая интеграция ScottPlot

using System;
using System.Drawing;
using System.Windows.Forms;
using ScottPlot;
using Serilog;

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

        private ScottPlot.FormsPlot _formsPlot;
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
            // FormsPlot - основной контрол для графика
            _formsPlot = new ScottPlot.FormsPlot
            {
                Dock = DockStyle.Fill
            };

            // Текстовое поле для статистики
            _statisticsTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                BackColor = Color.White,
                Height = 120
            };

            // Кнопка экспорта
            _exportButton = new Button
            {
                Text = "Экспорт PNG",
                Width = 120,
                Height = 30,
                Enabled = false
            };
            _exportButton.Click += ExportButton_Click;

            // Кнопка информации о нормах
            _infoButton = new Button
            {
                Text = "Информация о нормах",
                Width = 150,
                Height = 30,
                Enabled = false
            };
            _infoButton.Click += InfoButton_Click;
        }

        #endregion

        #region Размещение компонентов

        private void SetupLayout()
        {
            // Панель для кнопок
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(5)
            };
            buttonPanel.Controls.Add(_exportButton);
            buttonPanel.Controls.Add(_infoButton);

            // Панель для статистики
            var statsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 130,
                Padding = new Padding(5)
            };
            statsPanel.Controls.Add(_statisticsTextBox);
            _statisticsTextBox.Dock = DockStyle.Fill;

            // Добавляем компоненты
            this.Controls.Add(_formsPlot);      // График занимает оставшееся место
            this.Controls.Add(statsPanel);      // Статистика внизу
            this.Controls.Add(buttonPanel);     // Кнопки самые внизу

            // Порядок важен! Dock работает от последнего к первому
        }

        #endregion

        #region Публичные методы

        /// <summary>
        /// Отображает график на FormsPlot контроле
        /// </summary>
        public void DisplayPlot(Plot plot)
        {
            if (plot == null)
            {
                Log.Warning("Попытка отобразить null график");
                return;
            }

            try
            {
                // Очищаем текущий график
                _formsPlot.Reset();

                // Копируем содержимое из созданного Plot
                _formsPlot.Plot.Clear();
                
                // Копируем все plottables
                foreach (var plottable in plot.GetPlottables())
                {
                    _formsPlot.Plot.Add(plottable);
                }

                // Копируем настройки
                _formsPlot.Plot.Title(plot.Title());
                _formsPlot.Plot.XLabel(plot.XAxis.Label.Text);
                _formsPlot.Plot.YLabel(plot.YAxis.Label.Text);

                // Автомасштабирование
                _formsPlot.Plot.AxisAuto();

                // Обновление отображения
                _formsPlot.Refresh();

                // Активируем кнопки
                _exportButton.Enabled = true;
                _infoButton.Enabled = true;

                Log.Information("График отображен на FormsPlot");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка отображения графика");
                MessageBox.Show($"Ошибка отображения графика: {ex.Message}", 
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Отображает статистику анализа
        /// </summary>
        public void DisplayStatistics(System.Collections.Generic.Dictionary<string, object>? statistics)
        {
            if (statistics == null || statistics.Count == 0)
            {
                _statisticsTextBox.Text = "Статистика недоступна";
                return;
            }

            try
            {
                var text = "=== СТАТИСТИКА АНАЛИЗА ===" + Environment.NewLine + Environment.NewLine;

                foreach (var (key, value) in statistics)
                {
                    if (value is System.Collections.Generic.Dictionary<string, int> dict)
                    {
                        text += $"{key}:" + Environment.NewLine;
                        foreach (var (subKey, subValue) in dict)
                        {
                            text += $"  {subKey}: {subValue}" + Environment.NewLine;
                        }
                    }
                    else if (value is double doubleValue)
                    {
                        text += $"{key}: {doubleValue:F2}" + Environment.NewLine;
                    }
                    else
                    {
                        text += $"{key}: {value}" + Environment.NewLine;
                    }
                }

                _statisticsTextBox.Text = text;
                Log.Debug("Статистика отображена");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка отображения статистики");
                _statisticsTextBox.Text = "Ошибка отображения статистики";
            }
        }

        /// <summary>
        /// Очищает секцию визуализации
        /// </summary>
        public void Clear()
        {
            _formsPlot.Reset();
            _statisticsTextBox.Clear();
            _exportButton.Enabled = false;
            _infoButton.Enabled = false;
            Log.Debug("VisualizationSection очищена");
        }

        #endregion

        #region Обработчики событий

        private void ExportButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Диалог сохранения файла
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "PNG Image|*.png|All Files|*.*",
                    Title = "Сохранить график",
                    DefaultExt = "png",
                    FileName = $"график_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    // Сохраняем график
                    _formsPlot.Plot.SaveFig(saveDialog.FileName, width: 1920, height: 1080);

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
            // TODO Чат 5: Открытие диалога с информацией о нормах
            MessageBox.Show("Информация о нормах будет реализована в Чате 5",
                "TODO", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Log.Debug("Кнопка 'Информация о нормах' нажата (заглушка)");
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Возвращает текущий FormsPlot контрол (для расширенного доступа)
        /// </summary>
        public ScottPlot.FormsPlot GetFormsPlot()
        {
            return _formsPlot;
        }

        #endregion
    }
}
