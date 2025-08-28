using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using AnalysisNorm.UI.ViewModels;
using MaterialDesignThemes.Wpf;

namespace AnalysisNorm.UI;

/// <summary>
/// Главное окно приложения - аналог Python NormsAnalyzerGUI класса
/// Минимальный код-behind в соответствии с MVVM паттерном
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    /// <summary>
    /// Конструктор главного окна с DI поддержкой
    /// Эквивалент Python __init__(self, root: tk.Tk)
    /// </summary>
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        
        // Устанавливаем DataContext для привязки данных
        DataContext = _viewModel;
        
        // Настраиваем тему MaterialDesign аналогично Python _setup_styles()
        SetupMaterialDesignTheme();
        
        // Подписываемся на события закрытия аналогично Python on_closing()
        Closing += MainWindow_Closing;
        
        // Инициализируем ViewModel после привязки DataContext
        Loaded += MainWindow_Loaded;
    }

    /// <summary>
    /// Настройка темы MaterialDesign - аналог Python _setup_styles()
    /// Конфигурирует цветовую схему и стили интерфейса
    /// </summary>
    private void SetupMaterialDesignTheme()
    {
        try
        {
            // Настраиваем основную цветовую палитру
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            
            // Устанавливаем светлую тему по умолчанию
            theme.SetBaseTheme(Theme.Light);
            
            // Настраиваем акцентный цвет для элементов интерфейса
            theme.SetPrimaryColor(Colors.Blue);
            theme.SetSecondaryColor(Colors.DeepOrange);
            
            paletteHelper.SetTheme(theme);
        }
        catch (Exception ex)
        {
            // Логируем ошибки настройки темы, но не прерываем запуск
            System.Diagnostics.Debug.WriteLine($"Ошибка настройки темы MaterialDesign: {ex.Message}");
        }
    }

    /// <summary>
    /// Обработчик события загрузки окна
    /// Инициализирует ViewModel после полной загрузки интерфейса
    /// </summary>
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Инициализируем ViewModel - аналог Python _setup_gui() + _connect_callbacks()
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            // Показываем пользователю критические ошибки инициализации
            MessageBox.Show(
                $"Ошибка инициализации приложения:\n{ex.Message}",
                "Критическая ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            
            // Закрываем приложение при критических ошибках
            Application.Current.Shutdown(1);
        }
    }

    /// <summary>
    /// Обработчик закрытия окна - аналог Python on_closing()
    /// Выполняет корректную очистку ресурсов перед закрытием
    /// </summary>
    private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // Показываем индикатор загрузки во время очистки
            _viewModel.IsLoading = true;
            _viewModel.LoadingMessage = "Завершение работы...";
            
            // Вызываем асинхронную очистку ресурсов ViewModel
            await _viewModel.ShutdownAsync();
        }
        catch (Exception ex)
        {
            // Логируем ошибки, но не блокируем закрытие
            System.Diagnostics.Debug.WriteLine($"Ошибка при завершении работы: {ex.Message}");
        }
    }

    /// <summary>
    /// Показывает уведомление пользователю
    /// Используется ViewModel для отображения системных сообщений
    /// </summary>
    /// <param name="message">Текст сообщения</param>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="isError">Является ли сообщение ошибкой</param>
    public void ShowNotification(string message, string title = "Информация", bool isError = false)
    {
        var icon = isError ? MessageBoxImage.Error : MessageBoxImage.Information;
        MessageBox.Show(message, title, MessageBoxButton.OK, icon);
    }

    /// <summary>
    /// Показывает диалог подтверждения действия
    /// Используется для критических операций вроде очистки данных
    /// </summary>
    /// <param name="message">Текст вопроса</param>
    /// <param name="title">Заголовок диалога</param>
    /// <returns>True если пользователь подтвердил действие</returns>
    public bool ShowConfirmationDialog(string message, string title = "Подтверждение")
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
}