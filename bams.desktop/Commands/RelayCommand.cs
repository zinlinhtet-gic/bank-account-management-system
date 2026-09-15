using System.Windows.Input;

namespace bams.desktop.Commands;

/// <summary>
/// Provides a basic synchronous command implementation for ViewModel actions.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(
        Action<object?> execute,
        Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    // Returns whether the bound UI action can currently run.
    public bool CanExecute(
        object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    // Runs the ViewModel action associated with the command.
    public void Execute(
        object? parameter)
    {
        _execute(parameter);
    }

    // Notifies WPF that command availability may have changed.
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(
            this,
            EventArgs.Empty);
    }
}
