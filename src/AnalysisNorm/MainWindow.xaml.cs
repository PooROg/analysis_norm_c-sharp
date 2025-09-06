// MainWindow.xaml.cs - ИСПРАВЛЕН для CHAT 3-4
using System.Windows;
using AnalysisNorm.ViewModels;

namespace AnalysisNorm;

/// <summary>
/// ИСПРАВЛЕННОЕ главное окно приложения для CHAT 3-4
/// Интегрируется с EnhancedMainViewModel и новой функциональностью
/// Минимальный code-behind согласно MVVM паттерну
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Конструктор по умолчанию - ViewModel будет установлена через DI
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        
        // ViewModel будет установлена через DI в App.xaml.cs
        // DataContext устанавливается в App.CreateAndShowMainWindowAsync()
        
        // Подписка на событие закрытия для корректной очистки ресурсов
        Closing += MainWindow_Closing;
        
        // Подписка на события для сохранения настроек окна
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
    }

    /// <summary>
    /// Альтернативный конструктор с явной передачей ViewModel (для совместимости)
    /// </summary>
    public MainWindow(EnhancedMainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    /// <summary>
    /// Обработка закрытия окна с уведомлением ViewModel
    /// </summary>
    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            if (DataContext is EnhancedMainViewModel viewModel)
            {
                // Проверяем, есть ли активные операции
                if (viewModel.IsProcessing)
                {
                    var result = MessageBox.Show(
                        "Выполняется обработка данных. Вы уверены, что хотите закрыть приложение?",
                        "Подтверждение закрытия",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                // Даем ViewModel возможность выполнить cleanup
                // await viewModel.OnWindowClosingAsync(); // Будет реализован в полной версии
            }
        }
        catch (Exception ex)
        {
            // Логируем ошибку, но не блокируем закрытие
            System.Diagnostics.Debug.WriteLine($"Ошибка при закрытии окна: {ex.Message}");
        }
    }

    /// <summary>
    /// Обработка изменения позиции окна для автосохранения
    /// </summary>
    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        // Сохранение позиции окна будет реализовано через UserPreferencesService
        // в будущих версиях
    }

    /// <summary>
    /// Обработка изменения размера окна для автосохранения
    /// </summary>
    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Сохранение размера окна будет реализовано через UserPreferencesService
        // в будущих версиях
    }

    /// <summary>
    /// Обработка изменения состояния окна (максимизация/восстановление)
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // Сохранение состояния окна будет реализовано через UserPreferencesService
        // в будущих версиях
    }

    /// <summary>
    /// НОВЫЙ метод: Установка ViewModel через DI
    /// Вызывается из App.xaml.cs после создания окна
    /// </summary>
    public void SetViewModel(EnhancedMainViewModel viewModel)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    /// <summary>
    /// НОВЫЙ метод: Применение настроек окна из конфигурации
    /// </summary>
    public void ApplyWindowSettings(WindowPosition position)
    {
        try
        {
            if (position.Width > 0 && position.Height > 0)
            {
                Width = position.Width;
                Height = position.Height;
            }

            if (position.X >= 0 && position.Y >= 0)
            {
                // Проверяем, что окно будет видимо на экране
                var workingArea = SystemParameters.WorkArea;
                if (position.X < workingArea.Width && position.Y < workingArea.Height)
                {
                    Left = position.X;
                    Top = position.Y;
                }
            }

            if (position.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка применения настроек окна: {ex.Message}");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Получение текущих настроек окна
    /// </summary>
    public WindowPosition GetCurrentWindowSettings()
    {
        try
        {
            return new WindowPosition
            {
                X = (int)Left,
                Y = (int)Top,
                Width = (int)Width,
                Height = (int)Height,
                IsMaximized = WindowState == WindowState.Maximized
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка получения настроек окна: {ex.Message}");
            return new WindowPosition(); // Возвращаем настройки по умолчанию
        }
    }

    /// <summary>
    /// Обработка нажатия клавиш для горячих клавиш
    /// </summary>
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        try
        {
            // Горячие клавиши для основных функций
            if (e.Key == System.Windows.Input.Key.F5)
            {
                // F5 - Обновить/Диагностика
                if (DataContext is EnhancedMainViewModel viewModel)
                {
                    viewModel.RunDiagnosticsCommand?.Execute(null);
                }
                e.Handled = true;
            }
            else if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case System.Windows.Input.Key.O:
                        // Ctrl+O - Открыть файлы маршрутов
                        if (DataContext is EnhancedMainViewModel viewModel1)
                        {
                            viewModel1.LoadRoutesCommand?.Execute(null);
                        }
                        e.Handled = true;
                        break;

                    case System.Windows.Input.Key.S:
                        // Ctrl+S - Экспорт в Excel
                        if (DataContext is EnhancedMainViewModel viewModel2)
                        {
                            viewModel2.ExportToExcelCommand?.Execute(null);
                        }
                        e.Handled = true;
                        break;

                    case System.Windows.Input.Key.D:
                        // Ctrl+D - Диагностика
                        if (DataContext is EnhancedMainViewModel viewModel3)
                        {
                            viewModel3.RunDiagnosticsCommand?.Execute(null);
                        }
                        e.Handled = true;
                        break;
                }
            }

            base.OnKeyDown(e);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка обработки горячих клавиш: {ex.Message}");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Показать диалог прогресса
    /// </summary>
    public void ShowProgressDialog(string title, string message)
    {
        try
        {
            // В будущих версиях здесь будет показ диалога прогресса
            // с использованием MaterialDesign DialogHost
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка показа диалога прогресса: {ex.Message}");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Скрыть диалог прогресса
    /// </summary>
    public void HideProgressDialog()
    {
        try
        {
            // В будущих версиях здесь будет скрытие диалога прогресса
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка скрытия диалога прогресса: {ex.Message}");
        }
    }

    /// <summary>
    /// НОВЫЙ метод: Показать уведомление
    /// </summary>
    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Information)
    {
        try
        {
            // Простое уведомление через MessageBox (в будущем - через Snackbar)
            var icon = type switch
            {
                NotificationType.Information => MessageBoxImage.Information,
                NotificationType.Warning => MessageBoxImage.Warning,
                NotificationType.Error => MessageBoxImage.Error,
                NotificationType.Success => MessageBoxImage.Information,
                _ => MessageBoxImage.Information
            };

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка показа уведомления: {ex.Message}");
        }
    }
}

/// <summary>
/// Типы уведомлений для UI
/// </summary>
public enum NotificationType
{
    Information,
    Warning,
    Error,
    Success
}

/// <summary>
/// Настройки позиции и размера окна (дублируется из новых интерфейсов)
/// </summary>
public record WindowPosition
{
    public int X { get; init; } = 100;
    public int Y { get; init; } = 100;
    public int Width { get; init; } = 1600;
    public int Height { get; init; } = 1000;
    public bool IsMaximized { get; init; } = false;
}