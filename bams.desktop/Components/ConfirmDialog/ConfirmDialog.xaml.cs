using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using bams.desktop.Services;

namespace bams.desktop.Components.ConfirmDialog;

/// <summary>
/// Themed confirmation dialog. Do not open it directly; call <see cref="IDialogService.Confirm"/>.
/// </summary>
/// <remarks>
/// Code-behind only applies the options and runs the entrance animation, which are view concerns.
/// </remarks>
public partial class ConfirmDialog : Window
{
    private static readonly Duration EntranceDuration = new(TimeSpan.FromMilliseconds(140));

    public ConfirmDialog(ConfirmDialogOptions options)
    {
        InitializeComponent();
        ApplyOptions(options);
        Loaded += (_, _) => PlayEntranceAnimation();
    }

    // Fills in the texts and picks the destructive (red) or normal (blue) look.
    private void ApplyOptions(ConfirmDialogOptions options)
    {
        TitleText.Text = options.Title;
        MessageText.Text = options.Message;
        ConfirmButton.Content = options.ConfirmText;
        CancelButton.Content = options.CancelText;
        Title = options.Title;

        var defaultIconKey = options.IsDestructive ? "Icon.AlertCircle" : "Icon.Check";
        IconGlyph.Geometry = options.Icon
            ?? (options.IconKey is null ? null : TryFindResource(options.IconKey) as Geometry)
            ?? (Geometry)FindResource(defaultIconKey);

        if (options.IsDestructive)
        {
            IconCircle.Fill = (Brush)FindResource("DangerSoftBrush");
            IconGlyph.Foreground = (Brush)FindResource("DangerBrush");
            ConfirmButton.Style = (Style)FindResource("Button.DangerSolid");
        }
    }

    // Fades the backdrop in and lifts the card 8px into place.
    private void PlayEntranceAnimation()
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        Backdrop.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, EntranceDuration) { EasingFunction = easing });
        CardOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, EntranceDuration) { EasingFunction = easing });
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    // A click on the dimmed area (not on the card) cancels, like pressing Esc.
    private void OnBackdropMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Backdrop))
        {
            DialogResult = false;
        }
    }
}
