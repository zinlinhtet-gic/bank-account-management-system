using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bams.Desktop.Components.NavBar;

public partial class NavBarViewModel : ObservableObject
{
    [ObservableProperty] private bool _isCollapsed;
    [ObservableProperty] private string _activeItem = "User Management";

    public string UserName { get; set; } = "Kaung Myat Htun";
    public string UserRole { get; set; } = "Branch Manager";
    public string UserInitials { get; set; } = "KMH";

    public ObservableCollection<NavItem> Items { get; } = new();

    // Like onNavigate prop
    public ICommand? NavigateCommand { get; set; }

    public NavBarViewModel()
    {
        Items.Add(new() { Label = "User Management", IconKey = "Icon.Accounts", IsActive = true });
        Items.Add(new() { Label = "Customer Management", IconKey = "Icon.Accounts" });
        Items.Add(new() { Label = "Accounting", IconKey = "Icon.Accounts" });
        Items.Add(new() { Label = "Operations", IconKey = "Icon.Transactions" });
        Items.Add(new() { Label = "Transactions", IconKey = "Icon.Transactions" });
        Items.Add(new() { Label = "Transaction History", IconKey = "Icon.Reports" });
        Items.Add(new() { Label = "Audit", IconKey = "Icon.Settings" });
        Items.Add(new() { Label = "Configurations", IconKey = "Icon.Settings" });

        foreach (var item in Items)
            item.Command = new RelayCommand(() => Select(item));
    }

    [RelayCommand]
    private void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    private void Select(NavItem item)
    {
        foreach (var navItem in Items)
            navItem.IsActive = navItem == item;

        ActiveItem = item.Label;
        NavigateCommand?.Execute(item.Label);
    }
}