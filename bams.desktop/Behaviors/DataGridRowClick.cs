using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace bams.desktop.Behaviors;

/// <summary>
/// Runs a command with the row's item when a DataGrid row is clicked (or Enter is pressed on the selected row).
/// Clicks on buttons inside the row are ignored, so row actions (Edit, Delete...) do not also open the row.
/// Usage: <c>xmlns:b="clr-namespace:bams.desktop.Behaviors"</c>, then
/// <c>b:DataGridRowClick.Command="{Binding ShowUserDetailsCommand}"</c> on the DataGrid.
/// </summary>
public static class DataGridRowClick
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command", typeof(ICommand), typeof(DataGridRowClick), new PropertyMetadata(null, OnCommandChanged));

    // One shared delegate so the same handler can be removed again.
    private static readonly MouseButtonEventHandler MouseUpHandler = OnMouseLeftButtonUp;

    public static ICommand? GetCommand(DependencyObject element) => (ICommand?)element.GetValue(CommandProperty);
    public static void SetCommand(DependencyObject element, ICommand? value) => element.SetValue(CommandProperty, value);

    // Subscribes when a command is set and unsubscribes when it is cleared.
    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid)
        {
            return;
        }

        dataGrid.RemoveHandler(UIElement.MouseLeftButtonUpEvent, MouseUpHandler);
        dataGrid.PreviewKeyDown -= OnPreviewKeyDown;

        if (e.NewValue is not null)
        {
            // handledEventsToo: DataGrid cells may mark the mouse-up as handled while updating the selection.
            dataGrid.AddHandler(UIElement.MouseLeftButtonUpEvent, MouseUpHandler, handledEventsToo: true);
            dataGrid.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    // Also sees clicks on the row's buttons (handled events); FindRow skips those.
    private static void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var row = FindRow(e.OriginalSource as DependencyObject);

        if (row is not null)
        {
            ExecuteCommand((DataGrid)sender, row.Item);
        }
    }

    // Enter opens the selected row, for keyboard users.
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var dataGrid = (DataGrid)sender;

        if (e.Key == Key.Enter && dataGrid.SelectedItem is not null)
        {
            e.Handled = true;
            ExecuteCommand(dataGrid, dataGrid.SelectedItem);
        }
    }

    private static void ExecuteCommand(DataGrid dataGrid, object item)
    {
        var command = GetCommand(dataGrid);

        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
        }
    }

    // Walks up from the clicked element to its row; returns null for headers, scrollbars and buttons.
    private static DataGridRow? FindRow(DependencyObject? element)
    {
        while (element is not null)
        {
            switch (element)
            {
                case ButtonBase:
                    return null;
                case DataGridRow row:
                    return row;
            }

            // Text runs are not visuals, so fall back to the logical parent for them.
            element = element is Visual or Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return null;
    }
}
