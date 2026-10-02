using bams.server.DTO.Accounting;
using bams.server.Models.Transactions;

namespace bams.server.Services.Interfaces;

public interface ICashOperationsService
{
    Task<CashPositionSessionResponse> OpenSessionAsync(OpenCashSessionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashPositionSessionResponse>> GetSessionsAsync(DateOnly? businessDate, long? branchId, CancellationToken cancellationToken);
    Task<CashPositionSessionDetailResponse> GetSessionDetailAsync(long sessionId, CancellationToken cancellationToken);
    Task<CashPositionSessionResponse> TransferCashAsync(long sessionId, TransferCashRequest request, CancellationToken cancellationToken);
    Task<CashCountResponse> SubmitCountAsync(long sessionId, SubmitCashCountRequest request, CancellationToken cancellationToken);
    Task<CashAdjustmentResponse> RequestAdjustmentAsync(long sessionId, RequestCashAdjustmentRequest request, CancellationToken cancellationToken);
    Task<CashAdjustmentResponse> ApproveAdjustmentAsync(long movementId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashAdjustmentResponse>> GetAdjustmentsAsync(DateOnly? businessDate, long? branchId, string? status, CancellationToken cancellationToken);
    Task AddTransactionMovementAsync(long sessionId, Transaction transaction, bool isDeposit, long actorId, decimal amount, CancellationToken cancellationToken, long? expectedBranchId = null);
}
