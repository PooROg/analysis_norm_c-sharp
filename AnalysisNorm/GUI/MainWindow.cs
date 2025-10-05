// GUI/MainWindow.cs
// Главное окно приложения
// Мигрировано из: gui/interface.py, class NormsAnalyzerGUI
// ЧАТ 2: ИНТЕГРАЦИЯ FILESECTION ЗАВЕРШЕНА ✅

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Serilog;
using AnalysisNorm.GUI.Components;
using AnalysisNorm.Core;

namespace AnalysisNorm.GUI
{
    /// <summary>
    /// Главное окно приложения
    /// Мигрировано из: gui/interface.py, class NormsAnalyzerGUI (lines 30-650)
    /// 
    /// ЧАТ 2 ИНТЕГРАЦИЯ:
    /// - FileSection полностью интегрирован
    /// - События подключены
    /// - Обработчики реализованы с TODO для Чата 3
    /// </summary>
    public partial class MainWindow : Form
    {
        #region Поля

        private MenuStrip menuStrip;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;

        // ЧАТ 2: FileSection заменяет placeholder панель ✅
        private FileSection _fileSection;           // Левая панель - выбор файлов
        private Panel controlSectionPanel;          // Центральная панель - управление (placeholder)
        private Panel visualSectionPanel;           // Правая панель - визуализация (placeholder)

        #endregion

        #region Конструктор

        public MainWindow()
        {
            InitializeComponent();
            SetupUI();
            SetupLogging();

            Log.Information("Главное окно инициализировано [ЧАТ 2: FileSection интегрирован]");
        }

        #endregion

        #region Настройка UI

        /// <summary>
        /// Настройка пользовательского интерфейса
        /// Python: _setup_gui(), line 45
        /// ЧАТ 2: Добавлена интеграция FileSection
        /// </summary>
        private void SetupUI()
        {
            // Python: self.root.title("Анализатор норм расхода электроэнергии РЖД")
            Text = "Анализатор норм расхода электроэнергии РЖД";

            // Python: self.root.geometry(...)
            Size = new Size(AppConfig.DefaultWindowSize.Width, 
                           AppConfig.DefaultWindowSize.Height);
            
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;

            // Создаем меню
            SetupMenu();

            // Создаем статус бар
            SetupStatusBar();

            // Создаем главный layout
            SetupMainLayout();
        }

        /// <summary>
        /// Настройка меню приложения
        /// Python: self._setup_menu(), line 85
        /// </summary>
        private void SetupMenu()
        {
            menuStrip = new MenuStrip();

            // Меню "Файл"
            var fileMenu = new ToolStripMenuItem("Файл");
            fileMenu.DropDownItems.Add("Экспорт в Excel", null, (s, e) => 
                MessageBox.Show("TODO Чат 7: Экспорт в Excel", "Информация"));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Выход", null, (s, e) => Application.Exit());

            // Меню "Настройки"
            var settingsMenu = new ToolStripMenuItem("Настройки");
            settingsMenu.DropDownItems.Add("Управление нормами", null, (s, e) => 
                MessageBox.Show("TODO Чат 7: Управление нормами", "Информация"));

            // Меню "О программе"
            var aboutMenu = new ToolStripMenuItem("О программе");
            aboutMenu.Click += (s, e) => ShowAbout();

            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, settingsMenu, aboutMenu });
            Controls.Add(menuStrip);
            MainMenuStrip = menuStrip;
        }

        /// <summary>
        /// Настройка статус бара
        /// Python: self.status_bar, line 110
        /// </summary>
        private void SetupStatusBar()
        {
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Готов")
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusStrip.Items.Add(statusLabel);
            Controls.Add(statusStrip);
        }

        /// <summary>
        /// Настройка главного layout
        /// Python: self._setup_main_layout(), line 125
        /// ЧАТ 2: FileSection интегрирован вместо placeholder ✅
        /// </summary>
        private void SetupMainLayout()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(5)
            };

            // Настройка колонок (25% - 30% - 45%)
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            // ============================================================
            // ЧАТ 2: ИНТЕГРАЦИЯ FILESECTION ✅
            // ============================================================
            
            // Создаем FileSection вместо placeholder
            _fileSection = new FileSection
            {
                Dock = DockStyle.Fill
            };

            // Подписываемся на события FileSection
            _fileSection.OnRoutesLoaded += FileSection_OnRoutesLoaded;
            _fileSection.OnNormsLoaded += FileSection_OnNormsLoaded;
            _fileSection.OnCoefficientsLoaded += FileSection_OnCoefficientsLoaded;

            Log.Information("FileSection создан и события подключены");

            // ============================================================
            // ОСТАЛЬНЫЕ СЕКЦИИ - PLACEHOLDERS (будут в следующих чатах)
            // ============================================================

            controlSectionPanel = CreatePlaceholderPanel(
                "Секция управления анализом",
                "Будет реализована в Чате 3:\n\n" +
                "• Выбор участка\n" +
                "• Выбор нормы\n" +
                "• Фильтр локомотивов\n" +
                "• Кнопка анализа\n" +
                "• Статистика"
            );

            visualSectionPanel = CreatePlaceholderPanel(
                "Секция визуализации",
                "Будет реализована в Чатах 4-5:\n\n" +
                "• Интерактивный график\n" +
                "• Открытие в браузере\n" +
                "• Экспорт графика\n" +
                "• Информация о нормах"
            );

            // Добавляем все секции в layout
            mainLayout.Controls.Add(_fileSection, 0, 0);           // ЧАТ 2: FileSection ✅
            mainLayout.Controls.Add(controlSectionPanel, 1, 0);    // TODO Чат 3
            mainLayout.Controls.Add(visualSectionPanel, 2, 0);     // TODO Чат 4-5

            Controls.Add(mainLayout);
        }

        /// <summary>
        /// Создает панель-заглушку с описанием
        /// </summary>
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

        #region Обработчики событий FileSection (ЧАТ 2) ✅

        /// <summary>
        /// Обработчик события загрузки HTML маршрутов
        /// Python: _on_routes_loaded(), interface.py line 280
        /// ЧАТ 2: Базовая реализация с логированием
        /// ЧАТ 3: Добавится RouteProcessor для парсинга HTML
        /// </summary>
        private void FileSection_OnRoutesLoaded(List<string> files)
        {
            Log.Information("MainWindow: Получены файлы маршрутов: {Count}", files.Count);
            
            foreach (var file in files)
            {
                Log.Debug("  - {FileName}", Path.GetFileName(file));
            }

            UpdateStatusBar($"Выбрано файлов маршрутов: {files.Count}");

            // TODO Чат 3: Вызвать RouteProcessor для парсинга HTML
            // var routeProcessor = new RouteProcessor();
            // var routes = routeProcessor.ProcessHtmlFiles(files);
            // controlSection.UpdateSectionsList(routes.GetSections());
        }

        /// <summary>
        /// Обработчик события загрузки HTML норм
        /// Python: _on_norms_loaded(), interface.py line 295
        /// ЧАТ 2: Базовая реализация с логированием
        /// ЧАТ 3: Добавится NormProcessor для парсинга HTML
        /// </summary>
        private void FileSection_OnNormsLoaded(List<string> files)
        {
            Log.Information("MainWindow: Получены файлы норм: {Count}", files.Count);
            
            foreach (var file in files)
            {
                Log.Debug("  - {FileName}", Path.GetFileName(file));
            }

            UpdateStatusBar($"Выбрано файлов норм: {files.Count}");

            // TODO Чат 3: Вызвать NormProcessor для парсинга HTML
            // var normProcessor = new NormProcessor(normStorage);
            // normProcessor.ProcessHtmlFiles(files);
            // controlSection.UpdateNormsList(normStorage.GetAllNormIds());
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
            // var coeffManager = new CoefficientsManager();
            // bool loaded = coeffManager.LoadCoefficients(filePath);
            // if (loaded)
            // {
            //     var dialog = new LocomotiveSelectorDialog(coeffManager);
            //     if (dialog.ShowDialog() == DialogResult.OK)
            //     {
            //         var selectedLocos = dialog.GetSelectedLocomotives();
            //         // Применить фильтр
            //     }
            // }
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Обновление текста статус бара
        /// Python: self.update_status(), line 150
        /// </summary>
        private void UpdateStatusBar(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatusBar(message)));
                return;
            }

            statusLabel.Text = message;
            Log.Information("Статус: {Message}", message);
        }

        /// <summary>
        /// Показать окно "О программе"
        /// Python: self._show_about(), line 380
        /// </summary>
        private void ShowAbout()
        {
            MessageBox.Show(
                "Анализатор норм расхода электроэнергии РЖД\n\n" +
                "Версия: 1.0 (C# миграция)\n" +
                "Прогресс миграции: Чат 2 из 8 (25%)\n\n" +
                "Мигрировано с Python 3.12 на C# .NET 9\n" +
                "Разработка: 2025",
                "О программе",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        #endregion

        #region Настройка логирования

        /// <summary>
        /// Настройка перенаправления логов в статус бар
        /// Python: setup_logging(), main.py line 25
        /// </summary>
        private void SetupLogging()
        {
            // Логи уже настроены в Program.cs через Serilog
            // Здесь только подключаем вывод в статус бар
            Log.Information("Логирование для MainWindow настроено");
        }

        #endregion
    }
}
