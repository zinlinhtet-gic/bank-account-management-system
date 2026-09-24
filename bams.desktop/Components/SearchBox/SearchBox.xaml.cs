using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using bams.desktop.Themes;

namespace bams.desktop.Components.SearchBox;

/// <summary>
/// Themed search field: <c>&lt;search:SearchBox Text="{Binding SearchText}" SearchCommand="{Binding SearchCommand}" Placeholder="Search"/&gt;</c>.
/// </summary>
/// <remarks>
/// Code-behind only switches between the idle look and the active look (icon shown, text indented), a view concern.
/// </remarks>
public partial class SearchBox : UserControl
{
    // Room for the 28px icon button plus its 4px left margin and a small gap before the text.
    private static readonly Thickness ActiveTextPadding = new(36, 0, 10, 0);
    private static readonly Thickness IdleTextPadding = new(10, 0, 10, 0);

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SearchBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLookChanged));

    public static readonly DependencyProperty SearchCommandProperty = DependencyProperty.Register(
        nameof(SearchCommand), typeof(ICommand), typeof(SearchBox));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(SearchBox),
        new FrameworkPropertyMetadata("Search", OnLookChanged));

    public SearchBox()
    {
        InitializeComponent();

        Input.IsKeyboardFocusedChanged += (_, _) => UpdateLook();
        UpdateLook();
    }

    /// <summary>The typed search text (two-way).</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Runs on Enter and on a click on the search icon.</summary>
    public ICommand? SearchCommand
    {
        get => (ICommand?)GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    /// <summary>Hint shown while the box is idle and empty.</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    private static void OnLookChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SearchBox)d).UpdateLook();
    }

    // Active while the user is typing or a search term is kept; the placeholder would sit under the icon, so hide it then.
    private void UpdateLook()
    {
        // Property callbacks can fire while InitializeComponent is still building the parts.
        if (Input is null || SearchButton is null)
        {
            return;
        }

        var isActive = Input.IsKeyboardFocused || !string.IsNullOrEmpty(Text);

        SearchButton.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        Input.Padding = isActive ? ActiveTextPadding : IdleTextPadding;
        ThemeAssist.SetPlaceholder(Input, isActive ? string.Empty : Placeholder);
    }
}
