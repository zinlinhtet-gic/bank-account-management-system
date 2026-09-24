namespace bams.desktop.ViewModels;

/// <summary>
/// A ViewModel shown in a modal dialog by <see cref="Services.IDialogService.ShowDialog"/>.
/// Its view is found by an implicit <c>DataTemplate</c> in <c>Views/DialogTemplates.xaml</c>.
/// </summary>
public interface IDialogViewModel
{
    /// <summary>
    /// Raised by the ViewModel to close its dialog: true when the work completed (e.g. saved), false when cancelled.
    /// </summary>
    event Action<bool>? CloseRequested;

    /// <summary>
    /// Whether a click on the dimmed area closes the dialog. Keep false for forms so typed data is not lost by a stray click.
    /// </summary>
    bool CanCloseOnBackdropClick { get; }

    /// <summary>
    /// Whether Esc / the backdrop may cancel right now (false while a save is running).
    /// </summary>
    bool CanCancel { get; }
}
