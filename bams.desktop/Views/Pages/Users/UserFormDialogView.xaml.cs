using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using bams.desktop.ViewModels.Pages.Users;

namespace bams.desktop.Views.Pages.Users;

/// <summary>
/// Create / edit user form. Code-behind only sets the header icon and the initial keyboard focus, which are view concerns.
/// </summary>
public partial class UserFormDialogView : UserControl
{
    public UserFormDialogView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    // Puts the caret in the first field so the manager can start typing right away.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserFormViewModel { IsEditMode: true })
        {
            HeaderIcon.Geometry = (Geometry)FindResource("Icon.Edit");
        }

        FirstNameBox.Focus();
        FirstNameBox.CaretIndex = FirstNameBox.Text.Length;
    }
}
