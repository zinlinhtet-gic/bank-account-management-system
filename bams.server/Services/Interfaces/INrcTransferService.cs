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

    /// <summary>
    /// Replaces the pickup code of a pending NRC transfer (lost code, or a create response that never arrived).
    /// The old code stops working, failed attempts reset and the new code gets a fresh validity period.
    /// The new code is returned once, in <see cref="TransactionResponse.PickupCode"/>, and is never stored.
    /// </summary>
    Task<TransactionResponse> ReissuePickupCodeAsync(
        long transactionId,
        RequestActor actor,
        CancellationToken cancellationToken);

    Task<TransactionResponse> CancelTransferAsync(
        long transactionId,
        NrcCancelRequest request,
        RequestActor actor,
        CancellationToken cancellationToken);
}
