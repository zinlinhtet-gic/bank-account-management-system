using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace bams.desktop.Themes;

/// <summary>Polls only while a view is loaded and visible; unload stops the timer and releases the view.</summary>
public static class RefreshAssist
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command", typeof(ICommand), typeof(RefreshAssist), new PropertyMetadata(null, CommandChanged));
    private static readonly DependencyProperty TimerProperty = DependencyProperty.RegisterAttached(
        "Timer", typeof(DispatcherTimer), typeof(RefreshAssist));
    private const int RefreshIntervalSeconds = 10;
    public static ICommand? GetCommand(DependencyObject element) => (ICommand?)element.GetValue(CommandProperty);
    public static void SetCommand(DependencyObject element, ICommand? value) => element.SetValue(CommandProperty, value);

    private static void CommandChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement view) return;
        view.Loaded -= Start; view.Unloaded -= Stop;
        if (view.GetValue(TimerProperty) is DispatcherTimer prior) prior.Stop();
        view.ClearValue(TimerProperty);
        if (args.NewValue is null) return;
        view.Loaded += Start; view.Unloaded += Stop;
        if (view.IsLoaded) Start(view, new RoutedEventArgs());
    }

    private static void Start(object sender, RoutedEventArgs args)
    {
        FrameworkElement view = (FrameworkElement)sender;
        if (view.GetValue(TimerProperty) is DispatcherTimer prior) prior.Stop();
        DispatcherTimer timer = new(DispatcherPriority.Background, view.Dispatcher) { Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds) };
        timer.Tick += (_, _) =>
        {
            ICommand? command = GetCommand(view);
            if (view.IsVisible && command?.CanExecute(null) == true) command.Execute(null);
        };
        view.SetValue(TimerProperty, timer);
        timer.Start();
    }

    private static void Stop(object sender, RoutedEventArgs args)
    {
        FrameworkElement view = (FrameworkElement)sender;
        if (view.GetValue(TimerProperty) is DispatcherTimer timer) timer.Stop();
        view.ClearValue(TimerProperty);
    }
}
