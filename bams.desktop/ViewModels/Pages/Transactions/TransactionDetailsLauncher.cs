using bams.desktop.DTOs.Transactions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// Opens a transaction's detail card and, when the user picks an entry's "Statement", that account's statement.
/// Shared by the Transactions and Transaction History pages so both behave the same.
/// </summary>
public sealed class TransactionDetailsLauncher
{
    private readonly ITransactionService _transactionService;
    private readonly IDialogService _dialogService;

    public TransactionDetailsLauncher(ITransactionService transactionService, IDialogService dialogService)
    {
        _transactionService = transactionService;
        _dialogService = dialogService;
    }

    /// <summary>
    /// Shows the loaded transaction, then the requested statement (if any). Returns an error message when the
    /// statement could not be loaded, otherwise null.
    /// </summary>
    public async Task<string?> ShowAsync(TransactionDetailResponse transaction)
    {
        var details = new TransactionDetailsViewModel(transaction);
        if (!_dialogService.ShowDialog(details) || details.RequestedStatement is not { } entry)
        {
            return null;
        }

        // Load before showing, so a failure is reported instead of an empty dialog.
        var statement = new AccountStatementViewModel(_transactionService, entry.AccountId, entry.AccountNo);
        if (!await statement.LoadAsync())
        {
            return statement.ErrorMessage;
        }

        _dialogService.ShowDialog(statement);
        return null;
    }
}
