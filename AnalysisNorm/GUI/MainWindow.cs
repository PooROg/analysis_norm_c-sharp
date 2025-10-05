using System;
using System.Drawing;
using System.Windows.Forms;
using Serilog;

namespace AnalysisNorm.GUI
{
    /// <summary>
    /// Главное окно приложения
    /// Мигрировано из: gui/interface.py, class NormsAnalyzerGUI (lines 30-650)
    /// </summary>
    public partial class MainWindow : Form
    {
        #region Поля

        private MenuStrip menuStrip;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;

        // Панели секций (пока пустые - заглушки)
        private Panel fileSectionPanel;     // Левая панель - выбор файлов
        private Panel controlSectionPanel;  // Центральная панель - управление анализом
        private Panel visualSectionPanel;   // Правая панель - визуализация

        #endregion

        #region Конструктор

        public MainWindow()
        {
            InitializeComponent();
            SetupUI();
            SetupLogging();

            Log.Information("Главное окно инициализировано");
        }

        #endregion

        #region Настройка UI

        /// <summary>
        /// Настройка пользовательского интерфейса
        /// Python: _setup_gui(), line 45
        /// </summary>
        private void SetupUI()
        {
            // Python: self.root.title("Анализатор норм расхода электроэнергии РЖД")
            Text = "Анализатор норм расхода электроэнергии РЖД";

            // Python: self.root.geometry(f"{APP_CONFIG.default_window_size[0]}x{APP_CONFIG.default_window_size[1]}")
            Size = new Size(Core.AppConfig.DefaultWindowSize.Width, 
                           Core.AppConfig.DefaultWindowSize.Height);
            
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;

            // Создаем меню
            CreateMenuStrip();

            // Создаем статус бар
            CreateStatusStrip();

            // Создаем основной layout с тремя панелями
            CreateMainLayout();
        }

        /// <summary>
        /// Создание главного меню
        /// </summary>
        private void CreateMenuStrip()
        {
            menuStrip = new MenuStrip();

            // Меню "Файл"
            var fileMenu = new ToolStripMenuItem("Файл");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Загрузить маршруты...", null, OnLoadRoutesClick) 
            { 
                Enabled = false // TODO: Реализовать в Чате 2
            });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Загрузить нормы...", null, OnLoadNormsClick) 
            { 
                Enabled = false // TODO: Реализовать в Чате 2
            });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Экспорт в Excel...", null, OnExportExcelClick) 
            { 
                Enabled = false // TODO: Реализовать в Чате 5-6
            });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Выход", null, OnExitClick));

            // Меню "Настройки"
            var settingsMenu = new ToolStripMenuItem("Настройки");
            settingsMenu.DropDownItems.Add(new ToolStripMenuItem("Фильтр локомотивов...", null, OnFilterClick) 
            { 
                Enabled = false // TODO: Реализовать в Чате 3
            });
            settingsMenu.DropDownItems.Add(new ToolStripMenuItem("Параметры...", null, OnSettingsClick) 
            { 
                Enabled = false // TODO: Реализовать позже
            });

            // Меню "О программе"
            var helpMenu = new ToolStripMenuItem("О программе");
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("О программе", null, OnAboutClick));
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("Документация", null, OnHelpClick) 
            { 
                Enabled = false // TODO: Реализовать позже
            });

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(settingsMenu);
            menuStrip.Items.Add(helpMenu);

            Controls.Add(menuStrip);
            MainMenuStrip = menuStrip;
        }

        /// <summary>
        /// Создание статус бара
        /// Python: _create_log_section(), line 150
        /// </summary>
        private void CreateStatusStrip()
        {
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Готов к работе");
            
            statusStrip.Items.Add(statusLabel);
            Controls.Add(statusStrip);
        }

        /// <summary>
        /// Создание основного layout с тремя панелями
        /// Python: _create_layout(), line 100
        /// </summary>
        private void CreateMainLayout()
        {
            // Python: main_container = ttk.Frame(self.root, padding="10")
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(10)
            };

            // Python: main_container.columnconfigure(1, weight=1)
            // Левая панель - 25%, центральная - 30%, правая - 45%
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            // Создаем панели с заглушками
            fileSectionPanel = CreatePlaceholderPanel(
                "Секция выбора файлов",
                "Будет реализована в Чате 2:\n\n" +
                "• Выбор HTML файлов маршрутов\n" +
                "• Выбор HTML файлов норм\n" +
                "• Загрузка данных\n" +
                "• Статус загрузки"
            );

            controlSectionPanel = CreatePlaceholderPanel(
                "Секция управления анализом",
                "Будет реализована в Чатах 2-3:\n\n" +
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

            mainLayout.Controls.Add(fileSectionPanel, 0, 0);
            mainLayout.Controls.Add(controlSectionPanel, 1, 0);
            mainLayout.Controls.Add(visualSectionPanel, 2, 0);

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

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightSteelBlue
            };

            var descLabel = new Label
            {
                Text = description,
                Font = new Font("Segoe UI", 9F),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.DimGray
            };

            panel.Controls.Add(descLabel);
            panel.Controls.Add(titleLabel);

            return panel;
        }

        #endregion

        #region Логирование

        /// <summary>
        /// Настройка логирования в UI
        /// Python: _setup_logging(), line 350
        /// </summary>
        private void SetupLogging()
        {
            // TODO: В будущем добавить custom Serilog sink для вывода в statusLabel
            // Пока просто логируем события в файл через Serilog
        }

        /// <summary>
        /// Обновление статус бара
        /// </summary>
        public void UpdateStatus(string message, bool isError = false)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, bool>(UpdateStatus), message, isError);
                return;
            }

            statusLabel.Text = message;
            statusLabel.ForeColor = isError ? Color.Red : Color.Black;
            
            Log.Information("Статус: {Message}", message);
        }

        #endregion

        #region Обработчики событий меню (заглушки)

        private void OnLoadRoutesClick(object sender, EventArgs e)
        {
            // TODO: Реализовать в Чате 2
            MessageBox.Show("Функция будет реализована в следующем чате", "TODO", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnLoadNormsClick(object sender, EventArgs e)
        {
            // TODO: Реализовать в Чате 2
            MessageBox.Show("Функция будет реализована в следующем чате", "TODO", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnExportExcelClick(object sender, EventArgs e)
        {
            // TODO: Реализовать в Чатах 5-6
            MessageBox.Show("Функция будет реализована позже", "TODO", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnFilterClick(object sender, EventArgs e)
        {
            // TODO: Реализовать в Чате 3
            MessageBox.Show("Функция будет реализована в Чате 3", "TODO", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnSettingsClick(object sender, EventArgs e)
        {
            // TODO: Реализовать позже
            MessageBox.Show("Функция будет реализована позже", "TODO", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnHelpClick(object sender, EventArgs e)
        {
            // TODO: Реализовать позже
            MessageBox.Show("Функция будет реализована позже", "TODO", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnAboutClick(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Анализатор норм расхода электроэнергии РЖД\n\n" +
                "Версия: 1.0 (C# миграция)\n" +
                "Платформа: .NET 9\n\n" +
                "Мигрировано из Python 3.12",
                "О программе",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            Log.Information("Закрытие приложения");
            Application.Exit();
        }

        #endregion

        #region Закрытие окна

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Python: on_closing(), line 600
            Log.Information("Приложение закрывается");
            
            // TODO: В будущем добавить очистку временных файлов
            
            base.OnFormClosing(e);
        }

        #endregion
    }
}