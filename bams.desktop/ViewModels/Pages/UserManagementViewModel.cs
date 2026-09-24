namespace bams.desktop.ViewModels.Pages;

/// <summary>
/// ViewModel for the User Management page.
/// Allows managers to manage system users and their roles.
/// </summary>
public sealed class UserManagementViewModel : ViewModelBase
{
    public string PageTitle => "User Management";
    public string PageDescription => "Manage system users, roles, and permissions";
}