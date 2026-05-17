using System.Windows.Input;

namespace String.Localization;

/// <summary>
/// Simpel ICommand-implementering to be used in MVVM.
/// </summary>
public class RelayCommand<T> : System.Windows.Input.ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool> _canExecute;
    public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
    {
        _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)ConvertParameter(parameter));

    public void Execute(object parameter)    => _execute((T)ConvertParameter(parameter));

    // Forward event subscriptions to CommandManager so the event is actually referenced/used.
    public event EventHandler CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    private static object ConvertParameter(object parameter)
    {
        if (parameter == null)
            return default(T)!;

        if (parameter is T t)
            return t;

        return System.Convert.ChangeType(parameter, typeof(T));
    }
}
