// GUI/MainWindow.cs
// Главное окно приложения
// Мигрировано из: gui/interface.py, class NormsAnalyzerGUI
// ЧАТ 2: ИНТЕГРАЦИЯ FILESECTION ✅
// ЧАТ 3: ИНТЕГРАЦИЯ ROUTEPROCESSOR, NORMPROCESSOR, CONTROLSECTION ✅

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;
using Serilog;
using AnalysisNorm.GUI.Components;
using AnalysisNorm.Core;
using AnalysisNorm.Analysis;

namespace AnalysisNorm.GUI
{
    /// <summary>
    /// Главное окно приложения
    /// Мигрировано из: gui/interface.py, class NormsAnalyzerGUI (lines 30-650)
    /// 
    /// ЧАТ 2 ИНТЕГРАЦИЯ:
    /// - FileSection полностью интегрирован
    /// - События подключены
    /// 
    /// ЧАТ 3 ИНТЕГРАЦИЯ:
    /// - RouteProcessor для парсинга HTML маршрутов
    /// - NormProcessor для парсинга HTML норм
    /// - ControlSection для управления анализом
    /// </summary>
    public partial class MainWindow : Form
    {
        #region Поля

        private MenuStrip menuStrip;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;

        // ЧАТ 2: FileSection ✅
        private FileSection _fileSection;
        
        // ЧАТ 3: ControlSection ✅
        private ControlSection _controlSection;
        
        private Panel _visualSectionPanel; // TODO Чат 4

        // ЧАТ 3: Процессоры ✅
        private RouteProcessor _routeProcessor;
        private NormProcessor _normProcessor;
        
        // ЧАТ 2: Core компоненты ✅
        private NormStorage _normStorage;

        #endregion

        #region Конструктор

        public MainWindow()
        {
            InitializeComponent();
            InitializeComponents(); // ЧАТ 3: Инициализация процессоров
            SetupUI();
            SetupLogging();

            Log.Information("Главное окно инициализировано [ЧАТ 3: RouteProcessor, NormProcessor, ControlSection]");
        }

        #endregion

        #region Инициализация компонентов (ЧАТ 3) ✅

        /// <summary>
        /// Инициализирует процессоры и core компоненты
        /// ЧАТ 3: Новый метод
        /// </summary>
        private void InitializeComponents()
        {
            // Инициализируем NormStorage
            _normStorage = new NormStorage();
            Log.Information("NormStorage инициализирован");

            // Инициализируем процессоры
            _routeProcessor = new RouteProcessor();
            _normProcessor = new NormProcessor(_normStorage);
            Log.Information("Процессоры инициализированы");
        }

        #endregion

        #region Настройка UI

        /// <summary>
        /// Настройка пользовательского интерфейса
        /// Python: _setup_gui(), line 45
        /// ЧАТ 2: Добавлена интеграция FileSection
        /// ЧАТ 3: Добавлена интеграция ControlSection
        /// </summary>
        private void SetupUI()
        {
            Text = "Анализатор норм расхода электроэнергии РЖД";
            Size = new Size(AppConfig.DefaultWindowSize.Width, 
                           AppConfig.DefaultWindowSize.Height);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 600);

            SetupMenu();
            SetupStatusBar();
            SetupMainLayout();

            Log.Information("UI настроен");
        }

        /// <summary>
        /// Настройка меню
        /// Python: _setup_menu(), line 65
        /// </summary>
        private void SetupMenu()
        {
            menuStrip = new MenuStrip();

            // Меню "Файл"
            var fileMenu = new ToolStripMenuItem("Файл");
            fileMenu.DropDownItems.Add("Экспорт в Excel", null, (s, e) =>
            {
                // TODO Чат 7: Реализовать экспорт
                MessageBox.Show("Экспорт будет реализован в Чате 7", "Информация");
            });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Выход", null, (s, e) => Application.Exit());

            // Меню "Настройки"
            var settingsMenu = new ToolStripMenuItem("Настройки");
            settingsMenu.DropDownItems.Add("Управление нормами", null, (s, e) =>
            {
                // TODO Чат 7: Открыть диалог управления нормами
                MessageBox.Show("Диалог будет реализован в Чате 7", "Информация");
            });

            // Меню "О программе"
            var helpMenu = new ToolStripMenuItem("Справка");
            helpMenu.DropDownItems.Add("О программе", null, (s, e) =>
            {
                MessageBox.Show(
                    "Анализатор норм расхода электроэнергии РЖД\n\n" +
                    $"Версия: {AppConfig.Version}\n" +
                    "Портировано с Python на C# .NET 9\n\n" +
                    "Чат 3: HTML парсинг + Управление",
                    "О программе",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            });

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(settingsMenu);
            menuStrip.Items.Add(helpMenu);

            Controls.Add(menuStrip);
            MainMenuStrip = menuStrip;
        }

        /// <summary>
        /// Настройка статус бара
        /// Python: часть _setup_gui()
        /// </summary>
        private void SetupStatusBar()
        {
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel
            {
                Text = "Готов к работе",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            statusStrip.Items.Add(statusLabel);
            Controls.Add(statusStrip);
        }

        /// <summary>
        /// Настройка основного layout с тремя секциями
        /// Python: _setup_gui() создание главных панелей
        /// ЧАТ 2: FileSection интегрирован
        /// ЧАТ 3: ControlSection интегрирован
        /// </summary>
        private void SetupMainLayout()
        {
            // TableLayoutPanel: 3 колонки (Файлы | Управление | Визуализация)
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(5)
            };

            // Колонки: 25% | 25% | 50%
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // ЧАТ 2: FileSection ✅
            _fileSection = new FileSection
            {
                Dock = DockStyle.Fill
            };
            _fileSection.OnRoutesLoaded += FileSection_OnRoutesLoaded;
            _fileSection.OnNormsLoaded += FileSection_OnNormsLoaded;
            _fileSection.OnCoefficientsLoaded += FileSection_OnCoefficientsLoaded;

            // ЧАТ 3: ControlSection ✅
            _controlSection = new ControlSection
            {
                Dock = DockStyle.Fill
            };
            _controlSection.OnAnalyzeRequested += ControlSection_OnAnalyzeRequested;
            _controlSection.OnFilterLocomotivesRequested += ControlSection_OnFilterLocomotivesRequested;

            // TODO Чат 4: VisualizationSection
            _visualSectionPanel = CreatePlaceholderPanel(
                "Визуализация",
                "Будет реализована в Чате 4:\n\n" +
                "• Интерактивный график (ScottPlot)\n" +
                "• Экспорт графика\n" +
                "• Информация о нормах"
            );

            mainLayout.Controls.Add(_fileSection, 0, 0);
            mainLayout.Controls.Add(_controlSection, 1, 0);
            mainLayout.Controls.Add(_visualSectionPanel, 2, 0);

            Controls.Add(mainLayout);

            Log.Information("MainLayout настроен [3 секции: FileSection, ControlSection, Placeholder]");
        }

        private Panel CreatePlaceholderPanel(string title, string description)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.WhiteSmoke
            };

            var groupBox = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var label = new Label
            {
                Text = description,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 10),
                ForeColor = Color.Gray
            };

            groupBox.Controls.Add(label);
            panel.Controls.Add(groupBox);

            return panel;
        }

        #endregion

        #region Логирование

        private void SetupLogging()
        {
            // Подписываемся на события логов Serilog для отображения в StatusBar
            // (в реальном приложении используется Sink)
        }

        /// <summary>
        /// Обновляет текст в статус баре
        /// Python: self.status_var.set(...)
        /// </summary>
        private void UpdateStatusBar(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatusBar(message)));
                return;
            }

            statusLabel.Text = message;
            Log.Debug("StatusBar: {Message}", message);
        }

        #endregion

        #region Обработчики событий FileSection (ЧАТ 2+3) ✅

        /// <summary>
        /// Обработчик события загрузки HTML маршрутов
        /// Python: _on_routes_loaded(), interface.py line 280
        /// ЧАТ 2: Базовая реализация с логированием
        /// ЧАТ 3: Добавлен RouteProcessor для парсинга HTML ✅
        /// </summary>
        private async void FileSection_OnRoutesLoaded(List<string> files)
        {
            Log.Information("MainWindow: Получены файлы маршрутов: {Count}", files.Count);
            
            UpdateStatusBar("Обработка HTML маршрутов...");

            try
            {
                // ЧАТ 3: Парсим HTML маршруты ✅
                await Task.Run(() =>
                {
                    var dataFrame = _routeProcessor.ProcessHtmlFiles(files);
                    
                    Log.Information("Маршруты обработаны: {Rows} записей", dataFrame.Rows.Count);
                });

                // Обновляем список участков в ControlSection
                var sections = _routeProcessor.GetSections();
                _controlSection.UpdateSectionsList(sections);

                UpdateStatusBar($"Загружено маршрутов: {_routeProcessor.RoutesDataFrame?.Rows.Count ?? 0}");

                MessageBox.Show(
                    $"Загружено файлов: {files.Count}\n" +
                    $"Обработано записей: {_routeProcessor.RoutesDataFrame?.Rows.Count ?? 0}\n" +
                    $"Найдено участков: {sections.Count}\n\n" +
                    $"Статистика: {_routeProcessor.ProcessingStats}",
                    "Маршруты загружены",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки маршрутов");
                UpdateStatusBar("Ошибка обработки маршрутов");
                MessageBox.Show(
                    $"Ошибка обработки маршрутов:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Обработчик события загрузки HTML норм
        /// Python: _on_norms_loaded(), interface.py line 295
        /// ЧАТ 2: Базовая реализация с логированием
        /// ЧАТ 3: Добавлен NormProcessor для парсинга HTML ✅
        /// </summary>
        private async void FileSection_OnNormsLoaded(List<string> files)
        {
            Log.Information("MainWindow: Получены файлы норм: {Count}", files.Count);
            
            UpdateStatusBar("Обработка HTML норм...");

            try
            {
                // ЧАТ 3: Парсим HTML нормы ✅
                await Task.Run(() =>
                {
                    _normProcessor.ProcessHtmlFiles(files);
                });

                // Обновляем список норм в ControlSection
                var norms = _normProcessor.GetLoadedNorms();
                _controlSection.UpdateNormsList(norms);

                UpdateStatusBar($"Загружено норм: {norms.Count}");

                MessageBox.Show(
                    $"Загружено файлов: {files.Count}\n" +
                    $"Найдено норм: {norms.Count}\n\n" +
                    $"{_normProcessor.GetStorageInfo()}",
                    "Нормы загружены",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки норм");
                UpdateStatusBar("Ошибка обработки норм");
                MessageBox.Show(
                    $"Ошибка обработки норм:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Обработчик события загрузки Excel коэффициентов
        /// Python: _on_coefficients_loaded(), interface.py line 310
        /// ЧАТ 2: Базовая реализация с логированием
        /// ЧАТ 5: Добавится LocomotiveSelectorDialog
        /// </summary>
        private void FileSection_OnCoefficientsLoaded(string filePath)
        {
            Log.Information("MainWindow: Получен файл коэффициентов: {File}", 
                Path.GetFileName(filePath));

            UpdateStatusBar($"Выбран файл коэффициентов: {Path.GetFileName(filePath)}");

            // TODO Чат 5: Открыть диалог селектора локомотивов
            MessageBox.Show(
                $"Файл коэффициентов: {Path.GetFileName(filePath)}\n\n" +
                "Диалог выбора локомотивов будет реализован в Чате 5",
                "Коэффициенты",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        #endregion

        #region Обработчики событий ControlSection (ЧАТ 3) ✅

        /// <summary>
        /// Обработчик запроса на анализ участка
        /// Python: _on_analyze_click(), interface.py line 330
        /// ЧАТ 3: Базовая реализация с заглушкой
        /// ЧАТ 4: Полная реализация с DataAnalyzer
        /// </summary>
        private void ControlSection_OnAnalyzeRequested(string section, bool singleSectionMode)
        {
            Log.Information("Запрос на анализ: участок={Section}, одиночный={SingleMode}", 
                section, singleSectionMode);

            UpdateStatusBar($"Анализ участка: {section}");

            // TODO Чат 4: Полная реализация анализа
            // - DataAnalyzer
            // - InteractiveAnalyzer
            // - PlotBuilder

            MessageBox.Show(
                $"Анализ участка: {section}\n" +
                $"Одиночный режим: {singleSectionMode}\n\n" +
                "Полный анализ будет реализован в Чате 4",
                "Анализ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// Обработчик запроса на фильтрацию локомотивов
        /// Python: _on_filter_locomotives_click(), interface.py line 350
        /// ЧАТ 5: Откроет LocomotiveSelectorDialog
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
    }
}