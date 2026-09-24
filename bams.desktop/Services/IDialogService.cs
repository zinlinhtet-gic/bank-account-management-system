using System.Windows.Media;

namespace bams.desktop.Services;

/// <summary>
/// Text and look of a confirmation dialog.
/// </summary>
/// <param name="Title">Short question, e.g. "Log out?".</param>
/// <param name="Message">One or two sentences explaining what will happen.</param>
/// <param name="ConfirmText">Label of the confirm button, a verb: "Log out", "Disable user".</param>
/// <param name="CancelText">Label of the cancel button.</param>
/// <param name="IsDestructive">True for irreversible or risky actions: red icon and solid red confirm button.</param>
/// <param name="Icon">Optional icon geometry (an <c>Icon.*</c> resource); a default is used when null.</param>
/// <param name="IconKey">
/// Optional icon resource key such as "Icon.Key", for ViewModels that should not touch WPF resources.
/// Used when <paramref name="Icon"/> is null.
/// </param>
public sealed record ConfirmDialogOptions(
    string Title,
    string Message,
    string ConfirmText,
    string CancelText = "Cancel",
    bool IsDestructive = false,
    Geometry? Icon = null,
    string? IconKey = null);

/// <summary>
/// Shows themed modal dialogs. Use this instead of <c>MessageBox.Show</c> so every prompt matches the design
/// and ViewModels stay free of WPF window code.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation dialog over the main window and waits for the user's answer.
    /// </summary>
    /// <returns>True when the user confirmed; false when they cancelled, pressed Esc, or clicked outside.</returns>
    bool Confirm(ConfirmDialogOptions options);

    /// <summary>
    /// Shows a dialog ViewModel (a form, a detail card...) in a themed modal over the blurred main window.
    /// Its view is the <c>DataTemplate</c> for the ViewModel type in <c>Views/DialogTemplates.xaml</c>.
    /// </summary>
    /// <returns>True when the ViewModel closed itself as completed (e.g. saved); false when cancelled.</returns>
    bool ShowDialog(ViewModels.IDialogViewModel viewModel);
}
