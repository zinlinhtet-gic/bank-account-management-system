using System.Collections.ObjectModel;
using bams.desktop.Models.Configuration;

namespace bams.desktop.ViewModels.Pages.Configuration;


/// ViewModel for the Other Banks list page.
public sealed class OtherBanksViewModel : ViewModelBase
{
    public string PageTitle => "Other Banks";
    public string PageDescription => "View only. Correspondent bank records are managed by back-office operations.";

    public ObservableCollection<OtherBankRowModel> Rows { get; } = new();
}
