namespace bams.desktop.ViewModels.Pages;

/// <summary>
/// ViewModel for the Transaction History page.
/// Allows auditors and managers to view transaction history.
/// </summary>
public sealed class TransactionHistoryViewModel : ViewModelBase
{
    public string PageTitle => "Transaction History";
    public string PageDescription => "View historical transaction records";
}