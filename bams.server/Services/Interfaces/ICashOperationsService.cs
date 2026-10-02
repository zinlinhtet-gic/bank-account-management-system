using bams.server.DTO.Accounting;
using bams.server.Models.Transactions;

namespace bams.server.Services.Interfaces;

public interface ICashOperationsService
{
    Task<CashPositionSessionResponse> OpenSessionAsync(OpenCashSessionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashPositionSessionResponse>> GetSessionsAsync(DateOnly? businessDate, CancellationToken cancellationToken);
    Task<CashPositionSessionDetailResponse> GetSessionDetailAsync(long sessionId, CancellationToken cancellationToken);
    Task<CashPositionSessionResponse> TransferCashAsync(long sessionId, TransferCashRequest request, CancellationToken cancellationToken);
    Task<CashCountResponse> SubmitCountAsync(long sessionId, SubmitCashCountRequest request, CancellationToken cancellationToken);
    Task<CashAdjustmentResponse> RequestAdjustmentAsync(long sessionId, RequestCashAdjustmentRequest request, CancellationToken cancellationToken);
    Task<CashAdjustmentResponse> ApproveAdjustmentAsync(long movementId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashAdjustmentResponse>> GetAdjustmentsAsync(DateOnly? businessDate, string? status, CancellationToken cancellationToken);
    Task AddTransactionMovementAsync(Transaction transaction, bool isDeposit, long actorId, decimal amount, CancellationToken cancellationToken);
}
