using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using bams.desktop.Constants;

namespace bams.desktop.Behaviors;

/// <summary>
/// Sizes every DataGrid column to the data it shows, so nobody has to drag column edges to read a table.
/// <list type="bullet">
/// <item>Each column is measured (header + visible rows) whenever the rows change or the grid is shown.</item>
/// <item>Text columns stop growing at <see cref="TableConstants.MaxAutoTextColumnWidth"/>; longer text is trimmed
/// with "…" (see <see cref="TrimmedTextToolTip"/> for the hover text). Template columns keep their full width.</item>
/// <item>Space left over is shared by the columns declared <c>Width="*"</c> in XAML (by their star value), or
/// the last column when none are, so the table still fills its card. When the columns need more room than
/// the grid has, the grid scrolls horizontally instead of squeezing them.</item>
/// </list>
/// The <c>Table</c> style turns this on for every table. Per column:
/// <c>b:DataGridAutoFitColumns.IsFixedWidth="True"</c> keeps the XAML width, and
/// <c>b:DataGridAutoFitColumns.MaxAutoWidth="480"</c> changes the cap.
/// </summary>
public static class DataGridAutoFitColumns
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(DataGridAutoFitColumns), new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty IsFixedWidthProperty = DependencyProperty.RegisterAttached(
        "IsFixedWidth", typeof(bool), typeof(DataGridAutoFitColumns), new PropertyMetadata(false));

    public static readonly DependencyProperty MaxAutoWidthProperty = DependencyProperty.RegisterAttached(
        "MaxAutoWidth", typeof(double), typeof(DataGridAutoFitColumns), new PropertyMetadata(double.NaN));

    // Per-grid bookkeeping; private so XAML cannot set it.
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(FitState), typeof(DataGridAutoFitColumns), new PropertyMetadata(null));

    // One shared delegate so the same handler can be removed again.
    private static readonly ScrollChangedEventHandler ScrollChangedHandler = OnScrollChanged;

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsFixedWidth(DependencyObject element) => (bool)element.GetValue(IsFixedWidthProperty);
    public static void SetIsFixedWidth(DependencyObject element, bool value) => element.SetValue(IsFixedWidthProperty, value);

    public static double GetMaxAutoWidth(DependencyObject element) => (double)element.GetValue(MaxAutoWidthProperty);
    public static void SetMaxAutoWidth(DependencyObject element, double value) => element.SetValue(MaxAutoWidthProperty, value);

    /// <summary>What the behavior remembers about one grid between fits.</summary>
    private sealed class FitState
    {
        // Width written in XAML, captured before the first fit; star columns absorb the leftover space.
        public Dictionary<DataGridColumn, DataGridLength> DeclaredWidths { get; } = new();

        // Content width of each auto-fitted column from the last measure (already capped).
        public Dictionary<DataGridColumn, double> ContentWidths { get; } = new();

        // Kept so the same delegate can be removed again; ItemCollection does not expose its grid.
        public NotifyCollectionChangedEventHandler? ItemsChangedHandler { get; set; }

        public bool IsMeasurePending { get; set; }
        public bool IsFillPending { get; set; }
    }

    // Subscribes when enabled and unsubscribes when disabled.
    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid)
        {
            return;
        }

        dataGrid.Loaded -= OnLoaded;
        dataGrid.SizeChanged -= OnSizeChanged;
        dataGrid.IsVisibleChanged -= OnIsVisibleChanged;
        dataGrid.RemoveHandler(ScrollViewer.ScrollChangedEvent, ScrollChangedHandler);

        if (dataGrid.GetValue(StateProperty) is FitState oldState)
        {
            ((INotifyCollectionChanged)dataGrid.Items).CollectionChanged -= oldState.ItemsChangedHandler;
            dataGrid.ClearValue(StateProperty);
        }

        if (e.NewValue is true)
        {
            // Items also raises a Reset when ItemsSource is replaced, so this covers reloads and edits.
            var state = new FitState { ItemsChangedHandler = (_, _) => ScheduleMeasure(dataGrid) };

            dataGrid.SetValue(StateProperty, state);
            dataGrid.Loaded += OnLoaded;
            dataGrid.SizeChanged += OnSizeChanged;
            dataGrid.IsVisibleChanged += OnIsVisibleChanged;
            dataGrid.AddHandler(ScrollViewer.ScrollChangedEvent, ScrollChangedHandler);
            ((INotifyCollectionChanged)dataGrid.Items).CollectionChanged += state.ItemsChangedHandler;

            if (dataGrid.IsLoaded)
            {
                ScheduleMeasure(dataGrid);
            }
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => ScheduleMeasure((DataGrid)sender);

    // A grid inside a hidden tab measures nothing, so measure again once it is shown.
    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            ScheduleMeasure((DataGrid)sender);
        }
    }

    // Window resize: content widths are unchanged, only the leftover space to share differs.
    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
        {
            ScheduleFill((DataGrid)sender);
        }
    }

    // A vertical scroll bar appearing narrows the viewport; shrink the fill so no horizontal bar appears.
    // Only shrinking is handled: re-growing could let the two scroll bars switch each other on and off.
    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var dataGrid = (DataGrid)sender;

        if (e.ViewportWidthChange < 0 && e.OriginalSource == FindScrollViewer(dataGrid))
        {
            ScheduleFill(dataGrid);
        }
    }

    // Runs one measure after the current burst of changes (e.g. rows added one by one) has been laid out.
    private static void ScheduleMeasure(DataGrid dataGrid)
    {
        if (dataGrid.GetValue(StateProperty) is not FitState state || state.IsMeasurePending)
        {
            return;
        }

        state.IsMeasurePending = true;
        dataGrid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            state.IsMeasurePending = false;
            MeasureColumns(dataGrid, state);
        });
    }

    // Runs one fill after the current layout pass.
    private static void ScheduleFill(DataGrid dataGrid)
    {
        if (dataGrid.GetValue(StateProperty) is not FitState state || state.IsFillPending)
        {
            return;
        }

        state.IsFillPending = true;
        dataGrid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            state.IsFillPending = false;

            // Nothing measured yet (grid was hidden): measure, which fills afterwards.
            if (state.ContentWidths.Count == 0)
            {
                MeasureColumns(dataGrid, state);
            }
            else
            {
                FillAvailableWidth(dataGrid, state);
            }
        });
    }

    // Lets WPF size each column to its header and visible cells, then records those widths (capped).
    private static void MeasureColumns(DataGrid dataGrid, FitState state)
    {
        if (!dataGrid.IsLoaded || !dataGrid.IsVisible || dataGrid.ActualWidth <= 0)
        {
            return;
        }

        var columns = GetAutoFitColumns(dataGrid, state);

        // Auto measures the realized cells at their natural width; the widths are frozen right after,
        // so columns do not keep growing while the user scrolls.
        foreach (var column in columns)
        {
            column.Width = DataGridLength.Auto;
        }

        dataGrid.UpdateLayout();

        state.ContentWidths.Clear();
        foreach (var column in columns)
        {
            state.ContentWidths[column] = CapContentWidth(column, column.ActualWidth);
        }

        FillAvailableWidth(dataGrid, state);
    }

    // Visible columns the behavior sizes, in display order; also remembers their XAML width the first time.
    private static List<DataGridColumn> GetAutoFitColumns(DataGrid dataGrid, FitState state)
    {
        var columns = new List<DataGridColumn>();

        foreach (var column in dataGrid.Columns.OrderBy(c => c.DisplayIndex))
        {
            if (GetIsFixedWidth(column))
            {
                continue;
            }

            state.DeclaredWidths.TryAdd(column, column.Width);

            if (column.Visibility == Visibility.Visible)
            {
                columns.Add(column);
            }
        }

        return columns;
    }

    // Text columns stop at the cap (their text is trimmed with "…"); template columns keep their full width.
    private static double CapContentWidth(DataGridColumn column, double contentWidth)
    {
        var cap = GetMaxAutoWidth(column);

        if (double.IsNaN(cap))
        {
            cap = column is DataGridTextColumn ? TableConstants.MaxAutoTextColumnWidth : double.PositiveInfinity;
        }

        return Math.Max(column.MinWidth, Math.Min(contentWidth, cap));
    }

    // Sets each column to its content width plus its share of any space left over in the grid.
    private static void FillAvailableWidth(DataGrid dataGrid, FitState state)
    {
        var columns = dataGrid.Columns
            .Where(c => c.Visibility == Visibility.Visible && state.ContentWidths.ContainsKey(c))
            .OrderBy(c => c.DisplayIndex)
            .ToList();

        if (columns.Count == 0)
        {
            return;
        }

        var fixedWidth = dataGrid.Columns
            .Where(c => c.Visibility == Visibility.Visible && !state.ContentWidths.ContainsKey(c))
            .Sum(c => c.ActualWidth);
        var contentWidth = columns.Sum(c => state.ContentWidths[c]);

        // Whole pixels only: a fraction left over would show a horizontal scroll bar for nothing.
        var leftover = Math.Floor(GetViewportWidth(dataGrid) - fixedWidth - contentWidth);

        var fillColumns = columns.Where(c => state.DeclaredWidths[c].IsStar).ToList();
        if (fillColumns.Count == 0)
        {
            fillColumns.Add(columns[^1]);
        }

        var totalWeight = fillColumns.Sum(c => GetFillWeight(state, c));

        foreach (var column in columns)
        {
            var extra = leftover > 0 && fillColumns.Contains(column)
                ? Math.Floor(leftover * GetFillWeight(state, column) / totalWeight)
                : 0;

            column.Width = new DataGridLength(state.ContentWidths[column] + extra);
        }
    }

    // A "2*" column takes twice the leftover of a "*" one; a non-star fallback column counts as 1.
    private static double GetFillWeight(FitState state, DataGridColumn column)
    {
        var declared = state.DeclaredWidths[column];
        return declared.IsStar ? declared.Value : 1;
    }

    // Width the columns can use without scrolling (inside the borders and any vertical scroll bar).
    private static double GetViewportWidth(DataGrid dataGrid)
    {
        var scrollViewer = FindScrollViewer(dataGrid);

        if (scrollViewer is not null && scrollViewer.ViewportWidth > 0)
        {
            return scrollViewer.ViewportWidth;
        }

        return dataGrid.ActualWidth - dataGrid.BorderThickness.Left - dataGrid.BorderThickness.Right;
    }

    // The grid's own scroll viewer from its template (not one inside a cell).
    private static ScrollViewer? FindScrollViewer(DataGrid dataGrid)
    {
        if (dataGrid.Template?.FindName("DG_ScrollViewer", dataGrid) is ScrollViewer named)
        {
            return named;
        }

        return VisualTreeHelper.GetChildrenCount(dataGrid) > 0
            ? FindFirstDescendant<ScrollViewer>(VisualTreeHelper.GetChild(dataGrid, 0))
            : null;
    }

    private static T? FindFirstDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T match)
        {
            return match;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var found = FindFirstDescendant<T>(VisualTreeHelper.GetChild(parent, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
