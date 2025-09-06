using System.Windows;
using AnalysisNorm.ViewModels;

namespace AnalysisNorm.Views;

/// <summary>
/// Главное окно приложения с минимальным code-behind
/// Вся логика выносится в MainViewModel согласно MVVM паттерну
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Инициализация главного окна через DI
    /// </summary>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        
        // Привязка ViewModel через DI
        DataContext = viewModel;
        
        // Подписка на событие закрытия для корректной очистки ресурсов
        Closing += MainWindow_Closing;
    }

    /// <summary>
    /// Обработка закрытия окна с уведомлением ViewModel
    /// </summary>
    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            // Даем ViewModel возможность выполнить cleanup
            await viewModel.OnWindowClosingAsync();
        }
    }
}