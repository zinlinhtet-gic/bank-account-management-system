using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using bams.desktop.Services;
using bams.desktop.ViewModels;

namespace bams.desktop.Components.ModalDialog;

/// <summary>
/// Themed modal that hosts any <see cref="IDialogViewModel"/>. Do not open it directly; call
/// <see cref="IDialogService.ShowDialog"/>.
/// </summary>
/// <remarks>
/// Code-behind only wires closing (ViewModel request, Esc, backdrop click) and the entrance animation, which are view concerns.
/// </remarks>
public partial class ModalDialog : Window
{
    private static readonly Duration EntranceDuration = new(TimeSpan.FromMilliseconds(140));

    private readonly IDialogViewModel _viewModel;

    public ModalDialog(IDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        Body.Content = viewModel;
        _viewModel.CloseRequested += OnCloseRequested;

        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => PlayEntranceAnimation();
        Closed += (_, _) => _viewModel.CloseRequested -= OnCloseRequested;
    }

    // The ViewModel finished (saved) or cancelled.
    private void OnCloseRequested(bool result)
    {
        DialogResult = result;
    }

    // Esc cancels, like the confirmation dialog, unless the ViewModel is in the middle of saving.
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.CanCancel)
        {
            e.Handled = true;
            DialogResult = false;
        }
    }

    // A click on the dimmed area (not on the card) cancels when the ViewModel allows it.
    private void OnBackdropMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Backdrop) && _viewModel.CanCloseOnBackdropClick && _viewModel.CanCancel)
        {
            DialogResult = false;
        }
    }

    // Fades the backdrop in and lifts the card 8px into place.
    private void PlayEntranceAnimation()
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        Backdrop.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, EntranceDuration) { EasingFunction = easing });
        CardOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, EntranceDuration) { EasingFunction = easing });
    }
}
