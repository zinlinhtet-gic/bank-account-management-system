using bams.desktop.Constants;
using System.Collections.ObjectModel;
using System.Windows.Input;
using bams.desktop.Commands;
using bams.desktop.Services;
using bams.desktop.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelayCommand = bams.desktop.Commands.RelayCommand;

namespace Bams.Desktop.Components.NavBar;

public partial class NavBarViewModel : ObservableObject
{
    [ObservableProperty] private bool _isCollapsed;
    [ObservableProperty] private string _activeItem = PageNames.UserManagement;

    public string UserName { get; set; } = "Kaung Myat Htun";
    public string UserRole { get; set; } = "Branch Manager";
    public string UserInitials { get; set; } = "KMH";

    public ObservableCollection<NavItem> Items { get; } = new();

    // Like onNavigate prop
    public ICommand? NavigateCommand { get; set; }

    public NavBarViewModel()
    {
        AppLog.WriteInformation($"{nameof(NavBarViewModel)}: ViewModel created.");
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
            AddNavItem(PageNames.UserManagement, "Icon.Users", true);

        if (flags.CanManageCustomers)
            AddNavItem(PageNames.CustomerManagement, "Icon.Accounts", Items.Count == 0);

        if (flags.CanPerformKYC)
            AddNavItem(PageNames.CustomerKyc, "Icon.UserCheck");

        if (flags.CanManageAccounts)
            AddNavItem(PageNames.AccountManagement, "Icon.Accounts", Items.Count == 0);

        if (flags.CanViewTransactions)
            AddNavItem(PageNames.Transactions, "Icon.Transactions", Items.Count == 0);

        // Officers post on the Transactions page and review and finish transfers here; auditors only review.
        if (flags.CanViewTransactionHistory || flags.CanViewTransactions)
            AddNavItem(PageNames.TransactionHistory, "Icon.Reports");

        if (flags.CanAccessAccounting)
        {
            AddAccountingNavigation(flags);
        }

        if (flags.CanManageCashOperations && !flags.CanAccessAccounting)
            AddNavItem(PageNames.CashReconciliation, "Icon.Finance", Items.Count == 0);

        if (flags.CanPerformOperations)
            AddNavItem(PageNames.Operations, "Icon.Settings");

        if (flags.CanViewAudit)
        {
            AddAuditNavigation();
        }

        if (flags.CanConfigureSystem)
            AddNavGroup(PageNames.Configurations, "Icon.Settings", PageNames.InterestRate, PageNames.FeeRate, PageNames.BankPolicies, PageNames.OtherBanks);

        if (flags.CanViewCustomerList)
            AddNavItem(PageNames.CustomerList, "Icon.Users");

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


    /// Adds a toggleable group
    private void AddNavGroup(string label, string iconKey, params string[] childLabels)
    {
        var group = new NavItem
        {
            Label = label,
            IconKey = iconKey,
            IsExpanded = false
        };

        foreach (var childLabel in childLabels)
        {
            var child = new NavItem { Label = childLabel };
            child.Command = new RelayCommand(_ => Select(child));
            group.Children.Add(child);
        }

        group.Command = new RelayCommand(_ => group.IsExpanded = !group.IsExpanded);
        Items.Add(group);
    }

    /// <summary>
    /// Builds default navigation items (used when not authenticated).
    /// </summary>
    private void BuildDefaultNavigationItems()
    {
        Items.Clear();
        Items.Add(new() { Label = PageNames.UserManagement, IconKey = "Icon.Users", IsActive = true });
        Items.Add(new() { Label = PageNames.CustomerManagement, IconKey = "Icon.Accounts" });
        Items.Add(new() { Label = PageNames.AccountManagement, IconKey = "Icon.Accounts" });
        Items.Add(new() { Label = PageNames.Transactions, IconKey = "Icon.Transactions" });
        Items.Add(new() { Label = PageNames.TransactionHistory, IconKey = "Icon.Reports" });
        Items.Add(new() { Label = PageNames.Accounting, IconKey = "Icon.Finance" });
        Items.Add(new() { Label = PageNames.Operations, IconKey = "Icon.Settings" });
        Items.Add(new() { Label = PageNames.Audit, IconKey = "Icon.Shield" });

        foreach (var item in Items)
            item.Command = new RelayCommand(_ => Select(item));

        AddNavGroup(PageNames.Configurations, "Icon.Settings", PageNames.InterestRate, PageNames.FeeRate, PageNames.BankPolicies, PageNames.OtherBanks);
    }

    [RelayCommand]
    private void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    // Asks the shell to open the item's page. The highlight is not changed here: the shell sets ActiveItem only
    // when navigation succeeds, and OnActiveItemChanged then moves the highlight.
    private void Select(NavItem item)
    {
        foreach (var navItem in Items)
        {
            navItem.IsActive = navItem == item;

            if (navItem.Children is null)
                continue;

            foreach (var child in navItem.Children)
                child.IsActive = child == item;
        }

        ActiveItem = item.Label;
        NavigateCommand?.Execute(item.Label);
    }

    // Keeps the highlighted item (and the expanded parent group of a child page) in step with the page shown.
    partial void OnActiveItemChanged(string value)
    {
        foreach (var item in Items)
        {
            item.IsActive = item.Label == value;

            foreach (var child in item.Children)
            {
                child.IsActive = child.Label == value;

                if (child.IsActive)
                {
                    item.IsActive = true;
                    item.IsExpanded = true;
                }
            }
        }
    }
    /// <summary>
    /// Adds the Accounting navigation group and its child pages.
    /// </summary>
    private void AddAccountingNavigation(PermissionFlags flags)
    {
        var accounting = new NavItem
        {
            Label = PageNames.Accounting,
            IconKey = "Icon.Finance"
        };

        accounting.Children.Add(CreateChildNavItem(PageNames.GeneralLedger));

        accounting.Children.Add(CreateChildNavItem(PageNames.AccountingEntries));

        accounting.Children.Add(CreateChildNavItem(PageNames.Reconciliation));
        if (flags.CanManageCashOperations)
            accounting.Children.Add(CreateChildNavItem(PageNames.CashReconciliation));
        accounting.Children.Add(CreateChildNavItem(PageNames.EndOfDay));

        accounting.Command =new RelayCommand(_ => ToggleGroup(accounting));

        Items.Add(accounting);
    }

    /// <summary>
    /// Adds the Audit navigation group and its child pages.
    /// </summary>
    private void AddAuditNavigation()
    {
        var audit = new NavItem
        {
            Label = PageNames.Audit,
            IconKey = "Icon.Shield"
        };

        audit.Children.Add(CreateChildNavItem(PageNames.TransactionAudit));

        audit.Command =new RelayCommand(_ => ToggleGroup(audit));

        Items.Add(audit);
    }
    /// <summary>
    /// Creates a navigation item that opens an actual page.
    /// </summary>
    private NavItem CreateChildNavItem(string label)
    {
        var item = new NavItem
        {
            Label = label
        };

        item.Command =
            new RelayCommand(_ => SelectPage(item));

        return item;
    }
    /// <summary>
    /// Expands or collapses a parent navigation group.
    /// </summary>
    private static void ToggleGroup(NavItem item)
    {
        item.IsExpanded = !item.IsExpanded;
    }
    /// <summary>
    /// Sends a child page's label to the main navigation service; the highlight follows only when the page opens
    /// (see <see cref="OnActiveItemChanged"/>).
    /// </summary>
    private void SelectPage(NavItem selectedItem)
    {
        NavigateCommand?.Execute(selectedItem.Label);
    }
}
