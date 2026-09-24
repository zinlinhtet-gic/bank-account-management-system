using System.Windows.Input;

namespace bams.desktop.Commands;

/// <summary>
/// Command for ViewModel actions that call the server.
/// While the action runs, <see cref="CanExecute"/> returns false, so double-clicks cannot start it twice
/// and bound buttons are disabled automatically.
/// </summary>
/// <remarks>
/// The action must catch <c>AppException</c> itself and turn it into UI state (e.g. <c>ErrorMessage</c>).
/// This command does not swallow exceptions.
/// </remarks>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _executeAsync;
    private readonly Predicate<object?>? _canExecute;
    private bool _isRunning;

    // Creates a command for an action that does not need the command parameter.
    public AsyncRelayCommand(
        Func<Task> executeAsync,
        Func<bool>? canExecute = null)
        : this(
            _ => executeAsync(),
            canExecute is null ? null : _ => canExecute())
    {
    }

    // Creates a command whose action receives the command parameter (e.g. the clicked row's item).
    public AsyncRelayCommand(
        Func<object?, Task> executeAsync,
        Predicate<object?>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// True while the action is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    // Returns whether the action may start: not already running and allowed by the ViewModel.
    public bool CanExecute(
        object? parameter)
    {
        return !_isRunning && (_canExecute?.Invoke(parameter) ?? true);
    }

    // ICommand requires a void method; the awaited work is in ExecuteAsync.
    public async void Execute(
        object? parameter)
    {
        await ExecuteAsync(parameter);
    }

    /// <summary>
    /// Runs the action if it can currently execute. Useful to call from code (e.g. InitializeAsync).
    /// </summary>
    public async Task ExecuteAsync(
        object? parameter = null)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            // Block re-entry and disable bound buttons until the action finishes.
            _isRunning = true;
            RaiseCanExecuteChanged();

            await _executeAsync(parameter);
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    // Notifies WPF that command availability may have changed.
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(
            this,
            EventArgs.Empty);
    }
}
