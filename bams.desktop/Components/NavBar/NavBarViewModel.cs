using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bams.Desktop.Components.NavBar;

public partial class NavBarViewModel : ObservableObject
{
    [ObservableProperty] private bool _isCollapsed;
    [ObservableProperty] private string _activeItem = "Dashboard";

    public string UserName { get; set; } = "Kaung Myat Htun";
    public string UserRole { get; set; } = "Branch Manager";
    public string UserInitials { get; set; } = "KMH";

    public ObservableCollection<NavItem> Items { get; } = new();

    // Like onNavigate prop
    public ICommand? NavigateCommand { get; set; }

    public NavBarViewModel()
    {
        Items.Add(new() { Label = "Dashboard",    IconKey = "Icon.Dashboard"    });
        Items.Add(new() { Label = "Accounts",     IconKey = "Icon.Accounts"     });
        Items.Add(new() { Label = "Transactions", IconKey = "Icon.Transactions", Badge = "4" });
        Items.Add(new() { Label = "Transfers",    IconKey = "Icon.Transfers"    });
        Items.Add(new() { Label = "Loans",        IconKey = "Icon.Loans"        });
        Items.Add(new() { Label = "Reports",      IconKey = "Icon.Reports"      });
        Items.Add(new() { Label = "Settings",     IconKey = "Icon.Settings"     });

        foreach (var item in Items)
            item.Command = new RelayCommand(() => Select(item));
    }

    [RelayCommand]
    private void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    private void Select(NavItem item)
    {
        ActiveItem = item.Label;
        NavigateCommand?.Execute(item.Label);
    }
}