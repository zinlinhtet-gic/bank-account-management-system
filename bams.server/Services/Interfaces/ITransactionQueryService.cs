using bams.server.DTO.Common;
using bams.server.DTO.Transactions;

namespace bams.server.Services.Interfaces;

/// <summary>
/// Read-only transaction list, detail and account statement.
/// </summary>
public interface ITransactionQueryService
{
    Task<PagedResponse<TransactionSummaryResponse>> GetTransactionsAsync(
        TransactionListQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OtherBankResponse>> GetOtherBanksAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BranchResponse>> GetBranchesAsync(CancellationToken cancellationToken);

    Task<TransactionDetailResponse> GetTransactionByIdAsync(
        long id,
        CancellationToken cancellationToken);

    Task<PagedResponse<AccountStatementLineResponse>> GetAccountStatementAsync(
        long accountId,
        AccountStatementQuery query,
        CancellationToken cancellationToken);
}
