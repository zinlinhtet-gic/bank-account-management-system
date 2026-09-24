using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using bams.desktop.Themes;

namespace bams.desktop.Components.PasswordInput;

/// <summary>
/// Password field with a show/hide toggle, styled with the shared theme.
/// <code>
/// xmlns:pwd="clr-namespace:bams.desktop.Components.PasswordInput"
/// &lt;pwd:PasswordInput Password="{Binding Password}" HasError="{Binding HasError}"/&gt;
/// </code>
/// </summary>
/// <remarks>
/// WPF's PasswordBox does not allow binding its Password, so this control keeps the hidden and
/// visible boxes in sync and exposes a bindable <see cref="Password"/>. This is view-only behaviour,
/// which is why it lives in code-behind.
/// </remarks>
public partial class PasswordInput : UserControl
{
    private const string ShowPasswordToolTip = "Show password";
    private const string HidePasswordToolTip = "Hide password";

    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(
        nameof(Password), typeof(string), typeof(PasswordInput),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChanged));

    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register(
        nameof(HasError), typeof(bool), typeof(PasswordInput), new PropertyMetadata(false));

    // Prevents the two boxes and the Password property from updating each other in a loop.
    private bool _isSynchronizing;
    private bool _isPasswordVisible;

    public PasswordInput()
    {
        InitializeComponent();
    }

    /// <summary>The entered password (two-way bindable).</summary>
    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    /// <summary>Draws the field with a red border.</summary>
    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    // Pushes a Password value coming from the ViewModel into both boxes.
    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var input = (PasswordInput)d;
        var password = (string?)e.NewValue ?? string.Empty;

        if (input._isSynchronizing)
        {
            return;
        }

        input._isSynchronizing = true;
        input.HiddenBox.Password = password;
        input.VisibleBox.Text = password;
        input._isSynchronizing = false;
    }

    // Typing in the hidden box updates the Password property.
    private void OnHiddenBoxPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizing)
        {
            return;
        }

        _isSynchronizing = true;
        Password = HiddenBox.Password;
        VisibleBox.Text = HiddenBox.Password;
        _isSynchronizing = false;
    }

    // Typing in the visible box (password shown) updates the Password property.
    private void OnVisibleBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSynchronizing)
        {
            return;
        }

        _isSynchronizing = true;
        Password = VisibleBox.Text;
        HiddenBox.Password = VisibleBox.Text;
        _isSynchronizing = false;
    }

    // Switches between the masked and plain-text box, keeps the caret at the end and swaps the eye icon.
    private void OnToggleVisibilityClick(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;

        HiddenBox.Visibility = _isPasswordVisible ? Visibility.Collapsed : Visibility.Visible;
        VisibleBox.Visibility = _isPasswordVisible ? Visibility.Visible : Visibility.Collapsed;

        ThemeAssist.SetIcon(ToggleButton, (Geometry)FindResource(_isPasswordVisible ? "Icon.EyeOff" : "Icon.Eye"));
        ToggleButton.ToolTip = _isPasswordVisible ? HidePasswordToolTip : ShowPasswordToolTip;

        if (_isPasswordVisible)
        {
            VisibleBox.Focus();
            VisibleBox.CaretIndex = VisibleBox.Text.Length;
        }
        else
        {
            HiddenBox.Focus();
        }
    }
}
