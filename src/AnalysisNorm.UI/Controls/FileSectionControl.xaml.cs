using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MaterialDesignThemes.Wpf;

namespace AnalysisNorm.UI.Controls;

/// <summary>
/// UserControl для работы с файлами - аналог Python FileSection
/// Обеспечивает выбор и загрузку HTML файлов маршрутов и норм
/// </summary>
public partial class FileSectionControl : UserControl
{
    #region Dependency Properties - для биндинга с ViewModel

    public static readonly DependencyProperty RouteFilesProperty =
        DependencyProperty.Register(nameof(RouteFiles), typeof(string[]), typeof(FileSectionControl));

    public static readonly DependencyProperty NormFilesProperty =
        DependencyProperty.Register(nameof(NormFiles), typeof(string[]), typeof(FileSectionControl));

    /// <summary>
    /// Массив выбранных файлов маршрутов - аналог Python self.route_files
    /// </summary>
    public string[] RouteFiles
    {
        get => (string[])GetValue(RouteFilesProperty);
        set => SetValue(RouteFilesProperty, value);
    }

    /// <summary>
    /// Массив выбранных файлов норм - аналог Python self.norm_files
    /// </summary>
    public string[] NormFiles
    {
        get => (string[])GetValue(NormFilesProperty);
        set => SetValue(NormFilesProperty, value);
    }

    #endregion

    public FileSectionControl()
    {
        InitializeComponent();
        
        // Инициализируем пустые массивы
        RouteFiles = Array.Empty<string>();
        NormFiles = Array.Empty<string>();
    }

    #region Обработчики событий выбора файлов - аналоги Python file selection methods

    /// <summary>
    /// Выбор HTML файлов маршрутов - аналог Python _select_files для routes
    /// </summary>
    private void SelectRouteFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите HTML файлы с маршрутами",
            Filter = "HTML файлы (*.html;*.htm)|*.html;*.htm|Все файлы (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            RouteFiles = dialog.FileNames;
            UpdateRouteFilesDisplay();
            LoadRoutesButton.IsEnabled = RouteFiles.Any();
            
            // Показываем статус выбора - аналог Python label update
            ShowStatus($"Выбрано {RouteFiles.Length} файлов маршрутов", StatusType.Info);
        }
    }

    /// <summary>
    /// Выбор HTML файлов норм - аналог Python _select_files для norms  
    /// </summary>
    private void SelectNormFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите HTML файлы с нормами",
            Filter = "HTML файлы (*.html;*.htm)|*.html;*.htm|Все файлы (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            NormFiles = dialog.FileNames;
            UpdateNormFilesDisplay();
            LoadNormsButton.IsEnabled = NormFiles.Any();
            
            // Показываем статус выбора
            ShowStatus($"Выбрано {NormFiles.Length} файлов норм", StatusType.Info);
        }
    }

    /// <summary>
    /// Очистка выбранных файлов маршрутов - аналог Python _clear_files для routes
    /// </summary>
    private void ClearRouteFiles_Click(object sender, RoutedEventArgs e)
    {
        RouteFiles = Array.Empty<string>();
        UpdateRouteFilesDisplay();
        LoadRoutesButton.IsEnabled = false;
        
        ShowStatus("Файлы маршрутов очищены", StatusType.Warning);
    }

    /// <summary>
    /// Очистка выбранных файлов норм - аналог Python _clear_files для norms
    /// </summary>
    private void ClearNormFiles_Click(object sender, RoutedEventArgs e)
    {
        NormFiles = Array.Empty<string>();
        UpdateNormFilesDisplay();
        LoadNormsButton.IsEnabled = false;
        
        ShowStatus("Файлы норм очищены", StatusType.Warning);
    }

    #endregion

    #region Методы обновления UI - аналоги Python UI update methods

    /// <summary>
    /// Обновляет отображение выбранных файлов маршрутов
    /// Аналог Python label.config(text=...) для route files
    /// </summary>
    private void UpdateRouteFilesDisplay()
    {
        if (!RouteFiles.Any())
        {
            RouteFilesLabel.Text = "Не выбраны";
            RouteFilesLabel.Foreground = System.Windows.Media.Brushes.Gray;
            return;
        }

        if (RouteFiles.Length == 1)
        {
            RouteFilesLabel.Text = System.IO.Path.GetFileName(RouteFiles[0]);
        }
        else
        {
            RouteFilesLabel.Text = $"{RouteFiles.Length} файлов: {string.Join(", ", RouteFiles.Take(2).Select(System.IO.Path.GetFileName))}" +
                                  (RouteFiles.Length > 2 ? "..." : "");
        }

        RouteFilesLabel.Foreground = System.Windows.Media.Brushes.Black;
        
        // Устанавливаем полный путь в ToolTip для удобства
        RouteFilesLabel.ToolTip = string.Join("\n", RouteFiles);
    }

    /// <summary>
    /// Обновляет отображение выбранных файлов норм
    /// Аналог Python label.config(text=...) для norm files
    /// </summary>
    private void UpdateNormFilesDisplay()
    {
        if (!NormFiles.Any())
        {
            NormFilesLabel.Text = "Не выбраны";
            NormFilesLabel.Foreground = System.Windows.Media.Brushes.Gray;
            return;
        }

        if (NormFiles.Length == 1)
        {
            NormFilesLabel.Text = System.IO.Path.GetFileName(NormFiles[0]);
        }
        else
        {
            NormFilesLabel.Text = $"{NormFiles.Length} файлов: {string.Join(", ", NormFiles.Take(2).Select(System.IO.Path.GetFileName))}" +
                                  (NormFiles.Length > 2 ? "..." : "");
        }

        NormFilesLabel.Foreground = System.Windows.Media.Brushes.Black;
        
        // Устанавливаем полный путь в ToolTip
        NormFilesLabel.ToolTip = string.Join("\n", NormFiles);
    }

    /// <summary>
    /// Показывает статусное сообщение - аналог Python update_status
    /// </summary>
    /// <param name="message">Текст сообщения</param>
    /// <param name="statusType">Тип статуса для цветового кодирования</param>
    public void ShowStatus(string message, StatusType statusType)
    {
        StatusText.Text = message;
        StatusIcon.Visibility = Visibility.Visible;

        // Устанавливаем цвет и иконку в соответствии с типом - аналог Python style_map
        switch (statusType)
        {
            case StatusType.Success:
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                StatusIcon.Kind = PackIconKind.CheckCircle;
                StatusIcon.Foreground = System.Windows.Media.Brushes.Green;
                break;
                
            case StatusType.Error:
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                StatusIcon.Kind = PackIconKind.AlertCircle;
                StatusIcon.Foreground = System.Windows.Media.Brushes.Red;
                break;
                
            case StatusType.Warning:
                StatusText.Foreground = System.Windows.Media.Brushes.Orange;
                StatusIcon.Kind = PackIconKind.Alert;
                StatusIcon.Foreground = System.Windows.Media.Brushes.Orange;
                break;
                
            case StatusType.Info:
            default:
                StatusText.Foreground = System.Windows.Media.Brushes.RoyalBlue;
                StatusIcon.Kind = PackIconKind.Information;
                StatusIcon.Foreground = System.Windows.Media.Brushes.RoyalBlue;
                break;
        }
    }

    /// <summary>
    /// Очищает статусное сообщение
    /// </summary>
    public void ClearStatus()
    {
        StatusText.Text = "";
        StatusIcon.Visibility = Visibility.Collapsed;
    }

    #endregion
}

/// <summary>
/// Типы статусных сообщений - аналог Python status_type параметра
/// </summary>
public enum StatusType
{
    Info,
    Success,
    Warning,
    Error
}