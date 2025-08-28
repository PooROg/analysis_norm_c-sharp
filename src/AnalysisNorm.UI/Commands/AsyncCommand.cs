using System.Windows.Input;

namespace AnalysisNorm.UI.Commands;

/// <summary>
/// Асинхронная команда для длительных операций - аналог Python threading + callback
/// Предотвращает блокировку UI при выполнении тяжелых операций
/// </summary>
public class AsyncCommand : ICommand
{
    private readonly Func<object?, Task> _executeAsync;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        : this(_ => executeAsync(), canExecute)
    {
    }

    public AsyncCommand(Func<object?, Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;

        try
        {
            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();
            
            await _executeAsync(parameter);
        }
        catch (Exception ex)
        {
            // Логирование ошибок команды
            System.Diagnostics.Debug.WriteLine($"AsyncCommand error: {ex}");
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Принудительно обновляет состояние CanExecute
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>
/// Синхронная команда для быстрых UI операций - аналог Python simple callbacks
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute)
    {
    }

    public RelayCommand(Action<object?> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        try
        {
            _execute(parameter);
        }
        catch (Exception ex)
        {
            // Логирование ошибок команды
            System.Diagnostics.Debug.WriteLine($"RelayCommand error: {ex}");
        }
    }

    /// <summary>
    /// Принудительно обновляет состояние CanExecute
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}