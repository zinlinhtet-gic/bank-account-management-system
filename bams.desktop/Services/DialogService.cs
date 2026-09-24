using System.Windows;
using System.Windows.Media.Effects;
using bams.desktop.Components.ConfirmDialog;
using bams.desktop.Components.ModalDialog;
using bams.desktop.ViewModels;

namespace bams.desktop.Services;

/// <summary>
/// Opens themed dialogs over the application's main window, which is dimmed and blurred while a dialog is open.
/// </summary>
public sealed class DialogService : IDialogService
{
    private const double FallbackDialogWidth = 520;
    private const double FallbackDialogHeight = 360;

    // Enough to make the page unreadable behind the dialog while keeping its layout recognisable.
    private const double BackdropBlurRadius = 6;

    /// <inheritdoc />
    public bool Confirm(ConfirmDialogOptions options)
    {
        return ShowOverMainWindow(new ConfirmDialog(options));
    }

    /// <inheritdoc />
    public bool ShowDialog(IDialogViewModel viewModel)
    {
        return ShowOverMainWindow(new ModalDialog(viewModel));
    }

    // Positions the dialog over the main window, blurs the window content while the dialog is open, and waits.
    private static bool ShowOverMainWindow(Window dialog)
    {
        var owner = Application.Current?.MainWindow;

        if (owner is not { IsVisible: true })
        {
            // No visible main window (e.g. during start-up): show a plain centred dialog.
            dialog.Width = FallbackDialogWidth;
            dialog.Height = FallbackDialogHeight;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return dialog.ShowDialog() == true;
        }

        dialog.Owner = owner;
        CoverOwnerContent(dialog, owner);

        // Blur only if nothing else already did (e.g. a confirmation opened from inside another dialog).
        var content = owner.Content as UIElement;
        var appliedBlur = content is { Effect: null };
        if (appliedBlur)
        {
            content!.Effect = new BlurEffect { Radius = BackdropBlurRadius };
        }

        try
        {
            return dialog.ShowDialog() == true;
        }
        finally
        {
            if (appliedBlur)
            {
                content!.Effect = null;
            }
        }
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
