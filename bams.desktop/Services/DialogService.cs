using System.Windows;
using bams.desktop.Components.ConfirmDialog;

namespace bams.desktop.Services;

/// <summary>
/// Opens themed dialogs over the application's main window.
/// </summary>
public sealed class DialogService : IDialogService
{
    private const double FallbackDialogWidth = 520;
    private const double FallbackDialogHeight = 360;

    /// <inheritdoc />
    public bool Confirm(ConfirmDialogOptions options)
    {
        var dialog = new ConfirmDialog(options);
        var owner = Application.Current?.MainWindow;

        if (owner is { IsVisible: true })
        {
            dialog.Owner = owner;
            CoverOwnerContent(dialog, owner);
        }
        else
        {
            // No visible main window (e.g. during start-up): show a plain centred dialog.
            dialog.Width = FallbackDialogWidth;
            dialog.Height = FallbackDialogHeight;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true;
    }

    // Places the transparent dialog exactly over the owner's client area so its backdrop dims the whole
    // window, including when the owner is maximised or on a scaled (high-DPI) display.
    private static void CoverOwnerContent(Window dialog, Window owner)
    {
        var content = owner.Content as FrameworkElement ?? owner;
        var source = PresentationSource.FromVisual(content);

        if (source?.CompositionTarget is null)
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Width = FallbackDialogWidth;
            dialog.Height = FallbackDialogHeight;
            return;
        }

        // PointToScreen returns device pixels; convert back to WPF units.
        var topLeft = source.CompositionTarget.TransformFromDevice.Transform(content.PointToScreen(new Point(0, 0)));

        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Left = topLeft.X;
        dialog.Top = topLeft.Y;
        dialog.Width = content.ActualWidth;
        dialog.Height = content.ActualHeight;
    }
}
