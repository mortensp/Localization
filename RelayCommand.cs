using System.Windows.Input;

namespace Localization;

/// <summary>
/// Simpel ICommand-implementering to be used in MVVM.
/// </summary>
public class RelayCommand<T> : System.Windows.Input.ICommand
{
    private readonly Action<T>      _execute;
    private readonly Func<T, bool> _canExecute;
    public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
    {
        _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)ConvertParameter(parameter));

    public void Execute(object   parameter)    => _execute((T)ConvertParameter(parameter));

    public event EventHandler CanExecuteChanged;

    private static object ConvertParameter(object parameter)
    {
        if (parameter == null)
            return default(T)!;

        if (parameter is T t)
            return t;

        return System.Convert.ChangeType(parameter, typeof(T));
    }
}
