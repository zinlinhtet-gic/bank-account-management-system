using System.Windows;
using System.Windows.Media;

namespace bams.desktop.Themes;

/// <summary>
/// Size variants shared by buttons and other themed controls.
/// </summary>
public enum ControlSize
{
    Small,
    Medium,
    Large
}

/// <summary>
/// Attached properties used by the theme styles so one style can cover several variants.
/// Usage in XAML: <c>xmlns:t="clr-namespace:bams.desktop.Themes"</c>, then e.g.
/// <c>&lt;Button Style="{StaticResource Button.Primary}" t:ThemeAssist.Size="Small" t:ThemeAssist.Icon="{StaticResource Icon.Plus}"/&gt;</c>.
/// </summary>
public static class ThemeAssist
{
    // ----- Buttons -----

    /// <summary>Small (28px), Medium (36px, default) or Large (44px).</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.RegisterAttached(
        "Size", typeof(ControlSize), typeof(ThemeAssist), new FrameworkPropertyMetadata(ControlSize.Medium));

    /// <summary>Icon geometry shown before the button text (use an <c>Icon.*</c> resource).</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(Geometry), typeof(ThemeAssist), new FrameworkPropertyMetadata(null));

    /// <summary>Icon size in pixels; set automatically by <see cref="SizeProperty"/>.</summary>
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.RegisterAttached(
        "IconSize", typeof(double), typeof(ThemeAssist), new FrameworkPropertyMetadata(14d));

    /// <summary>Shows a spinner, hides the icon and blocks clicks. Bind it to the ViewModel's busy flag.</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.RegisterAttached(
        "IsLoading", typeof(bool), typeof(ThemeAssist), new FrameworkPropertyMetadata(false));

    /// <summary>Background while the mouse is over the control (set by each variant style).</summary>
    public static readonly DependencyProperty HoverBackgroundProperty = DependencyProperty.RegisterAttached(
        "HoverBackground", typeof(Brush), typeof(ThemeAssist), new FrameworkPropertyMetadata(null));

    /// <summary>Foreground while the mouse is over the control (set by each variant style).</summary>
    public static readonly DependencyProperty HoverForegroundProperty = DependencyProperty.RegisterAttached(
        "HoverForeground", typeof(Brush), typeof(ThemeAssist), new FrameworkPropertyMetadata(null));

    /// <summary>Border brush while the mouse is over the control (set by each variant style).</summary>
    public static readonly DependencyProperty HoverBorderBrushProperty = DependencyProperty.RegisterAttached(
        "HoverBorderBrush", typeof(Brush), typeof(ThemeAssist), new FrameworkPropertyMetadata(null));

    /// <summary>Background while the control is pressed (set by each variant style).</summary>
    public static readonly DependencyProperty PressedBackgroundProperty = DependencyProperty.RegisterAttached(
        "PressedBackground", typeof(Brush), typeof(ThemeAssist), new FrameworkPropertyMetadata(null));

    // ----- Inputs -----

    /// <summary>Grey hint text shown while a TextBox or ComboBox is empty.</summary>
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder", typeof(string), typeof(ThemeAssist), new FrameworkPropertyMetadata(string.Empty));

    /// <summary>Fixed text shown on the left inside a TextBox, e.g. "MMK".</summary>
    public static readonly DependencyProperty PrefixProperty = DependencyProperty.RegisterAttached(
        "Prefix", typeof(string), typeof(ThemeAssist), new FrameworkPropertyMetadata(string.Empty));

    /// <summary>Draws the input with a red border. Bind it to the ViewModel's validation state.</summary>
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached(
        "HasError", typeof(bool), typeof(ThemeAssist), new FrameworkPropertyMetadata(false));

    // ----- Badges -----

    /// <summary>Shows the small coloured dot before a badge's text (default true).</summary>
    public static readonly DependencyProperty ShowDotProperty = DependencyProperty.RegisterAttached(
        "ShowDot", typeof(bool), typeof(ThemeAssist), new FrameworkPropertyMetadata(true));

    public static ControlSize GetSize(DependencyObject element) => (ControlSize)element.GetValue(SizeProperty);
    public static void SetSize(DependencyObject element, ControlSize value) => element.SetValue(SizeProperty, value);

    public static Geometry? GetIcon(DependencyObject element) => (Geometry?)element.GetValue(IconProperty);
    public static void SetIcon(DependencyObject element, Geometry? value) => element.SetValue(IconProperty, value);

    public static double GetIconSize(DependencyObject element) => (double)element.GetValue(IconSizeProperty);
    public static void SetIconSize(DependencyObject element, double value) => element.SetValue(IconSizeProperty, value);

    public static bool GetIsLoading(DependencyObject element) => (bool)element.GetValue(IsLoadingProperty);
    public static void SetIsLoading(DependencyObject element, bool value) => element.SetValue(IsLoadingProperty, value);

    public static Brush? GetHoverBackground(DependencyObject element) => (Brush?)element.GetValue(HoverBackgroundProperty);
    public static void SetHoverBackground(DependencyObject element, Brush? value) => element.SetValue(HoverBackgroundProperty, value);

    public static Brush? GetHoverForeground(DependencyObject element) => (Brush?)element.GetValue(HoverForegroundProperty);
    public static void SetHoverForeground(DependencyObject element, Brush? value) => element.SetValue(HoverForegroundProperty, value);

    public static Brush? GetHoverBorderBrush(DependencyObject element) => (Brush?)element.GetValue(HoverBorderBrushProperty);
    public static void SetHoverBorderBrush(DependencyObject element, Brush? value) => element.SetValue(HoverBorderBrushProperty, value);

    public static Brush? GetPressedBackground(DependencyObject element) => (Brush?)element.GetValue(PressedBackgroundProperty);
    public static void SetPressedBackground(DependencyObject element, Brush? value) => element.SetValue(PressedBackgroundProperty, value);

    public static string GetPlaceholder(DependencyObject element) => (string)element.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject element, string value) => element.SetValue(PlaceholderProperty, value);

    public static string GetPrefix(DependencyObject element) => (string)element.GetValue(PrefixProperty);
    public static void SetPrefix(DependencyObject element, string value) => element.SetValue(PrefixProperty, value);

    public static bool GetHasError(DependencyObject element) => (bool)element.GetValue(HasErrorProperty);
    public static void SetHasError(DependencyObject element, bool value) => element.SetValue(HasErrorProperty, value);

    public static bool GetShowDot(DependencyObject element) => (bool)element.GetValue(ShowDotProperty);
    public static void SetShowDot(DependencyObject element, bool value) => element.SetValue(ShowDotProperty, value);
}
