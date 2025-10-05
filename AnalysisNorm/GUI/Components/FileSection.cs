// GUI/Components/FileSection.cs
// Миграция из: gui/components.py -> FileSection
// Секция работы с файлами (HTML маршруты, HTML нормы, Excel коэффициенты)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Serilog;

namespace AnalysisNorm.GUI.Components
{
    /// <summary>
    /// Секция работы с файлами
    /// Python класс: FileSection (gui/components.py)
    /// 
    /// Функционал:
    /// - Выбор HTML файлов маршрутов
    /// - Выбор HTML файлов норм
    /// - Выбор Excel файла коэффициентов
    /// - Отображение статуса загрузки
    /// </summary>
    public class FileSection : UserControl
    {
        // Списки выбранных файлов
        private List<string> _routeFiles;
        private List<string> _normFiles;
        private string _coefficientsFile;

        // Делегаты для коллбэков
        public delegate void FilesLoadedHandler(List<string> files);
        public delegate void CoefficientFileLoadedHandler(string filePath);

        // События для оповещения MainWindow
        public event FilesLoadedHandler OnRoutesLoaded;
        public event FilesLoadedHandler OnNormsLoaded;
        public event CoefficientFileLoadedHandler OnCoefficientsLoaded;

        // GUI элементы
        private GroupBox _fileGroupBox;
        private Label _routesLabel;
        private Label _normsLabel;
        private Label _coeffsLabel;
        private Label _statusLabel;

        private Button _selectRoutesButton;
        private Button _selectNormsButton;
        private Button _selectCoeffsButton;
        private Button _loadRoutesButton;
        private Button _loadNormsButton;
        private Button _loadCoeffsButton;
        private Button _clearRoutesButton;
        private Button _clearNormsButton;
        private Button _clearCoeffsButton;

        public FileSection()
        {
            _routeFiles = new List<string>();
            _normFiles = new List<string>();
            _coefficientsFile = null;

            InitializeComponent();
        }

        #region Инициализация GUI
        // Python: create_widgets()

        private void InitializeComponent()
        {
            SuspendLayout();

            // Главная группа
            _fileGroupBox = new GroupBox
            {
                Text = "Файлы данных",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // Создаем TableLayoutPanel для размещения элементов
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 5,
                Padding = new Padding(5)
            };

            // Настройка колонок
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Метка
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Статус файлов
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Кнопка "Выбрать"
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Кнопка "Очистить" / "Загрузить"

            // Настройка строк
            for (int i = 0; i < 4; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Статус

            // =============== Строка 1: HTML файлы маршрутов ===============
            var routesLabelText = new Label
            {
                Text = "HTML файлы маршрутов:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 5, 0, 0)
            };
            layout.Controls.Add(routesLabelText, 0, 0);

            _routesLabel = new Label
            {
                Text = "Не выбраны",
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.Gray,
                Padding = new Padding(0, 5, 0, 0)
            };
            layout.Controls.Add(_routesLabel, 1, 0);

            _selectRoutesButton = new Button
            {
                Text = "Выбрать файлы",
                AutoSize = true,
                Padding = new Padding(10, 2, 10, 2)
            };
            _selectRoutesButton.Click += SelectRoutesButton_Click;
            layout.Controls.Add(_selectRoutesButton, 2, 0);

            _clearRoutesButton = new Button
            {
                Text = "Очистить",
                AutoSize = true,
                Padding = new Padding(10, 2, 10, 2)
            };
            _clearRoutesButton.Click += (s, e) => ClearFiles("routes");
            layout.Controls.Add(_clearRoutesButton, 3, 0);

            // =============== Строка 2: HTML файлы норм ===============
            var normsLabelText = new Label
            {
                Text = "HTML файлы норм:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 5, 0, 0)
            };
            layout.Controls.Add(normsLabelText, 0, 1);

            _normsLabel = new Label
            {
                Text = "Не выбраны",
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.Gray,
                Padding = new Padding(0, 5, 0, 0)
            };
            layout.Controls.Add(_normsLabel, 1, 1);

            _selectNormsButton = new Button
            {
                Text = "Выбрать файлы",
                AutoSize = true,
                Padding = new Padding(10, 2, 10, 2)
            };
            _selectNormsButton.Click += SelectNormsButton_Click;
            layout.Controls.Add(_selectNormsButton, 2, 1);

            _clearNormsButton = new Button
            {
                Text = "Очистить",
                AutoSize = true,
                Padding = new Padding(10, 2, 10, 2)
            };
            _clearNormsButton.Click += (s, e) => ClearFiles("norms");
            layout.Controls.Add(_clearNormsButton, 3, 1);

            // =============== Строка 3: Excel коэффициенты ===============
            var coeffsLabelText = new Label
            {
                Text = "Excel коэффициенты:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 5, 0, 0)
            };
            layout.Controls.Add(coeffsLabelText, 0, 2);

            _coeffsLabel = new Label
            {
                Text = "Не выбран",
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.Gray,
                Padding = new Padding(0, 5, 0, 0)
            };
            layout.Controls.Add(_coeffsLabel, 1, 2);

            _selectCoeffsButton = new Button
            {
                Text = "Выбрать файл",
                AutoSize = true,
                Padding = new Padding(10, 2, 10, 2)
            };
            _selectCoeffsButton.Click += SelectCoefficientsButton_Click;
            layout.Controls.Add(_selectCoeffsButton, 2, 2);

            _clearCoeffsButton = new Button
            {
                Text = "Очистить",
                AutoSize = true,
                Padding = new Padding(10, 2, 10, 2)
            };
            _clearCoeffsButton.Click += (s, e) => ClearFiles("coefficients");
            layout.Controls.Add(_clearCoeffsButton, 3, 2);

            // =============== Строка 4: Кнопки загрузки ===============
            var buttonsPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0)
            };

            _loadRoutesButton = new Button
            {
                Text = "Загрузить маршруты",
                Enabled = false,
                Padding = new Padding(15, 5, 15, 5),
                Margin = new Padding(0, 0, 5, 0)
            };
            _loadRoutesButton.Click += LoadRoutesButton_Click;
            buttonsPanel.Controls.Add(_loadRoutesButton);

            _loadNormsButton = new Button
            {
                Text = "Загрузить нормы",
                Enabled = false,
                Padding = new Padding(15, 5, 15, 5),
                Margin = new Padding(0, 0, 5, 0)
            };
            _loadNormsButton.Click += LoadNormsButton_Click;
            buttonsPanel.Controls.Add(_loadNormsButton);

            _loadCoeffsButton = new Button
            {
                Text = "Загрузить коэффициенты",
                Enabled = false,
                Padding = new Padding(15, 5, 15, 5)
            };
            _loadCoeffsButton.Click += LoadCoefficientsButton_Click;
            buttonsPanel.Controls.Add(_loadCoeffsButton);

            layout.Controls.Add(buttonsPanel, 0, 3);
            layout.SetColumnSpan(buttonsPanel, 4);

            // =============== Строка 5: Статус ===============
            _statusLabel = new Label
            {
                Text = "",
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.Green,
                Padding = new Padding(0, 10, 0, 0)
            };
            layout.Controls.Add(_statusLabel, 0, 4);
            layout.SetColumnSpan(_statusLabel, 4);

            _fileGroupBox.Controls.Add(layout);
            Controls.Add(_fileGroupBox);

            ResumeLayout(false);
        }

        #endregion

        #region Обработчики кнопок выбора файлов
        // Python: _select_files()

        private void SelectRoutesButton_Click(object sender, EventArgs e)
        {
            SelectFiles("routes", _routesLabel, "маршрутов", _loadRoutesButton);
        }

        private void SelectNormsButton_Click(object sender, EventArgs e)
        {
            SelectFiles("norms", _normsLabel, "норм", _loadNormsButton);
        }

        private void SelectCoefficientsButton_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Выберите файл коэффициентов",
                Filter = "Excel файлы (*.xlsx;*.xls)|*.xlsx;*.xls|Все файлы (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _coefficientsFile = dialog.FileName;
                
                string fileName = Path.GetFileName(_coefficientsFile);
                _coeffsLabel.Text = fileName;
                _coeffsLabel.ForeColor = System.Drawing.Color.Black;
                
                _loadCoeffsButton.Enabled = true;
                
                Log.Information("Выбран файл коэффициентов: {File}", fileName);
            }
        }

        private void SelectFiles(string fileType, Label statusLabel, string kindLabel, Button loadButton)
        {
            using var dialog = new OpenFileDialog
            {
                Title = $"Выберите HTML файлы {kindLabel}",
                Filter = "HTML файлы (*.html;*.htm)|*.html;*.htm|Все файлы (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var files = dialog.FileNames.ToList();

                // Сохраняем файлы
                if (fileType == "routes")
                    _routeFiles = files;
                else
                    _normFiles = files;

                // Обновляем UI
                string displayText = GetDisplayText(files);
                statusLabel.Text = displayText;
                statusLabel.ForeColor = System.Drawing.Color.Black;

                // Активируем кнопку загрузки
                loadButton.Enabled = true;

                Log.Information("Выбрано {Count} файлов {Type}", files.Count, kindLabel);
            }
        }

        private string GetDisplayText(List<string> files)
        {
            var fileNames = files.Select(Path.GetFileName).ToList();
            
            if (fileNames.Count <= 3)
                return string.Join(", ", fileNames);
            
            return $"{string.Join(", ", fileNames.Take(3))} и еще {fileNames.Count - 3} файлов";
        }

        #endregion

        #region Очистка файлов
        // Python: _clear_files()

        private void ClearFiles(string fileType)
        {
            switch (fileType)
            {
                case "routes":
                    _routeFiles.Clear();
                    _routesLabel.Text = "Не выбраны";
                    _routesLabel.ForeColor = System.Drawing.Color.Gray;
                    _loadRoutesButton.Enabled = false;
                    Log.Debug("Очищены файлы маршрутов");
                    break;

                case "norms":
                    _normFiles.Clear();
                    _normsLabel.Text = "Не выбраны";
                    _normsLabel.ForeColor = System.Drawing.Color.Gray;
                    _loadNormsButton.Enabled = false;
                    Log.Debug("Очищены файлы норм");
                    break;

                case "coefficients":
                    _coefficientsFile = null;
                    _coeffsLabel.Text = "Не выбран";
                    _coeffsLabel.ForeColor = System.Drawing.Color.Gray;
                    _loadCoeffsButton.Enabled = false;
                    Log.Debug("Очищен файл коэффициентов");
                    break;
            }
        }

        #endregion

        #region Обработчики загрузки
        // Python: _load_routes(), _load_norms()

        /// <summary>
        /// Загрузка маршрутов (ЗАГЛУШКА для Чата 2)
        /// Python: _load_routes()
        /// 
        /// TODO Чат 3: Реализовать полную загрузку через RouteProcessor
        /// </summary>
        private void LoadRoutesButton_Click(object sender, EventArgs e)
        {
            if (_routeFiles.Count == 0)
            {
                MessageBox.Show("Сначала выберите HTML файлы маршрутов", 
                    "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ЗАГЛУШКА: В Чате 2 только логируем
            Log.Information("Загрузка {Count} файлов маршрутов", _routeFiles.Count);
            foreach (var file in _routeFiles)
            {
                Log.Debug("  - {File}", Path.GetFileName(file));
            }

            UpdateStatus("Файлы маршрутов выбраны. Загрузка будет реализована в Чате 3.", StatusType.Info);

            // Вызываем коллбэк (если подписаны)
            OnRoutesLoaded?.Invoke(new List<string>(_routeFiles));
        }

        /// <summary>
        /// Загрузка норм (ЗАГЛУШКА для Чата 2)
        /// Python: _load_norms()
        /// 
        /// TODO Чат 3: Реализовать полную загрузку через NormProcessor
        /// </summary>
        private void LoadNormsButton_Click(object sender, EventArgs e)
        {
            if (_normFiles.Count == 0)
            {
                MessageBox.Show("Сначала выберите HTML файлы норм",
                    "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ЗАГЛУШКА: В Чате 2 только логируем
            Log.Information("Загрузка {Count} файлов норм", _normFiles.Count);
            foreach (var file in _normFiles)
            {
                Log.Debug("  - {File}", Path.GetFileName(file));
            }

            UpdateStatus("Файлы норм выбраны. Загрузка будет реализована в Чате 3.", StatusType.Info);

            // Вызываем коллбэк (если подписаны)
            OnNormsLoaded?.Invoke(new List<string>(_normFiles));
        }

        /// <summary>
        /// Загрузка коэффициентов (РАБОТАЕТ в Чате 2!)
        /// Python: обработка в dialogs/selector.py
        /// </summary>
        private void LoadCoefficientsButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_coefficientsFile))
            {
                MessageBox.Show("Сначала выберите файл коэффициентов",
                    "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Log.Information("Загрузка файла коэффициентов: {File}", Path.GetFileName(_coefficientsFile));
            
            UpdateStatus("Коэффициенты выбраны. Загрузка через диалог селектора локомотивов.", StatusType.Success);

            // Вызываем коллбэк
            OnCoefficientsLoaded?.Invoke(_coefficientsFile);
        }

        #endregion

        #region Обновление статуса
        // Python: update_status()

        public enum StatusType
        {
            Info,
            Success,
            Warning,
            Error
        }

        public void UpdateStatus(string message, StatusType type = StatusType.Info)
        {
            _statusLabel.Text = message;

            _statusLabel.ForeColor = type switch
            {
                StatusType.Success => System.Drawing.Color.Green,
                StatusType.Error => System.Drawing.Color.Red,
                StatusType.Warning => System.Drawing.Color.Orange,
                _ => System.Drawing.Color.Black
            };

            Log.Debug("Статус FileSection: {Message}", message);
        }

        #endregion

        #region Публичные методы для доступа к данным

        /// <summary>
        /// Получить список файлов маршрутов
        /// </summary>
        public List<string> GetRouteFiles() => new List<string>(_routeFiles);

        /// <summary>
        /// Получить список файлов норм
        /// </summary>
        public List<string> GetNormFiles() => new List<string>(_normFiles);

        /// <summary>
        /// Получить файл коэффициентов
        /// </summary>
        public string GetCoefficientsFile() => _coefficientsFile;

        #endregion
    }
}
