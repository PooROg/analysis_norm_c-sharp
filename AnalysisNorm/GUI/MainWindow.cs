// GUI/MainWindow.cs
// Главное окно приложения
// Мигрировано из: gui/interface.py
// ЧАТ 4: Интеграция DataAnalyzer, InteractiveAnalyzer, PlotBuilder, VisualizationSection

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.Analysis;
using Serilog;
using AnalysisNorm.Analysis;
using AnalysisNorm.Core;
using AnalysisNorm.GUI.Components;

namespace AnalysisNorm.GUI
{
    /// <summary>
    /// Главное окно приложения
    /// Python: class NormsAnalyzerGUI, gui/interface.py
    /// </summary>
    public partial class MainWindow : Form
    {
        #region Поля

        // Компоненты GUI
        private FileSection _fileSection;
        private ControlSection _controlSection;
        private VisualizationSection _visualizationSection;  // ЧАТ 4: Новый компонент

        // Панели для компонентов (из Чата 3)
        private Panel _fileSectionPanel;
        private Panel _controlSectionPanel;
        private Panel _visualSectionPanel;

        // Бэкенд компоненты (из Чата 3)
        private RouteProcessor _routeProcessor;
        private NormProcessor _normProcessor;
        private NormStorage _normStorage;

        // ЧАТ 4: Новые компоненты анализа
        private InteractiveAnalyzer _analyzer;
        private PlotBuilder _plotBuilder;

        // Статус бар (из Чата 1)
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;

        #endregion

        #region Конструктор

        public MainWindow()
        {
            InitializeComponents();
            SetupLayout();
            SetupMenuBar();
            
            Text = "Анализ норм расхода электроэнергии";
            Width = 1400;
            Height = 900;
            StartPosition = FormStartPosition.CenterScreen;

            Log.Information("MainWindow инициализирован");
        }

        #endregion

        #region Инициализация компонентов

        /// <summary>
        /// Инициализирует все компоненты
        /// ЧАТ 4: Добавлены InteractiveAnalyzer и PlotBuilder
        /// </summary>
        private void InitializeComponents()
        {
            // Инициализация логики (Чат 2, 3, 4)
            _normStorage = new NormStorage();
            _routeProcessor = new RouteProcessor();
            _normProcessor = new NormProcessor(_normStorage); // ИСПРАВЛЕНО: передаем _normStorage
            _analyzer = new InteractiveAnalyzer();
            _plotBuilder = new PlotBuilder();
            _analyzer.SetPlotBuilder(_plotBuilder);

            // GUI компоненты
            _fileSection = new FileSection();
            _controlSection = new ControlSection();
            _visualizationSection = new VisualizationSection();  // ЧАТ 4: Новый

            // Бэкенд (из Чата 3)
            _normStorage = new NormStorage();
            _routeProcessor = new RouteProcessor();
            _normProcessor = new NormProcessor(_normStorage);

            // ЧАТ 4: Анализаторы
            _analyzer = new InteractiveAnalyzer();
            _plotBuilder = new PlotBuilder();
            _analyzer.SetPlotBuilder(_plotBuilder);

            // Статус бар
            _statusLabel = new ToolStripStatusLabel("Готов");
            _statusStrip = new StatusStrip();
            _statusStrip.Items.Add(_statusLabel);

            // Подключение событий
            _fileSection.OnRoutesLoaded += FileSection_OnRoutesLoaded;
            _fileSection.OnNormsLoaded += FileSection_OnNormsLoaded;
            _fileSection.OnCoefficientsLoaded += FileSection_OnCoefficientsLoaded;
            
            _controlSection.OnAnalyzeRequested += ControlSection_OnAnalyzeRequested;  // ЧАТ 4: Реализация
            _controlSection.OnFilterLocomotivesRequested += ControlSection_OnFilterLocomotivesRequested;

            Log.Debug("Компоненты инициализированы");
        }

        #endregion

        #region Размещение компонентов

        /// <summary>
        /// Настраивает layout главного окна
        /// Python: _setup_ui(), interface.py
        /// ЧАТ 4: Добавлена VisualizationSection
        /// </summary>
        private void SetupLayout()
        {
            // Главный контейнер с тремя секциями
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(5)
            };

            // Пропорции колонок: 20% | 25% | 55%
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));

            // Панель для файлов (слева)
            _fileSectionPanel = CreateSectionPanel("Загрузка файлов", _fileSection);
            mainLayout.Controls.Add(_fileSectionPanel, 0, 0);

            // Панель для управления (центр)
            _controlSectionPanel = CreateSectionPanel("Управление анализом", _controlSection);
            mainLayout.Controls.Add(_controlSectionPanel, 1, 0);

            // ЧАТ 4: Панель для визуализации (справа)
            _visualSectionPanel = CreateSectionPanel("Визуализация", _visualizationSection);
            mainLayout.Controls.Add(_visualSectionPanel, 2, 0);

            // Добавляем на форму
            Controls.Add(mainLayout);
            Controls.Add(_statusStrip);

            Log.Debug("Layout настроен");
        }

        /// <summary>
        /// Создает панель секции с заголовком
        /// </summary>
        private Panel CreateSectionPanel(string title, Control content)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5)
            };

            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.LightGray
            };

            content.Dock = DockStyle.Fill;

            panel.Controls.Add(content);
            panel.Controls.Add(titleLabel);

            return panel;
        }

        #endregion

        #region Меню

        /// <summary>
        /// Настраивает меню приложения
        /// Python: _setup_menu(), interface.py
        /// </summary>
        private void SetupMenuBar()
        {
            var menuStrip = new MenuStrip();

            // Меню "Файл"
            var fileMenu = new ToolStripMenuItem("Файл");
            fileMenu.DropDownItems.Add("Экспорт в Excel", null, Menu_ExportToExcel);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Выход", null, (s, e) => Close());

            // Меню "Настройки"
            var settingsMenu = new ToolStripMenuItem("Настройки");
            settingsMenu.DropDownItems.Add("Управление нормами", null, Menu_ManageNorms);

            // Меню "О программе"
            var aboutMenu = new ToolStripMenuItem("О программе");
            aboutMenu.Click += Menu_About;

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(settingsMenu);
            menuStrip.Items.Add(aboutMenu);

            MainMenuStrip = menuStrip;
            Controls.Add(menuStrip);

            Log.Debug("Меню настроено");
        }

        #endregion

        #region Обработчики событий FileSection

        /// <summary>
        /// Обработчик загрузки HTML маршрутов
        /// Python: _on_load_routes_click(), interface.py line 280
        /// ЧАТ 4: Использует InteractiveAnalyzer
        /// </summary>
        private async void FileSection_OnRoutesLoaded(List<string> files)
        {
            Log.Information("Загрузка {Count} HTML файлов маршрутов", files.Count);
            UpdateStatusBar("Загрузка маршрутов...");

            try
            {
                // ЧАТ 4: Используем InteractiveAnalyzer
                bool success = await Task.Run(() => _analyzer.LoadRoutesFromHtml(files));

                if (success)
                {
                    // Получаем данные маршрутов
                    var routesData = _analyzer.GetRoutesData();

                    // ИСПРАВЛЕНО CS0266: DataFrame.Rows.Count возвращает long
                    int recordsCount = (int)routesData.Rows.Count;

                    // Получаем список доступных участков
                    var sections = _analyzer.GetAvailableSections();

                    // ИСПРАВЛЕНО CS1061: Правильное название метода
                    _controlSection.UpdateSectionsList(sections);  // <-- ТУТ ИСПРАВЛЕНИЕ

                    UpdateStatusBar($"Загружено маршрутов: {recordsCount} записей");

                    MessageBox.Show(
                        $"HTML маршруты успешно загружены\n\n" +
                        $"Файлов обработано: {files.Count}\n" +
                        $"Записей получено: {recordsCount}\n" +
                        $"Участков найдено: {sections.Count}",
                        "Загрузка завершена",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки HTML маршрутов");
                UpdateStatusBar("Ошибка загрузки маршрутов");
                MessageBox.Show(
                    $"Ошибка загрузки HTML маршрутов:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Обработчик загрузки HTML норм
        /// Python: _on_load_norms_click(), interface.py line 300
        /// ЧАТ 4: Использует InteractiveAnalyzer
        /// </summary>
        private async void FileSection_OnNormsLoaded(List<string> files)
        {
            Log.Information("Загрузка {Count} HTML файлов норм", files.Count);
            UpdateStatusBar("Загрузка норм...");

            try
            {
                // ЧАТ 4: Используем InteractiveAnalyzer
                bool success = await Task.Run(() => _analyzer.LoadNormsFromHtml(files));

                if (success)
                {
                    // ИСПРАВЛЕНО CS1929: StorageInfo это класс, а не Dictionary
                    // Получаем информацию о загруженных нормах
                    var storageInfo = _analyzer.GetNormStorageInfo();
                    int normsCount = storageInfo.TotalNorms;  // Прямое обращение к свойству

                    UpdateStatusBar($"Загружено норм: {normsCount}");

                    MessageBox.Show(
                        $"HTML нормы успешно загружены\n\n" +
                        $"Файлов обработано: {files.Count}\n" +
                        $"Норм загружено: {normsCount}",
                        "Загрузка завершена",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    Log.Information("Нормы успешно загружены: {Count} норм", normsCount);
                }
                else
                {
                    UpdateStatusBar("Ошибка загрузки норм");
                    MessageBox.Show(
                        "Не удалось загрузить нормы из HTML файлов",
                        "Ошибка",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки HTML норм");
                UpdateStatusBar("Ошибка загрузки");
                MessageBox.Show(
                    $"Ошибка загрузки норм: {ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Обработчик загрузки коэффициентов
        /// Python: _on_load_coefficients_click(), interface.py line 320
        /// TODO Чат 5: Реализация с LocomotiveFilter
        /// </summary>
        private void FileSection_OnCoefficientsLoaded(string file)
        {
            Log.Information("Загрузка коэффициентов из: {File}", file);
            
            // TODO Чат 5: Реализация с CoefficientsManager
            MessageBox.Show(
                $"Коэффициенты загружены из:\n{file}\n\n" +
                "Полная обработка будет реализована в Чате 5",
                "Загрузка коэффициентов",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            UpdateStatusBar("Коэффициенты загружены");
        }

        #endregion

        #region Обработчики событий ControlSection

        /// <summary>
        /// Обработчик запроса на анализ участка
        /// Python: _on_analyze_click(), interface.py line 330
        /// ЧАТ 4: ПОЛНАЯ РЕАЛИЗАЦИЯ с анализатором и графиком
        /// </summary>
        private async void ControlSection_OnAnalyzeRequested(string section, bool singleSectionMode)
        {
            Log.Information("Запрос на анализ: участок={Section}, одиночный={SingleMode}", 
                section, singleSectionMode);

            UpdateStatusBar($"Анализ участка: {section}...");

            try
            {
                // Получаем выбранную норму (если есть)
                string? selectedNorm = _controlSection.GetSelectedNorm();

                // ЧАТ 4: Выполняем анализ через InteractiveAnalyzer
                var (figure, statistics, error) = await Task.Run(() =>
                    _analyzer.AnalyzeSection(
                        section,
                        selectedNorm,
                        singleSectionMode,
                        locomotiveFilter: null  // TODO Чат 5: Фильтр локомотивов
                    ));

                if (error != null)
                {
                    UpdateStatusBar($"Ошибка анализа: {error}");
                    MessageBox.Show(error, "Ошибка анализа", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // ЧАТ 4: Отображаем график
                if (figure is ScottPlot.Plot plot)
                {
                    _visualizationSection.DisplayPlot(plot);
                    Log.Information("График отображен");
                }
                else
                {
                    Log.Warning("График не создан");
                }

                // ЧАТ 4: Отображаем статистику
                if (statistics != null)
                {
                    _visualizationSection.DisplayStatistics(statistics);
                    _controlSection.UpdateStatistics(FormatStatistics(statistics));
                    Log.Information("Статистика обновлена");
                }

                UpdateStatusBar($"Анализ завершен: {section}");
                
                Log.Information("Анализ участка {Section} успешно завершен", section);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка анализа участка {Section}", section);
                UpdateStatusBar("Ошибка анализа");
                MessageBox.Show(
                    $"Ошибка анализа: {ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Обработчик запроса на фильтрацию локомотивов
        /// Python: _on_filter_locomotives_click(), interface.py line 350
        /// TODO Чат 5: Откроет LocomotiveSelectorDialog
        /// </summary>
        private void ControlSection_OnFilterLocomotivesRequested()
        {
            Log.Information("Запрос на фильтрацию локомотивов");

            // TODO Чат 5: Открыть LocomotiveSelectorDialog
            MessageBox.Show(
                "Диалог фильтрации локомотивов будет реализован в Чате 5",
                "Фильтр",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        #endregion

        #region Обработчики меню

        /// <summary>
        /// Экспорт в Excel
        /// TODO Чат 7: Полная реализация
        /// </summary>
        private void Menu_ExportToExcel(object? sender, EventArgs e)
        {
            Log.Information("Запрос экспорта в Excel");
            
            // TODO Чат 7: Реализация экспорта
            MessageBox.Show(
                "Экспорт в Excel будет реализован в Чате 7",
                "TODO",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// Управление нормами
        /// TODO Чат 7: Откроет NormManagementDialog
        /// </summary>
        private void Menu_ManageNorms(object? sender, EventArgs e)
        {
            Log.Information("Запрос управления нормами");
            
            // TODO Чат 7: Открыть NormManagementDialog
            MessageBox.Show(
                "Управление нормами будет реализовано в Чате 7",
                "TODO",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// О программе
        /// </summary>
        private void Menu_About(object? sender, EventArgs e)
        {
            MessageBox.Show(
                "Анализ норм расхода электроэнергии\n\n" +
                "Версия: 1.0 (Чат 4)\n" +
                "Мигрировано из Python на C# .NET 9\n\n" +
                "© 2025",
                "О программе",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        #endregion

        #region Утилиты

        /// <summary>
        /// Обновляет статус бар
        /// </summary>
        private void UpdateStatusBar(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatusBar(message)));
                return;
            }

            _statusLabel.Text = message;
            Log.Debug("Статус: {Message}", message);
        }

        /// <summary>
        /// Форматирует статистику для отображения
        /// </summary>
        private string FormatStatistics(Dictionary<string, object> statistics)
        {
            var text = "";

            foreach (var (key, value) in statistics)
            {
                if (value is Dictionary<string, int> dict)
                {
                    text += $"{key}:\n";
                    foreach (var (subKey, subValue) in dict)
                    {
                        text += $"  {subKey}: {subValue}\n";
                    }
                }
                else if (value is double doubleValue)
                {
                    text += $"{key}: {doubleValue:F2}\n";
                }
                else
                {
                    text += $"{key}: {value}\n";
                }
            }

            return text;
        }

        #endregion
    }
}