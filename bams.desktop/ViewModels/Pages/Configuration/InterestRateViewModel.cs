using System.Collections.ObjectModel;
using bams.desktop.Models.Configuration;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// ViewModel for the Interest Rate list page.
public sealed class InterestRateViewModel : ViewModelBase
{
    public string PageTitle => "Interest Rate";
    public string PageDescription => "Interest rate rules per account type.";

    public ObservableCollection<InterestRateRowModel> Rows { get; } = new();
}
