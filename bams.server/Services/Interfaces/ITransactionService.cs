using bams.server.DTO.Common;
using bams.server.DTO.Transactions;

namespace bams.server.Services.Interfaces;

/// <summary>
/// Cash deposits, cash withdrawals and internal transfers.
/// </summary>
public interface ITransactionService
{
    Task<TransactionResponse> DepositAsync(
        DepositRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<TransactionResponse> WithdrawAsync(
        WithdrawalRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<TransactionResponse> TransferInternallyAsync(
        InternalTransferRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken);
}
