using System.Windows;

namespace bams.desktop.Behaviors;

/// <summary>
/// Carries a DataContext to elements outside the visual tree, such as DataGrid columns, which cannot bind to the
/// page themselves. Declare <c>&lt;b:BindingProxy x:Key="PageProxy" Data="{Binding}"/&gt;</c> in the view's resources,
/// then bind e.g. <c>Visibility="{Binding Data.ShowsActions, Source={StaticResource PageProxy}, ...}"</c>.
/// </summary>
public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(object), typeof(BindingProxy), new PropertyMetadata(null));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    // Freezable requires this factory; the proxy is never cloned in practice.
    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
