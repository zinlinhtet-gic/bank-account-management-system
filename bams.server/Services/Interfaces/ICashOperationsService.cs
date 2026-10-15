using bams.server.DTO.Accounting;
using bams.server.Models.Transactions;

namespace bams.server.Services.Interfaces;

public interface ICashOperationsService
{
    Task<CashPositionSessionResponse> OpenSessionAsync(OpenCashSessionRequest request, string? idempotencyKey, CancellationToken cancellationToken);
    Task<CashPositionSessionResponse> ActivateSessionAsync(long sessionId, ActivateCashSessionRequest request, CancellationToken cancellationToken);
    Task<CashPositionSessionResponse> CancelScheduledSessionAsync(long sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashSessionAssigneeResponse>> GetSessionAssigneesAsync(CancellationToken cancellationToken);
    Task<CashProposalResponse> CreateCashProposalAsync(long sessionId, CreateCashProposalRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashProposalResponse>> GetCashProposalsAsync(DateOnly? businessDate, CancellationToken cancellationToken);
    Task<CashProposalResponse> ApproveCashProposalAsync(long proposalId, ApproveCashProposalRequest request, CancellationToken cancellationToken);
    Task<CashProposalResponse> ReceiveCashProposalAsync(long proposalId, CashProposalActionRequest request, CancellationToken cancellationToken);
    Task<CashProposalResponse> RejectCashProposalAsync(long proposalId, CashProposalActionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashPositionSessionResponse>> GetSessionsAsync(DateOnly? businessDate, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashHandoffRecipientResponse>> GetHandoffRecipientsAsync(CancellationToken cancellationToken);
    Task<CashPositionSessionDetailResponse> GetSessionDetailAsync(long sessionId, CancellationToken cancellationToken);
    Task<CashPositionSessionResponse> TransferCashAsync(long sessionId, TransferCashRequest request, CancellationToken cancellationToken);
    Task<CashCountResponse> SubmitCountAsync(long sessionId, SubmitCashCountRequest request, string? idempotencyKey, CancellationToken cancellationToken);
    Task<CashAdjustmentResponse> RequestAdjustmentAsync(long sessionId, RequestCashAdjustmentRequest request, CancellationToken cancellationToken);
    Task<CashAdjustmentResponse> ApproveAdjustmentAsync(long movementId, CancellationToken cancellationToken);
    Task<CashAdjustmentResponse> RejectAdjustmentAsync(long movementId, string reason, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashAdjustmentResponse>> GetAdjustmentsAsync(DateOnly? businessDate, string? status, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashHandoffResponse>> GetCashHandoffsAsync(DateOnly? businessDate, CancellationToken cancellationToken);
    Task<CashHandoffDetailResponse> GetCashHandoffDetailAsync(long handoffId, CancellationToken cancellationToken);
    Task<CashHandoffResponse> AcceptCashHandoffAsync(long handoffId, CashHandoffActionRequest request, CancellationToken cancellationToken);
    Task<CashHandoffResponse> DeclineCashHandoffAsync(long handoffId, CashHandoffActionRequest request, CancellationToken cancellationToken);
    Task<CashHandoffResponse> ReassignCashHandoffAsync(long handoffId, ReassignCashHandoffRequest request, CancellationToken cancellationToken);
    Task AddTransactionMovementAsync(Transaction transaction, bool isDeposit, long actorId, decimal amount, CancellationToken cancellationToken);
}
