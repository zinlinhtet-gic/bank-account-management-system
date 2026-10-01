using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace Bams.Desktop.Components.NavBar;

public partial class NavItem : ObservableObject
{
    public string Label { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;   // e.g. "Icon.Dashboard"
    public string? Badge { get; set; }
    public ICommand? Command { get; set; }

    public ObservableCollection<NavItem> Children { get; } = new();
    public bool HasChildren => Children.Count > 0;
    
    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isExpanded;
}