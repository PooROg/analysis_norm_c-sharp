// GUI/Components/ControlSection.cs
// Панель управления анализом (центральная секция)
// Python: часть gui/interface.py, класс NormsAnalyzerGUI
// ЧАТ 3: Базовая версия с выбором участка

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Serilog;

namespace AnalysisNorm.GUI.Components
{
    /// <summary>
    /// Секция управления анализом маршрутов
    /// Python: часть gui/interface.py (центральная панель)
    /// 
    /// ЧАТ 3: Базовая структура
    /// - Выбор участка
    /// - Выбор нормы
    /// - Кнопки анализа и фильтра
    /// </summary>
    public class ControlSection : UserControl
    {
        #region Поля

        private ComboBox _sectionComboBox;
        private ComboBox _normComboBox;
        private CheckBox _singleSectionCheckBox;
        private Button _analyzeButton;
        private Button _filterLocomotivesButton;
        private TextBox _statisticsTextBox;

        #endregion

        #region События

        /// <summary>
        /// Событие: запрос на анализ участка
        /// </summary>
        public event Action<string, bool>? OnAnalyzeRequested;

        /// <summary>
        /// Событие: запрос на фильтрацию локомотивов
        /// </summary>
        public event Action? OnFilterLocomotivesRequested;

        #endregion

        #region Конструктор

        public ControlSection()
        {
            InitializeComponent();
            SetupUI();

            Log.Debug("ControlSection создан");
        }

        #endregion

        #region Инициализация компонентов

        private void InitializeComponent()
        {
            // GroupBox контейнер
            var groupBox = new GroupBox
            {
                Text = "Управление",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // TableLayoutPanel для организации элементов
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(5)
            };

            // Настройка колонок: метки и контролы
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Настройка строк
            for (int i = 0; i < 6; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Последняя строка - статистика

            int row = 0;

            #region Выбор участка

            // Python: self.section_var = tk.StringVar()
            layout.Controls.Add(new Label
            {
                Text = "Участок:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            }, 0, row);

            _sectionComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _sectionComboBox.SelectedIndexChanged += SectionComboBox_SelectedIndexChanged;
            layout.Controls.Add(_sectionComboBox, 1, row);
            row++;

            #endregion

            #region Выбор нормы

            // Python: self.norm_var = tk.StringVar()
            layout.Controls.Add(new Label
            {
                Text = "Норма:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            }, 0, row);

            _normComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            layout.Controls.Add(_normComboBox, 1, row);
            row++;

            #endregion

            #region Чекбокс "Только один участок"

            // Python: self.single_section_mode = tk.BooleanVar()
            _singleSectionCheckBox = new CheckBox
            {
                Text = "Только один участок",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Checked = false,
                Enabled = false
            };
            layout.Controls.Add(_singleSectionCheckBox, 1, row);
            row++;

            #endregion

            #region Кнопка "Анализировать"

            // Python: analyze_btn = ttk.Button(..., command=self._on_analyze_click)
            _analyzeButton = new Button
            {
                Text = "Анализировать",
                Dock = DockStyle.Fill,
                Enabled = false,
                Height = 30
            };
            _analyzeButton.Click += AnalyzeButton_Click;
            layout.Controls.Add(_analyzeButton, 1, row);
            row++;

            #endregion

            #region Кнопка "Фильтр локомотивов"

            // Python: filter_btn = ttk.Button(..., command=self._on_filter_locomotives_click)
            _filterLocomotivesButton = new Button
            {
                Text = "Фильтр локомотивов",
                Dock = DockStyle.Fill,
                Enabled = false,
                Height = 30
            };
            _filterLocomotivesButton.Click += FilterLocomotivesButton_Click;
            layout.Controls.Add(_filterLocomotivesButton, 1, row);
            row++;

            #endregion

            #region Разделитель

            layout.Controls.Add(new Label
            {
                Text = "",
                Dock = DockStyle.Fill
            }, 0, row);
            row++;

            #endregion

            #region Статистика

            // Python: self.stats_text = tk.Text(...)
            layout.Controls.Add(new Label
            {
                Text = "Статистика:",
                TextAlign = ContentAlignment.TopLeft,
                Dock = DockStyle.Fill
            }, 0, row);

            _statisticsTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                Font = new Font("Consolas", 9F)
            };
            layout.Controls.Add(_statisticsTextBox, 1, row);

            #endregion

            groupBox.Controls.Add(layout);
            Controls.Add(groupBox);
        }

        private void SetupUI()
        {
            // Начальное состояние
            UpdateStatistics("Загрузите файлы маршрутов для начала работы");
        }

        #endregion

        #region Обработчики событий

        private void SectionComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Активируем кнопку анализа при выборе участка
            _analyzeButton.Enabled = !string.IsNullOrEmpty(_sectionComboBox.Text);

            Log.Debug("Выбран участок: {Section}", _sectionComboBox.Text);
        }

        /// <summary>
        /// Обработчик нажатия кнопки "Анализировать"
        /// Python: def _on_analyze_click(self), interface.py line 330
        /// </summary>
        private void AnalyzeButton_Click(object? sender, EventArgs e)
        {
            string selectedSection = _sectionComboBox.Text;
            bool singleSectionMode = _singleSectionCheckBox.Checked;

            if (string.IsNullOrEmpty(selectedSection))
            {
                MessageBox.Show("Выберите участок для анализа",
                    "Предупреждение",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Log.Information("Запрос на анализ участка: {Section}, одиночный режим: {Mode}",
                selectedSection, singleSectionMode);

            // Генерируем событие
            OnAnalyzeRequested?.Invoke(selectedSection, singleSectionMode);
        }

        /// <summary>
        /// Обработчик нажатия кнопки "Фильтр локомотивов"
        /// Python: def _on_filter_locomotives_click(self), interface.py line 350
        /// </summary>
        private void FilterLocomotivesButton_Click(object? sender, EventArgs e)
        {
            Log.Information("Запрос на открытие диалога фильтра локомотивов");

            // TODO Чат 5: Открыть LocomotiveSelectorDialog
            OnFilterLocomotivesRequested?.Invoke();
        }

        #endregion

        #region Публичные методы

        /// <summary>
        /// Обновляет список участков в выпадающем списке
        /// Python: section_dropdown['values'] = sections
        /// </summary>
        public void UpdateSectionsList(List<string> sections)
        {
            _sectionComboBox.Items.Clear();

            if (sections == null || sections.Count == 0)
            {
                _sectionComboBox.Enabled = false;
                _analyzeButton.Enabled = false;
                _singleSectionCheckBox.Enabled = false;
                return;
            }

            foreach (var section in sections)
            {
                _sectionComboBox.Items.Add(section);
            }

            _sectionComboBox.Enabled = true;
            _singleSectionCheckBox.Enabled = true;

            // Выбираем первый элемент
            if (_sectionComboBox.Items.Count > 0)
                _sectionComboBox.SelectedIndex = 0;

            Log.Information("Список участков обновлен: {Count} элементов", sections.Count);
        }

        /// <summary>
        /// Обновляет список норм в выпадающем списке
        /// Python: norm_dropdown['values'] = norms
        /// </summary>
        public void UpdateNormsList(List<string> norms)
        {
            _normComboBox.Items.Clear();

            if (norms == null || norms.Count == 0)
            {
                _normComboBox.Enabled = false;
                return;
            }

            foreach (var norm in norms)
            {
                _normComboBox.Items.Add(norm);
            }

            _normComboBox.Enabled = true;

            // Выбираем первый элемент
            if (_normComboBox.Items.Count > 0)
                _normComboBox.SelectedIndex = 0;

            Log.Information("Список норм обновлен: {Count} элементов", norms.Count);
        }

        /// <summary>
        /// Обновляет текст статистики
        /// Python: self.stats_text.delete('1.0', tk.END); self.stats_text.insert('1.0', text)
        /// </summary>
        public void UpdateStatistics(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatistics(text)));
                return;
            }

            _statisticsTextBox.Text = text;
            Log.Debug("Статистика обновлена");
        }

        /// <summary>
        /// Активирует/деактивирует кнопку фильтра локомотивов
        /// </summary>
        public void EnableFilterButton(bool enabled)
        {
            _filterLocomotivesButton.Enabled = enabled;
        }

        /// <summary>
        /// Очищает все выборы и деактивирует элементы
        /// </summary>
        public void Clear()
        {
            _sectionComboBox.Items.Clear();
            _sectionComboBox.Enabled = false;

            _normComboBox.Items.Clear();
            _normComboBox.Enabled = false;

            _analyzeButton.Enabled = false;
            _singleSectionCheckBox.Enabled = false;
            _singleSectionCheckBox.Checked = false;

            UpdateStatistics("");

            Log.Debug("ControlSection очищен");
        }

        #endregion
    }
}
