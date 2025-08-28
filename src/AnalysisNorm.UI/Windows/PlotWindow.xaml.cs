using System.Windows;
using OxyPlot;
using AnalysisNorm.UI.ViewModels;
using MaterialDesignThemes.Wpf;

namespace AnalysisNorm.UI.Windows;

/// <summary>
/// Окно интерактивных графиков - аналог Python Plotly browser window
/// Реализует dual subplot структуру с OxyPlot для анализа норм
/// </summary>
public partial class PlotWindow : Window
{
    private readonly PlotWindowViewModel _viewModel;

    /// <summary>
    /// Конструктор окна графиков с ViewModel
    /// Аналог Python PlotBuilder.create_interactive_plot()
    /// </summary>
    public PlotWindow(PlotWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        
        // Устанавливаем DataContext для привязки данных
        DataContext = _viewModel;
        
        // Настраиваем обработчики событий OxyPlot - аналог Python hover/click handlers
        SetupPlotEventHandlers();
        
        // Инициализируем ViewModel после привязки DataContext
        Loaded += PlotWindow_Loaded;
        
        // Обработчик закрытия окна
        Closing += PlotWindow_Closing;
    }

    /// <summary>
    /// Настраивает обработчики событий для интерактивности графиков
    /// Аналог Python interactive callbacks и hover handlers
    /// </summary>
    private void SetupPlotEventHandlers()
    {
        // Обработчики для главного графика норм
        NormsPlotView.MouseDown += (sender, e) => _viewModel.OnNormsPlotMouseDown(e);
        NormsPlotView.MouseMove += (sender, e) => _viewModel.OnNormsPlotMouseMove(e);
        
        // Обработчики для графика отклонений
        DeviationsPlotView.MouseDown += (sender, e) => _viewModel.OnDeviationsPlotMouseDown(e);
        DeviationsPlotView.MouseMove += (sender, e) => _viewModel.OnDeviationsPlotMouseMove(e);
    }

    /// <summary>
    /// Инициализация окна после загрузки
    /// </summary>
    private async void PlotWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Инициализируем ViewModel и строим графики
            await _viewModel.InitializePlotsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка инициализации графиков:\n{ex.Message}",
                "Ошибка графиков",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Close();
        }
    }

    /// <summary>
    /// Обработчик закрытия окна графиков
    /// </summary>
    private void PlotWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // Очистка ресурсов ViewModel
            _viewModel?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при закрытии окна графиков: {ex.Message}");
        }
    }

    /// <summary>
    /// Обработчик кнопки закрытия в toolbar
    /// </summary>
    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Показывает модальное окно с детальной информацией о точке
    /// Аналог Python modal dialogs для hover information
    /// </summary>
    /// <param name="title">Заголовок окна</param>
    /// <param name="content">Содержимое окна</param>
    public void ShowPointDetailsDialog(string title, string content)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 600,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.CanResize,
            Background = new SolidColorBrush(Colors.White),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };

        var mainPanel = new StackPanel { Margin = new Thickness(16) };

        // Заголовок
        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        mainPanel.Children.Add(titleBlock);

        // Содержимое в скроллируемом текстовом поле
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var contentTextBox = new TextBox
        {
            Text = content,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 11,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8)
        };

        scrollViewer.Content = contentTextBox;
        mainPanel.Children.Add(scrollViewer);

        // Кнопка закрытия
        var closeButton = new Button
        {
            Content = "Закрыть",
            Style = (Style)FindResource("MaterialDesignRaisedButton"),
            Margin = new Thickness(0, 12, 0, 0),
            Padding = new Thickness(20, 8),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        closeButton.Click += (s, e) => dialog.Close();
        mainPanel.Children.Add(closeButton);

        dialog.Content = mainPanel;
        dialog.ShowDialog();
    }

    /// <summary>
    /// Экспортирует графики в файл изображения
    /// Аналог Python plot export functionality
    /// </summary>
    public async Task ExportPlotsAsync(string filePath, int width = 1200, int height = 800)
    {
        try
        {
            // Создаем bitmap с обоими графиками
            var renderTargetBitmap = new RenderTargetBitmap(
                width, height, 
                96, 96, 
                PixelFormats.Pbgra32
            );

            // Рендерим основной контент окна
            var plotsGrid = (Grid)Content;
            plotsGrid.Measure(new Size(width, height));
            plotsGrid.Arrange(new Rect(0, 0, width, height));
            
            renderTargetBitmap.Render(plotsGrid);

            // Сохраняем в файл
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderTargetBitmap));

            using var fileStream = new FileStream(filePath, FileMode.Create);
            encoder.Save(fileStream);

            MessageBox.Show(
                $"График успешно экспортирован в файл:\n{filePath}",
                "Экспорт завершен",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка экспорта графика:\n{ex.Message}",
                "Ошибка экспорта",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
}