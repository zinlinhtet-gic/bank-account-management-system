using System.Collections.ObjectModel;
using System.Windows.Input;
using bams.desktop.Commands;
using bams.desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelayCommand = bams.desktop.Commands.RelayCommand;

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
        // Initialize with default items (will be replaced by permission-based items)
        BuildDefaultNavigationItems();
    }

    /// <summary>
    /// Builds navigation items based on user permissions.
    /// </summary>
    public void BuildNavigationItems(PermissionFlags flags)
    {
        Items.Clear();

        // Add items based on permissions
        if (flags.CanManageUsers)
            AddNavItem("User Management", "Icon.Users", true);

        if (flags.CanManageCustomers)
            AddNavItem("Customer Management", "Icon.Accounts", Items.Count == 0);

        if (flags.CanPerformKYC)
            AddNavItem("Customer KYC", "Icon.UserCheck");

        if (flags.CanManageAccounts)
            AddNavItem("Account Management", "Icon.Accounts", Items.Count == 0);

        if (flags.CanViewTransactions)
            AddNavItem("Transactions", "Icon.Transactions", Items.Count == 0);

        if (flags.CanViewTransactionHistory)
            AddNavItem("Transaction History", "Icon.Reports");

        if (flags.CanAccessAccounting)
            AddNavItem("Accounting", "Icon.Finance");

        if (flags.CanPerformOperations)
            AddNavItem("Operations", "Icon.Settings");

        if (flags.CanViewAudit)
            AddNavItem("Audit", "Icon.Shield");

        if (flags.CanConfigureSystem)
            AddNavItem("Configurations", "Icon.Settings");

        if (flags.CanViewCustomerList)
            AddNavItem("Customer List", "Icon.Users");

        // Set first item as active if none is active
        if (Items.Count > 0 && !Items.Any(i => i.IsActive))
        {
            Items[0].IsActive = true;
            ActiveItem = Items[0].Label;
        }
    }

    /// <summary>
    /// Adds a navigation item to the collection.
    /// </summary>
    private void AddNavItem(string label, string iconKey, bool isActive = false)
    {
        var item = new NavItem 
        { 
            Label = label, 
            IconKey = iconKey, 
            IsActive = isActive 
        };
        item.Command = new RelayCommand(_ => Select(item));
        Items.Add(item);
    }

    /// <summary>
    /// Builds default navigation items (used when not authenticated).
    /// </summary>
    private void BuildDefaultNavigationItems()
    {
        Items.Clear();
        Items.Add(new() { Label = "User Management", IconKey = "Icon.Users", IsActive = true });
        Items.Add(new() { Label = "Customer Management", IconKey = "Icon.Accounts" });
        Items.Add(new() { Label = "Account Management", IconKey = "Icon.Accounts" });
        Items.Add(new() { Label = "Transactions", IconKey = "Icon.Transactions" });
        Items.Add(new() { Label = "Transaction History", IconKey = "Icon.Reports" });
        Items.Add(new() { Label = "Accounting", IconKey = "Icon.Finance" });
        Items.Add(new() { Label = "Operations", IconKey = "Icon.Settings" });
        Items.Add(new() { Label = "Audit", IconKey = "Icon.Shield" });
        Items.Add(new() { Label = "Configurations", IconKey = "Icon.Settings" });

        foreach (var item in Items)
            item.Command = new RelayCommand(_ => Select(item));
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