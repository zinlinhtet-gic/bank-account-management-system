using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;
using bams.server.Models.Transactions;

namespace bams.server.Services.Interfaces;

public interface IAccountTransactionService
{
    /// <summary>Gets transaction entries for an account, newest first.</summary>
    Task<IReadOnlyList<AccountTransactionDetailResponse>> GetAccountTransactionsAsync(long accountId, CancellationToken cancellationToken);

    /// <summary>Records the opening transaction for a newly created account.</summary>
    Task RecordAccountOpeningTransactionAsync(
        Account account,
        decimal openingBalance,
        DateTime currentDateTime,
        CancellationToken cancellationToken);

    Task RecordScheduledTransactionAsync(Transaction transaction, Account account, decimal beforeLedger,
        decimal beforeAvailable, decimal afterLedger, decimal afterAvailable, decimal amount,
        EntryType entryType, DateOnly effectiveDate, string description, string status, DateTime createdAt,
        CancellationToken cancellationToken);

}
