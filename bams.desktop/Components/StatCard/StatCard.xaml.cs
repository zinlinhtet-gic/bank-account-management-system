using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace bams.desktop.Components.StatCard;

/// <summary>
/// Summary tile from the design (StatCard.tsx).
/// <code>
/// &lt;stat:StatCard Label="ACTIVE USERS" Value="{Binding ActiveUserCount}" SubValue="of all staff"
///                Icon="{StaticResource Icon.UserCheck}" AccentBrush="{StaticResource SuccessBrush}"/&gt;
/// </code>
/// Set <see cref="Trend"/> (percent, e.g. 3.2 or -5.1) to show a green ▲ / red ▼ change.
/// </summary>
public partial class StatCard : UserControl
{
    private const string TrendUpSymbol = "▲";
    private const string TrendDownSymbol = "▼";
    private const string TrendFlatSymbol = "—";

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubValueProperty = DependencyProperty.Register(
        nameof(SubValue), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty, OnFooterChanged));

    public static readonly DependencyProperty TrendProperty = DependencyProperty.Register(
        nameof(Trend), typeof(double?), typeof(StatCard), new PropertyMetadata(null, OnFooterChanged));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(StatCard), new PropertyMetadata(null, OnIconChanged));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(StatCard), new PropertyMetadata(null));

    public StatCard()
    {
        InitializeComponent();

        // Default accent is the primary colour, as in the design.
        if (AccentBrush is null && TryFindResource("PrimaryBrush") is Brush primary)
        {
            AccentBrush = primary;
        }

        UpdateIconVisibility();
        UpdateFooter();
    }

    /// <summary>Small caption at the top, written in capitals (e.g. "TOTAL USERS").</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The big number.</summary>
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Muted text under the value (e.g. "vs. last month").</summary>
    public string SubValue { get => (string)GetValue(SubValueProperty); set => SetValue(SubValueProperty, value); }

    /// <summary>Percentage change; positive is green ▲, negative is red ▼, null hides it.</summary>
    public double? Trend { get => (double?)GetValue(TrendProperty); set => SetValue(TrendProperty, value); }

    /// <summary>Icon geometry (an <c>Icon.*</c> resource). Hidden when null.</summary>
    public Geometry? Icon { get => (Geometry?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Colour of the icon and its tinted box.</summary>
    public Brush? AccentBrush { get => (Brush?)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((StatCard)d).UpdateIconVisibility();

    private static void OnFooterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((StatCard)d).UpdateFooter();

    // Hides the icon box when no icon is set.
    private void UpdateIconVisibility()
    {
        IconBox.Visibility = Icon is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // Shows the trend arrow with the design's colours and hides the footer when there is nothing to show.
    private void UpdateFooter()
    {
        if (Trend is double trend)
        {
            var symbol = trend > 0 ? TrendUpSymbol : trend < 0 ? TrendDownSymbol : TrendFlatSymbol;
            var brushKey = trend > 0 ? "SuccessBrush" : trend < 0 ? "DangerBrush" : "MutedForegroundBrush";

            TrendText.Text = $"{symbol}{Math.Abs(trend).ToString("0.#", CultureInfo.InvariantCulture)}%";
            TrendText.Foreground = TryFindResource(brushKey) as Brush ?? TrendText.Foreground;
            TrendText.Visibility = Visibility.Visible;
        }
        else
        {
            TrendText.Visibility = Visibility.Collapsed;
        }

        FooterPanel.Visibility = Trend is null && string.IsNullOrEmpty(SubValue)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
