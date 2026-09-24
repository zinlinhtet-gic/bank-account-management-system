using System.Collections.ObjectModel;
using bams.desktop.Models.Configuration;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// ViewModel for the Fee Rate list page.
public sealed class FeeRateViewModel : ViewModelBase
{
    public string PageTitle => "Fee Rate";
    public string PageDescription => "Fee rules per account type.";

    public ObservableCollection<FeeRateRowModel> Rows { get; } = new();
}
