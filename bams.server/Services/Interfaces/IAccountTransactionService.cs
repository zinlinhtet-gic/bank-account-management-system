using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;

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

}
