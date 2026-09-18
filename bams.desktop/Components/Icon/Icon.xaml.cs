using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace bams.desktop.Components.Icon;

public partial class Icon : UserControl
{
    public static readonly DependencyProperty GeometryProperty =
        DependencyProperty.Register(nameof(Geometry), typeof(Geometry), typeof(Icon));

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(nameof(Size), typeof(double), typeof(Icon),
            new PropertyMetadata(16d));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(Icon),
            new PropertyMetadata(1.8d));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(Icon),
            new PropertyMetadata(Brushes.Black));

    public Geometry Geometry
    {
        get => (Geometry)GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Icon() => InitializeComponent();
}