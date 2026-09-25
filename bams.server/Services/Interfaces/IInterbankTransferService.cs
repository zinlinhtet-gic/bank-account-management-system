using bams.server.DTO.Common;
using bams.server.DTO.Transactions;

namespace bams.server.Services.Interfaces;

/// <summary>
/// Interbank transfers and the recording of their payment-gateway result.
/// </summary>
public interface IInterbankTransferService
{
    Task<TransactionResponse> CreateTransferAsync(
        InterbankTransferRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<TransactionResponse> CompleteTransferAsync(
        long transactionId,
        InterbankSettlementRequest request,
        RequestActor actor,
        CancellationToken cancellationToken);

    Task<TransactionResponse> FailTransferAsync(
        long transactionId,
        InterbankFailureRequest request,
        RequestActor actor,
        CancellationToken cancellationToken);
}
