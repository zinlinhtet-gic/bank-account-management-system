using bams.server.DTO.Common;
using bams.server.DTO.Transactions;

namespace bams.server.Services.Interfaces;

/// <summary>
/// NRC transfers: creation, pickup at our branches with a one-time code, payout recorded for other banks, and
/// cancellation with refund.
/// </summary>
public interface INrcTransferService
{
    Task<TransactionResponse> CreateTransferAsync(
        NrcTransferRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<TransactionResponse> CompletePickupAsync(
        NrcPickupRequest request,
        RequestActor actor,
        CancellationToken cancellationToken);

    Task<TransactionResponse> RecordOtherBankPayoutAsync(
        long transactionId,
        NrcPayoutRequest request,
        RequestActor actor,
        CancellationToken cancellationToken);

    Task<TransactionResponse> CancelTransferAsync(
        long transactionId,
        NrcCancelRequest request,
        RequestActor actor,
        CancellationToken cancellationToken);
}
