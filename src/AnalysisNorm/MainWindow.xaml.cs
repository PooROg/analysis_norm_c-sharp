// MainWindow.xaml.cs - ПОЛНОЕ ИСПРАВЛЕНИЕ для Этапа 1
using System;                               // Добавлено для Exception, EventArgs, ArgumentNullException
using System.ComponentModel;                // Добавлено для CancelEventArgs
using System.Threading.Tasks;               // Добавлено для Task
using System.Windows;                       // Уже было
using System.Windows.Controls;              // Добавлено для SizeChangedEventArgs

// ВРЕМЕННО отключены до создания собственных пространств имен
// using AnalysisNorm.ViewModels;          // Будет включено на этапе 2

namespace AnalysisNorm;

/// <summary>
/// ЭТАП 1: Полностью исправленное главное окно приложения
/// Убраны все зависимости от несуществующих классов
/// Минимальный code-behind до создания ViewModels
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Конструктор по умолчанию
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        
        // Подписка на события окна
        Closing += MainWindow_Closing;
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
        
        // Установка базовых свойств окна
        Title = "Анализатор норм расхода электроэнергии РЖД v3.4 (Этап 1)";
    }

    /// <summary>
    /// Обработка закрытия окна - упрощенная версия без ViewModel
    /// </summary>
    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        try
        {
            // Простое подтверждение закрытия без проверки активных операций
            var result = MessageBox.Show(
                "Вы уверены, что хотите закрыть приложение?",
                "Подтверждение закрытия",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            // Базовая очистка ресурсов с минимальной задержкой
            await Task.Delay(50); // Даем время на завершение операций
        }
        catch (Exception ex)
        {
            // Логируем ошибку в Debug консоль, но не блокируем закрытие
            System.Diagnostics.Debug.WriteLine($"Ошибка при закрытии окна: {ex.Message}");
        }
    }

    /// <summary>
    /// Обработка изменения позиции окна
    /// ЭТАП 1: Заглушка - будет реализовано через UserPreferencesService на этапе 3
    /// </summary>
    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        // TODO: Сохранение позиции окна будет реализовано через UserPreferencesService
        // в следующих этапах после создания инфраструктуры
    }

    /// <summary>
    /// Обработка изменения размера окна
    /// ЭТАП 1: Заглушка - будет реализовано через UserPreferencesService на этапе 3
    /// </summary>
    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // TODO: Сохранение размера окна будет реализовано через UserPreferencesService
        // в следующих этапах после создания инфраструктуры
    }

    /// <summary>
    /// Обработка изменения состояния окна (максимизация/восстановление)
    /// ЭТАП 1: Заглушка - будет реализовано через UserPreferencesService на этапе 3
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // TODO: Сохранение состояния окна будет реализовано через UserPreferencesService
        // в следующих этапах после создания инфраструктуры
    }

    // ===== МЕТОДЫ ДЛЯ БУДУЩИХ ЭТАПОВ (ВРЕМЕННО ЗАКОММЕНТИРОВАНЫ) =====
    
    /*
    /// <summary>
    /// ЭТАП 2: Установка ViewModel через DI
    /// Будет раскомментирован после создания EnhancedMainViewModel
    /// </summary>
    public void SetViewModel(EnhancedMainViewModel viewModel)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    /// <summary>
    /// ЭТАП 3: Применение настроек окна из конфигурации
    /// Будет раскомментирован после создания WindowPosition класса
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
    /// ЭТАП 3: Получение текущих настроек окна
    /// Будет раскомментирован после создания WindowPosition класса
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
    /// ЭТАП 2: Альтернативный конструктор с явной передачей ViewModel
    /// Будет раскомментирован после создания EnhancedMainViewModel
    /// </summary>
    public MainWindow(EnhancedMainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
    */

    // ===== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ =====

    /// <summary>
    /// Метод для отображения простых сообщений пользователю
    /// Используется для отладки на этапе 1
    /// </summary>
    private void ShowMessage(string message, string title = "Информация")
    {
        try
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка отображения сообщения: {ex.Message}");
        }
    }

    /// <summary>
    /// Метод для безопасного логирования на этапе 1
    /// Будет заменен на полноценный логгер на этапе 2
    /// </summary>
    private void LogMessage(string message, string level = "Info")
    {
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] [{level}] MainWindow: {message}";
            System.Diagnostics.Debug.WriteLine(logEntry);
            
            // TODO: На этапе 2 заменить на полноценный логгер
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка логирования: {ex.Message}");
        }
    }

    /// <summary>
    /// Проверка корректности состояния окна
    /// Диагностический метод для этапа 1
    /// </summary>
    private bool ValidateWindowState()
    {
        try
        {
            // Базовые проверки состояния окна
            if (Width <= 0 || Height <= 0)
            {
                LogMessage("Некорректные размеры окна", "Warning");
                return false;
            }

            if (Left < -Width || Top < -Height)
            {
                LogMessage("Окно за пределами экрана", "Warning");
                return false;
            }

            LogMessage("Состояние окна корректно", "Debug");
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Ошибка валидации состояния окна: {ex.Message}", "Error");
            return false;
        }
    }
}