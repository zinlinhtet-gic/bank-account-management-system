using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace bams.desktop.Components.ToastMessage;

public partial class ToastMessage : UserControl
{
    private readonly DispatcherTimer _dismissTimer;

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(ToastMessage), new PropertyMetadata(string.Empty, OnMessageChanged));

    public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register(
        nameof(IsError), typeof(bool), typeof(ToastMessage), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey IsOpenPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsOpen), typeof(bool), typeof(ToastMessage), new PropertyMetadata(false));

    public static readonly DependencyProperty IsOpenProperty = IsOpenPropertyKey.DependencyProperty;

    public ToastMessage()
    {
        InitializeComponent();
        _dismissTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(6)
        };
        _dismissTimer.Tick += DismissTimer_Tick;
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IsError
    {
        get => (bool)GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    public bool IsOpen => (bool)GetValue(IsOpenProperty);

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToastMessage toast)
        {
            return;
        }

        var hasMessage = !string.IsNullOrWhiteSpace(e.NewValue as string);
        toast.SetValue(IsOpenPropertyKey, hasMessage);
        toast._dismissTimer.Stop();
        if (hasMessage && toast.IsLoaded)
        {
            toast._dismissTimer.Start();
        }
    }

    private void ToastMessage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Message))
        {
            SetValue(IsOpenPropertyKey, true);
            _dismissTimer.Start();
        }
    }

    private void ToastMessage_Unloaded(object sender, RoutedEventArgs e) => _dismissTimer.Stop();

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        _dismissTimer.Stop();
        SetValue(IsOpenPropertyKey, false);
    }

    private void DismissTimer_Tick(object? sender, EventArgs e)
    {
        _dismissTimer.Stop();
        SetValue(IsOpenPropertyKey, false);
    }
}
