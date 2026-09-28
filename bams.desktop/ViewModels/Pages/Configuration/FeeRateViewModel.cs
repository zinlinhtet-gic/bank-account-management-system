using System.Collections.ObjectModel;
using bams.desktop.Models.Configuration;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// ViewModel for the Fee Rate list page.
public sealed class FeeRateViewModel : ViewModelBase
{
    public string PageTitle => "Fee Rate";
    public string PageDescription => "Fee rules per account type.";

    public ObservableCollection<FeeRateRowModel> Rows { get; } = new();

    public bool IsEmpty => Rows.Count == 0;

    /// Counter shown next to the table title, e.g. "3 rules".
    public string CountText => Rows.Count == 1 ? "1 rule" : $"{Rows.Count} rules";
}
