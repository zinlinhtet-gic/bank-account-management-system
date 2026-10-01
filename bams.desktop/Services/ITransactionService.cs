using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Transactions;

namespace bams.desktop.Services;

/// <summary>
/// Transaction API calls (<c>api/transactions</c>). Postings need the <c>transactions</c> permission; the reads also
/// accept <c>transaction_history</c>. Postings take an idempotency key: send the same key when retrying the same
/// user action, so the server never posts it twice.
/// </summary>
public interface ITransactionService
{
    /// <summary>Loads one page of transactions matching the filter, newest first.</summary>
    Task<PagedResponse<TransactionSummaryResponse>> GetTransactionsAsync(
        TransactionListFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Loads one transaction with its account entries and NRC or interbank detail.</summary>
    Task<TransactionDetailResponse> GetTransactionByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>Loads one page of an account's entries, newest first, with the balance after each.</summary>
    /// <param name="from">Entries at or after this instant; null for no lower bound.</param>
    /// <param name="before">Entries before this instant (exclusive); null for no upper bound.</param>
    Task<PagedResponse<AccountStatementLineResponse>> GetAccountStatementAsync(
        long accountId,
        DateTimeOffset? from,
        DateTimeOffset? before,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Loads the branches where NRC transfers can be collected.</summary>
    Task<IReadOnlyList<BranchResponse>> GetBranchesAsync(CancellationToken cancellationToken);

    /// <summary>Loads the banks an interbank transfer can be sent to.</summary>
    Task<IReadOnlyList<OtherBankResponse>> GetOtherBanksAsync(CancellationToken cancellationToken);

    /// <summary>Deposits cash into an account.</summary>
    Task<TransactionResponse> DepositAsync(DepositRequest request, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Withdraws cash from an account.</summary>
    Task<TransactionResponse> WithdrawAsync(WithdrawalRequest request, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Transfers funds between two accounts in this bank.</summary>
    Task<TransactionResponse> TransferInternallyAsync(
        InternalTransferRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Submits a transfer to another bank; it stays pending until settled or failed.</summary>
    Task<TransactionResponse> TransferInterbankAsync(
        InterbankTransferRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Creates a pending NRC transfer; the response carries the one-time pickup code.</summary>
    Task<TransactionResponse> CreateNrcTransferAsync(
        NrcTransferRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Records that the other bank paid out an NRC transfer collected there.</summary>
    Task<TransactionResponse> RecordNrcPayoutAsync(long id, NrcPayoutRequest request, CancellationToken cancellationToken);

    /// <summary>Pays out an NRC transfer at our branch with its pickup code (cash or into an account).</summary>
    Task<TransactionResponse> CompleteNrcPickupAsync(NrcPickupRequest request, CancellationToken cancellationToken);

    /// <summary>Cancels a pending NRC transfer and refunds the sender; returns the refund transaction.</summary>
    Task<TransactionResponse> CancelNrcTransferAsync(long id, NrcCancelRequest request, CancellationToken cancellationToken);

    /// <summary>Records that the gateway settled a pending interbank transfer.</summary>
    Task<TransactionResponse> CompleteInterbankTransferAsync(
        long id,
        InterbankSettlementRequest request,
        CancellationToken cancellationToken);

    /// <summary>Records that the gateway rejected a pending interbank transfer; returns the refund transaction.</summary>
    Task<TransactionResponse> FailInterbankTransferAsync(
        long id,
        InterbankFailureRequest request,
        CancellationToken cancellationToken);
}
