using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace bams.desktop.Behaviors;

/// <summary>
/// Shows a TextBlock's full text as its tooltip, but only while the text is cut off with "…".
/// Usage (as the <c>Table.Text</c> styles do):
/// <c>&lt;Setter Property="ToolTip" Value="{Binding Text, RelativeSource={RelativeSource Self}}"/&gt;</c> and
/// <c>&lt;Setter Property="b:TrimmedTextToolTip.IsEnabled" Value="True"/&gt;</c>.
/// </summary>
public static class TrimmedTextToolTip
{
    // Sub-pixel rounding can make untrimmed text measure a hair wider than its box.
    private const double TrimTolerance = 0.5;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(TrimmedTextToolTip), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    // Subscribes when enabled and unsubscribes when disabled.
    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
        {
            return;
        }

        textBlock.ToolTipOpening -= OnToolTipOpening;

        if (e.NewValue is true)
        {
            textBlock.ToolTipOpening += OnToolTipOpening;
        }
    }

    // Cancels the tooltip when the whole text is already visible.
    private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (!IsTextTrimmed((TextBlock)sender))
        {
            e.Handled = true;
        }
    }

    // True when the text needs more width than the TextBlock was given.
    private static bool IsTextTrimmed(TextBlock textBlock)
    {
        if (string.IsNullOrEmpty(textBlock.Text))
        {
            return false;
        }

        var formattedText = new FormattedText(
            textBlock.Text,
            CultureInfo.CurrentCulture,
            textBlock.FlowDirection,
            new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
            textBlock.FontSize,
            textBlock.Foreground,
            VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);

        var textWidth = textBlock.ActualWidth - textBlock.Padding.Left - textBlock.Padding.Right;
        return formattedText.Width > textWidth + TrimTolerance;
    }
}
