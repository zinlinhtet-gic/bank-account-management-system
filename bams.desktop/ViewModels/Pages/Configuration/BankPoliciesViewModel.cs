using System.Collections.ObjectModel;
using bams.desktop.Models.Configuration;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// ViewModel for the Bank Policies list page.
public sealed class BankPoliciesViewModel : ViewModelBase
{
    public string PageTitle => "Bank Policies";
    public string PageDescription => "Opening balance, minimum maintained balance and limits per account type.";

    public ObservableCollection<BankPolicyRowModel> Rows { get; } = new();
}
