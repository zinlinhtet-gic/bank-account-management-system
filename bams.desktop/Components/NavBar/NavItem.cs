using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace Bams.Desktop.Components.NavBar;

public partial class NavItem : ObservableObject
{
    public string Label { get; set; } = "";
    public string IconKey { get; set; } = "";   // e.g. "Icon.Dashboard"
    public string? Badge { get; set; }
    public ICommand? Command { get; set; }

    [ObservableProperty]
    private bool _isActive;
}