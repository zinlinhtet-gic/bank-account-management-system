using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace bams.desktop.Components.AccountPicker;

/// <summary>
/// Live account search: <c>&lt;ap:AccountPicker ItemsSource="{Binding Accounts}" SelectedItem="{Binding Account}" DisplayMemberPath="DisplayName"/&gt;</c>.
/// </summary>
/// <remarks>
/// Filters the items by the typed text (case-insensitive, anywhere in the display text). Code-behind only handles
/// the text/list interaction, a view concern.
/// </remarks>
public partial class AccountPicker : UserControl
{
    private const int MaxResults = 50;

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(AccountPicker),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(AccountPicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(
        nameof(SelectedValue), typeof(object), typeof(AccountPicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValueChanged));

    public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(
        nameof(DisplayMemberPath), typeof(string), typeof(AccountPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedValuePathProperty = DependencyProperty.Register(
        nameof(SelectedValuePath), typeof(string), typeof(AccountPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(AccountPicker), new PropertyMetadata("Search by account number"));

    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register(
        nameof(HasError), typeof(bool), typeof(AccountPicker), new PropertyMetadata(false));

    private bool _updatingText;

    // Set while typing drops the chosen account, so the typed text is kept instead of being cleared.
    private bool _clearingByTyping;

    private Window? _hostWindow;

    public AccountPicker()
    {
        InitializeComponent();

        Input.TextChanged += OnTextChanged;
        Input.GotKeyboardFocus += (_, _) => ShowResults();
        Input.PreviewMouseLeftButtonDown += (_, _) => ShowResults();
        Input.PreviewKeyDown += OnInputKeyDown;
        Input.LostKeyboardFocus += OnInputLostFocus;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // The popup stays open on its own (a self-closing popup grabs the mouse and is closed again by the TextBox's
    // own click), so it is closed here when the user clicks elsewhere in the window or moves/leaves the window.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hostWindow = Window.GetWindow(this);

        if (_hostWindow is not null)
        {
            _hostWindow.PreviewMouseDown += OnWindowMouseDown;
            _hostWindow.LocationChanged += OnWindowMovedOrDeactivated;
            _hostWindow.Deactivated += OnWindowMovedOrDeactivated;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hostWindow is not null)
        {
            _hostWindow.PreviewMouseDown -= OnWindowMouseDown;
            _hostWindow.LocationChanged -= OnWindowMovedOrDeactivated;
            _hostWindow.Deactivated -= OnWindowMovedOrDeactivated;
            _hostWindow = null;
        }

        ResultsPopup.IsOpen = false;
    }

    // Clicks inside the popup never reach the window (the popup is its own window), so any click not on the box closes it.
    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!Input.IsMouseOver)
        {
            ResultsPopup.IsOpen = false;
        }
    }

    private void OnWindowMovedOrDeactivated(object? sender, EventArgs e)
    {
        ResultsPopup.IsOpen = false;
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Optional: the <see cref="SelectedValuePath"/> member of the selected item (two-way).</summary>
    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public string? DisplayMemberPath
    {
        get => (string?)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public string? SelectedValuePath
    {
        get => (string?)GetValue(SelectedValuePathProperty);
        set => SetValue(SelectedValuePathProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (AccountPicker)d;

        if (e.OldValue is INotifyCollectionChanged oldList)
        {
            oldList.CollectionChanged -= picker.OnItemsChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newList)
        {
            newList.CollectionChanged += picker.OnItemsChanged;
        }

        picker.SyncFromValue();
        picker.RefreshResults();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncFromValue();
        RefreshResults();
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (AccountPicker)d;

        picker.SetValueFromItem(e.NewValue);

        // Keep what the user is typing; only an outside change (choice, form reset) rewrites the text.
        if (!picker._clearingByTyping)
        {
            picker.SetText(e.NewValue is null ? string.Empty : picker.Describe(e.NewValue));
        }
    }

    private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AccountPicker)d).SyncFromValue();
    }

    // SelectedValue -> SelectedItem, once the items are there.
    private void SyncFromValue()
    {
        if (string.IsNullOrEmpty(SelectedValuePath) || ItemsSource is null)
        {
            return;
        }

        var value = SelectedValue;
        var match = value is null
            ? null
            : ItemsSource.Cast<object>().FirstOrDefault(item => Equals(ReadMember(item, SelectedValuePath), value));

        if (!Equals(match, SelectedItem))
        {
            SelectedItem = match;
        }
    }

    private void SetValueFromItem(object? item)
    {
        if (string.IsNullOrEmpty(SelectedValuePath))
        {
            return;
        }

        var value = item is null ? null : ReadMember(item, SelectedValuePath);

        if (!Equals(value, SelectedValue))
        {
            SelectedValue = value;
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingText)
        {
            return;
        }

        // Typing over a chosen account drops the choice, so the box never shows text that is not what will be posted.
        if (SelectedItem is not null && !string.Equals(Input.Text, Describe(SelectedItem), StringComparison.Ordinal))
        {
            _clearingByTyping = true;
            SelectedItem = null;
            _clearingByTyping = false;
        }

        RefreshResults();
        ShowResults();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                ShowResults();
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter when ResultsPopup.IsOpen && ResultsList.Items.Count > 0:
                Choose(ResultsList.SelectedItem ?? ResultsList.Items[0]);
                e.Handled = true;
                break;
            case Key.Escape:
                ResultsPopup.IsOpen = false;
                break;
        }
    }

    private void MoveSelection(int step)
    {
        if (ResultsList.Items.Count == 0)
        {
            return;
        }

        var next = Math.Clamp(ResultsList.SelectedIndex + step, 0, ResultsList.Items.Count - 1);

        ResultsList.SelectedIndex = next;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void OnResultClicked(object sender, MouseButtonEventArgs e)
    {
        var container = (e.OriginalSource as DependencyObject)?.FindAncestor<ListBoxItem>();

        // Choose on press and swallow it, so focus never leaves the box for the list.
        if (container is not null)
        {
            Choose(container.DataContext);
            e.Handled = true;
        }
    }

    // The list is not focusable, so losing focus always means leaving the control.
    private void OnInputLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ResultsPopup.IsOpen = false;
    }

    private void Choose(object item)
    {
        SelectedItem = item;
        ResultsPopup.IsOpen = false;
        Input.CaretIndex = Input.Text.Length;
    }

    private void ShowResults()
    {
        if (!Input.IsKeyboardFocused && !Input.IsMouseOver)
        {
            return;
        }

        RefreshResults();
        ResultsPopup.IsOpen = true;
    }

    private void RefreshResults()
    {
        if (ResultsList is null)
        {
            return;
        }

        var query = Input.Text.Trim();
        var source = ItemsSource?.Cast<object>() ?? [];
        var matches = (SelectedItem is not null && string.Equals(Input.Text, Describe(SelectedItem), StringComparison.Ordinal)
                ? source
                : source.Where(item => Describe(item).Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Take(MaxResults)
            .ToList();

        ResultsList.ItemsSource = matches;
        ResultsList.DisplayMemberPath = DisplayMemberPath;
        EmptyText.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetText(string text)
    {
        _updatingText = true;
        Input.Text = text;
        Input.CaretIndex = text.Length;
        _updatingText = false;
    }

    private string Describe(object item)
    {
        var text = string.IsNullOrEmpty(DisplayMemberPath) ? item : ReadMember(item, DisplayMemberPath);

        return text?.ToString() ?? string.Empty;
    }

    private static object? ReadMember(object item, string path)
    {
        return item.GetType().GetProperty(path)?.GetValue(item);
    }
}

internal static class VisualTreeExtensions
{
    public static T? FindAncestor<T>(this DependencyObject? start) where T : DependencyObject
    {
        while (start is not null and not T)
        {
            start = System.Windows.Media.VisualTreeHelper.GetParent(start);
        }

        return start as T;
    }
}
