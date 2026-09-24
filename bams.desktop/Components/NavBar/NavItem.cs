using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Bams.Desktop.Components.NavBar;

public partial class NavItem : ObservableObject
{
    public string Label { get; set; } = "";
    public string IconKey { get; set; } = "";   // e.g. "Icon.Dashboard"
    public string? Badge { get; set; }
    public ICommand? Command { get; set; }

    // Sub-menu items for nested navigation
    public ObservableCollection<NavItem>? Children { get; set; }

    public bool HasChildren => Children is { Count: > 0 };

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isExpanded;
}