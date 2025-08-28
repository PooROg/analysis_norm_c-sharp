using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.UI.Windows;

/// <summary>
/// Диалог выбора локомотивов - аналог Python LocomotiveSelectorDialog
/// Обеспечивает фильтрацию по сериям и применение коэффициентов
/// </summary>
public partial class LocomotiveFilterWindow : Window
{
    private readonly LocomotiveFilterViewModel _viewModel;

    public LocomotiveFilterWindow(LocomotiveFilterViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        
        Loaded += LocomotiveFilterWindow_Loaded;
    }

    /// <summary>
    /// Результат диалога - аналог Python dialog.res
    /// </summary>
    public LocomotiveFilterResult? DialogResult { get; private set; }

    private async void LocomotiveFilterWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        CreateSeriesTabs();
    }

    #region Event Handlers - аналоги Python dialog methods

    /// <summary>
    /// Загружает файл коэффициентов - аналог Python load_coefficients_file
    /// </summary>
    private async void LoadCoefficientsFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите файл коэффициентов",
            Filter = "Excel файлы (*.xlsx;*.xls)|*.xlsx;*.xls|Все файлы (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.LoadCoefficientsFileAsync(dialog.FileName);
            UpdateCoefficientsDisplay();
            RecreateSeriesTabs(); // Обновляем отображение коэффициентов
        }
    }

    /// <summary>
    /// Очищает коэффициенты - аналог Python clear_coefficients
    /// </summary>
    private void ClearCoefficients_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearCoefficients();
        UpdateCoefficientsDisplay();
        RecreateSeriesTabs();
    }

    /// <summary>
    /// Выбирает все локомотивы - аналог Python select_all
    /// </summary>
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectAllLocomotives();
        UpdateAllTreeViews();
    }

    /// <summary>
    /// Снимает выделение со всех локомотивов - аналог Python deselect_all
    /// </summary>
    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeselectAllLocomotives();
        UpdateAllTreeViews();
    }

    /// <summary>
    /// Применяет фильтр и закрывает диалог - аналог Python ok
    /// </summary>
    private void ApplyFilter_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _viewModel.GetFilterResult();
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Отменяет изменения - аналог Python cancel
    /// </summary>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = null;
        DialogResult = false;
        Close();
    }

    #endregion

    #region Методы обновления UI

    /// <summary>
    /// Создает вкладки для серий локомотивов - аналог Python _build_series_tab
    /// </summary>
    private void CreateSeriesTabs()
    {
        SeriesTabControl.Items.Clear();

        foreach (var series in _viewModel.LocomotiveSeries.OrderBy(s => s))
        {
            var tabItem = new TabItem
            {
                Header = series,
                Tag = series
            };

            var treeView = CreateTreeViewForSeries(series);
            
            var scrollViewer = new ScrollViewer
            {
                Content = treeView,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            tabItem.Content = scrollViewer;
            SeriesTabControl.Items.Add(tabItem);
        }
    }

    /// <summary>
    /// Создает TreeView для конкретной серии - аналог Python TreeView creation
    /// </summary>
    private TreeView CreateTreeViewForSeries(string series)
    {
        var treeView = new TreeView
        {
            Margin = new Thickness(8),
            Tag = series
        };

        // Заголовок с чекбоксом для всей серии - аналог Python series checkbox
        var seriesHeader = new TreeViewItem
        {
            IsExpanded = true,
            Tag = $"series_{series}"
        };

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
        
        var seriesCheckBox = new CheckBox
        {
            Content = $"Серия {series} (выбрать всю серию)",
            IsChecked = _viewModel.IsSeriesSelected(series),
            Tag = series
        };
        seriesCheckBox.Checked += SeriesCheckBox_Changed;
        seriesCheckBox.Unchecked += SeriesCheckBox_Changed;
        
        headerPanel.Children.Add(seriesCheckBox);
        seriesHeader.Header = headerPanel;

        // Добавляем локомотивы серии
        var locomotives = _viewModel.GetLocomotivesForSeries(series);
        foreach (var loco in locomotives.OrderBy(l => l.Number))
        {
            var locoItem = new TreeViewItem
            {
                Tag = $"{series}_{loco.Number}"
            };

            var locoPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var locoCheckBox = new CheckBox
            {
                IsChecked = _viewModel.IsLocomotiveSelected(series, loco.Number),
                Tag = $"{series}_{loco.Number}",
                VerticalAlignment = VerticalAlignment.Center
            };
            locoCheckBox.Checked += LocomotiveCheckBox_Changed;
            locoCheckBox.Unchecked += LocomotiveCheckBox_Changed;
            
            var locoInfo = new TextBlock
            {
                Text = $"№{loco.Number}",
                Margin = new Thickness(8, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 60
            };

            var coefficientInfo = new TextBlock
            {
                Text = GetCoefficientDisplay(series, loco.Number),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11
            };

            // Цветовое кодирование коэффициентов - аналог Python color tags
            var coefficient = _viewModel.GetCoefficient(series, loco.Number);
            coefficientInfo.Foreground = GetCoefficientColor(coefficient);

            locoPanel.Children.Add(locoCheckBox);
            locoPanel.Children.Add(locoInfo);
            locoPanel.Children.Add(coefficientInfo);
            
            locoItem.Header = locoPanel;
            seriesHeader.Items.Add(locoItem);
        }

        treeView.Items.Add(seriesHeader);
        return treeView;
    }

    /// <summary>
    /// Получает отображаемое значение коэффициента
    /// </summary>
    private string GetCoefficientDisplay(string series, int number)
    {
        var coefficient = _viewModel.GetCoefficient(series, number);
        return coefficient != 1.0 ? $"K={coefficient:F3}" : "K=-";
    }

    /// <summary>
    /// Получает цвет для коэффициента - аналог Python tag colors
    /// </summary>
    private System.Windows.Media.Brush GetCoefficientColor(double coefficient)
    {
        return coefficient switch
        {
            > 1.05 => System.Windows.Media.Brushes.Red,      // above_norm
            < 0.95 => System.Windows.Media.Brushes.Blue,     // below_norm  
            _ => System.Windows.Media.Brushes.Green           // norm
        };
    }

    /// <summary>
    /// Обработчик изменения чекбокса серии - аналог Python series selection
    /// </summary>
    private void SeriesCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var checkBox = (CheckBox)sender;
        var series = (string)checkBox.Tag;
        var isChecked = checkBox.IsChecked == true;

        _viewModel.SetSeriesSelection(series, isChecked);
        UpdateTreeViewForSeries(series);
        UpdateSelectionCount();
    }

    /// <summary>
    /// Обработчик изменения чекбокса локомотива
    /// </summary>
    private void LocomotiveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var checkBox = (CheckBox)sender;
        var tag = (string)checkBox.Tag;
        var parts = tag.Split('_');
        var series = parts[0];
        var number = int.Parse(parts[1]);
        var isChecked = checkBox.IsChecked == true;

        _viewModel.SetLocomotiveSelection(series, number, isChecked);
        UpdateSeriesCheckBox(series);
        UpdateSelectionCount();
    }

    /// <summary>
    /// Обновляет отображение коэффициентов - аналог Python update_coefficients_in_place
    /// </summary>
    private void UpdateCoefficientsDisplay()
    {
        if (_viewModel.CoefficientsLoaded)
        {
            CoefficientsFileLabel.Text = System.IO.Path.GetFileName(_viewModel.CoefficientsFileName);
            CoefficientsFileLabel.Foreground = System.Windows.Media.Brushes.Black;
            
            var stats = _viewModel.GetCoefficientsStatistics();
            CoefficientsStatusLabel.Text = $"Загружено: {stats.TotalLocomotives} локомотивов, " +
                                          $"{stats.SeriesCount} серий. " +
                                          $"Средн. откл.: {stats.AverageDeviationPercent:F1}%";
        }
        else
        {
            CoefficientsFileLabel.Text = "Не загружен";
            CoefficientsFileLabel.Foreground = System.Windows.Media.Brushes.Gray;
            CoefficientsStatusLabel.Text = "";
        }
    }

    /// <summary>
    /// Пересоздает вкладки серий с обновленными коэффициентами
    /// </summary>
    private void RecreateSeriesTabs()
    {
        CreateSeriesTabs();
        UpdateSelectionCount();
    }

    /// <summary>
    /// Обновляет все TreeView'ы
    /// </summary>
    private void UpdateAllTreeViews()
    {
        foreach (TabItem tabItem in SeriesTabControl.Items)
        {
            var series = (string)tabItem.Tag;
            UpdateTreeViewForSeries(series);
        }
        UpdateSelectionCount();
    }

    /// <summary>
    /// Обновляет TreeView для конкретной серии
    /// </summary>
    private void UpdateTreeViewForSeries(string series)
    {
        var tabItem = SeriesTabControl.Items.Cast<TabItem>()
                          .FirstOrDefault(t => (string)t.Tag == series);
        
        if (tabItem?.Content is ScrollViewer { Content: TreeView treeView })
        {
            foreach (TreeViewItem seriesItem in treeView.Items)
            {
                if (seriesItem.Header is StackPanel { Children: [CheckBox seriesCheckBox, ..] })
                {
                    seriesCheckBox.IsChecked = _viewModel.IsSeriesSelected(series);
                }

                foreach (TreeViewItem locoItem in seriesItem.Items)
                {
                    if (locoItem.Header is StackPanel { Children: [CheckBox locoCheckBox, ..] })
                    {
                        var tag = (string)locoCheckBox.Tag;
                        var parts = tag.Split('_');
                        var number = int.Parse(parts[1]);
                        locoCheckBox.IsChecked = _viewModel.IsLocomotiveSelected(series, number);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Обновляет чекбокс серии на основе состояния локомотивов
    /// </summary>
    private void UpdateSeriesCheckBox(string series)
    {
        var locomotives = _viewModel.GetLocomotivesForSeries(series);
        var selectedCount = locomotives.Count(l => _viewModel.IsLocomotiveSelected(series, l.Number));
        
        var tabItem = SeriesTabControl.Items.Cast<TabItem>()
                          .FirstOrDefault(t => (string)t.Tag == series);
        
        if (tabItem?.Content is ScrollViewer { Content: TreeView treeView })
        {
            foreach (TreeViewItem seriesItem in treeView.Items)
            {
                if (seriesItem.Header is StackPanel { Children: [CheckBox seriesCheckBox, ..] })
                {
                    seriesCheckBox.IsChecked = selectedCount switch
                    {
                        0 => false,
                        var count when count == locomotives.Count => true,
                        _ => null // Частичное выделение
                    };
                }
            }
        }
    }

    /// <summary>
    /// Обновляет счетчик выбранных локомотивов - аналог Python update_selection_count
    /// </summary>
    private void UpdateSelectionCount()
    {
        var total = _viewModel.TotalLocomotives;
        var selected = _viewModel.SelectedCount;
        SelectionLabel.Text = $"Выбрано: {selected} из {total}";
    }

    #endregion
}

/// <summary>
/// ViewModel для диалога фильтра локомотивов - аналог Python LocomotiveSelectorDialog logic
/// </summary>
public class LocomotiveFilterViewModel : INotifyPropertyChanged
{
    #region Поля

    private readonly ILogger<LocomotiveFilterViewModel> _logger;
    private readonly ILocomotiveFilterService _filterService;
    
    private List<Route> _routes = new();
    private List<LocomotiveCoefficient> _coefficients = new();
    private HashSet<(string Series, int Number)> _selectedLocomotives = new();
    
    private bool _useCoefficients;
    private bool _excludeLowWork;
    private bool _coefficientsLoaded;
    private string _coefficientsFileName = string.Empty;

    #endregion

    #region Конструктор

    public LocomotiveFilterViewModel(
        ILogger<LocomotiveFilterViewModel> logger,
        ILocomotiveFilterService filterService,
        List<Route> routes)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Использовать коэффициенты при анализе - аналог Python use_coefficients
    /// </summary>
    public bool UseCoefficients
    {
        get => _useCoefficients;
        set => SetProperty(ref _useCoefficients, value);
    }

    /// <summary>
    /// Исключать локомотивы с низкой работой - аналог Python exclude_low_work
    /// </summary>
    public bool ExcludeLowWork
    {
        get => _excludeLowWork;
        set => SetProperty(ref _excludeLowWork, value);
    }

    /// <summary>
    /// Загружены ли коэффициенты
    /// </summary>
    public bool CoefficientsLoaded
    {
        get => _coefficientsLoaded;
        private set => SetProperty(ref _coefficientsLoaded, value);
    }

    /// <summary>
    /// Имя файла коэффициентов
    /// </summary>
    public string CoefficientsFileName
    {
        get => _coefficientsFileName;
        private set => SetProperty(ref _coefficientsFileName, value);
    }

    /// <summary>
    /// Уникальные серии локомотивов
    /// </summary>
    public List<string> LocomotiveSeries { get; private set; } = new();

    /// <summary>
    /// Общее количество локомотивов
    /// </summary>
    public int TotalLocomotives { get; private set; }

    /// <summary>
    /// Количество выбранных локомотивов
    /// </summary>
    public int SelectedCount => _selectedLocomotives.Count;

    #endregion

    #region Методы инициализации

    /// <summary>
    /// Инициализирует данные диалога - аналог Python dialog initialization
    /// </summary>
    public async Task InitializeAsync()
    {
        await Task.Run(() =>
        {
            // Извлекаем уникальные серии из маршрутов
            LocomotiveSeries = _routes
                .Where(r => !string.IsNullOrEmpty(r.LocomotiveSeries))
                .Select(r => r.LocomotiveSeries)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            // Подсчитываем общее количество уникальных локомотивов
            var uniqueLocomotives = _routes
                .Where(r => !string.IsNullOrEmpty(r.LocomotiveSeries) && r.LocomotiveNumber > 0)
                .Select(r => (r.LocomotiveSeries, r.LocomotiveNumber))
                .Distinct();

            TotalLocomotives = uniqueLocomotives.Count();

            // По умолчанию все локомотивы выбраны - аналог Python default selection
            _selectedLocomotives = uniqueLocomotives.ToHashSet();

            _logger.LogInformation("Инициализирован диалог фильтра: {SeriesCount} серий, {LocomotiveCount} локомотивов", 
                LocomotiveSeries.Count, TotalLocomotives);
        });
    }

    #endregion

    #region Методы работы с выборкой - аналоги Python selection methods

    /// <summary>
    /// Проверяет, выбрана ли серия целиком
    /// </summary>
    public bool IsSeriesSelected(string series)
    {
        var locomotives = GetLocomotivesForSeries(series);
        return locomotives.All(l => _selectedLocomotives.Contains((series, l.Number)));
    }

    /// <summary>
    /// Проверяет, выбран ли конкретный локомотив
    /// </summary>
    public bool IsLocomotiveSelected(string series, int number)
    {
        return _selectedLocomotives.Contains((series, number));
    }

    /// <summary>
    /// Устанавливает выбор для всей серии
    /// </summary>
    public void SetSeriesSelection(string series, bool selected)
    {
        var locomotives = GetLocomotivesForSeries(series);
        
        foreach (var loco in locomotives)
        {
            if (selected)
                _selectedLocomotives.Add((series, loco.Number));
            else
                _selectedLocomotives.Remove((series, loco.Number));
        }
        
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// Устанавливает выбор для конкретного локомотива
    /// </summary>
    public void SetLocomotiveSelection(string series, int number, bool selected)
    {
        if (selected)
            _selectedLocomotives.Add((series, number));
        else
            _selectedLocomotives.Remove((series, number));
            
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// Выбирает все локомотивы - аналог Python select_all
    /// </summary>
    public void SelectAllLocomotives()
    {
        _selectedLocomotives = _routes
            .Where(r => !string.IsNullOrEmpty(r.LocomotiveSeries) && r.LocomotiveNumber > 0)
            .Select(r => (r.LocomotiveSeries, r.LocomotiveNumber))
            .Distinct()
            .ToHashSet();
            
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// Снимает выделение со всех локомотивов - аналог Python deselect_all
    /// </summary>
    public void DeselectAllLocomotives()
    {
        _selectedLocomotives.Clear();
        OnPropertyChanged(nameof(SelectedCount));
    }

    #endregion

    #region Методы работы с коэффициентами

    /// <summary>
    /// Загружает файл коэффициентов - аналог Python load_coefficients
    /// </summary>
    public async Task LoadCoefficientsFileAsync(string filePath)
    {
        try
        {
            _coefficients = await _filterService.LoadCoefficientsAsync(filePath);
            CoefficientsLoaded = true;
            CoefficientsFileName = filePath;
            
            _logger.LogInformation("Загружены коэффициенты из файла: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки коэффициентов");
            CoefficientsLoaded = false;
            CoefficientsFileName = string.Empty;
            throw;
        }
    }

    /// <summary>
    /// Очищает загруженные коэффициенты
    /// </summary>
    public void ClearCoefficients()
    {
        _coefficients.Clear();
        CoefficientsLoaded = false;
        CoefficientsFileName = string.Empty;
        
        _logger.LogInformation("Коэффициенты очищены");
    }

    /// <summary>
    /// Получает коэффициент для локомотива
    /// </summary>
    public double GetCoefficient(string series, int number)
    {
        var coefficient = _coefficients
            .FirstOrDefault(c => c.Series == series && c.Number == number);
        return coefficient?.Value ?? 1.0;
    }

    /// <summary>
    /// Получает статистику коэффициентов
    /// </summary>
    public (int TotalLocomotives, int SeriesCount, double AverageDeviationPercent) GetCoefficientsStatistics()
    {
        if (!_coefficients.Any())
            return (0, 0, 0);

        var totalCount = _coefficients.Count;
        var seriesCount = _coefficients.Select(c => c.Series).Distinct().Count();
        var avgDeviation = _coefficients.Average(c => Math.Abs(c.Value - 1.0) * 100);

        return (totalCount, seriesCount, avgDeviation);
    }

    #endregion

    #region Методы получения данных

    /// <summary>
    /// Получает локомотивы для серии
    /// </summary>
    public List<(int Number, double Coefficient)> GetLocomotivesForSeries(string series)
    {
        return _routes
            .Where(r => r.LocomotiveSeries == series && r.LocomotiveNumber > 0)
            .Select(r => r.LocomotiveNumber)
            .Distinct()
            .Select(num => (num, GetCoefficient(series, num)))
            .OrderBy(x => x.num)
            .ToList();
    }

    /// <summary>
    /// Возвращает результат фильтрации - аналог Python dialog result
    /// </summary>
    public LocomotiveFilterResult GetFilterResult()
    {
        return new LocomotiveFilterResult
        {
            SelectedLocomotives = _selectedLocomotives.ToList(),
            UseCoefficients = UseCoefficients,
            ExcludeLowWork = ExcludeLowWork,
            Coefficients = _coefficients.ToList()
        };
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}

/// <summary>
/// Результат работы диалога фильтра локомотивов - аналог Python dialog result
/// </summary>
public class LocomotiveFilterResult
{
    public List<(string Series, int Number)> SelectedLocomotives { get; set; } = new();
    public bool UseCoefficients { get; set; }
    public bool ExcludeLowWork { get; set; }
    public List<LocomotiveCoefficient> Coefficients { get; set; } = new();
}