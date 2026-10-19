using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

public interface ICashOperationsClientService
{
    Task<IReadOnlyList<CashPositionSessionResponse>> CreateSchedulesAsync(CreateSessionSchedulesRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<CashPositionSessionResponse>> GetSessionsAsync(DateOnly? date, CancellationToken cancellationToken);
    Task<CashPositionSessionDetailResponse> GetSessionDetailAsync(long sessionId, CancellationToken cancellationToken);
    Task<CashPositionSessionResponse> OpenSessionAsync(OpenCashSessionRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<CashPositionSessionResponse> ActivateSessionAsync(long sessionId, ActivateCashSessionRequest request, CancellationToken cancellationToken);
    Task<CashPositionSessionResponse> CancelScheduledSessionAsync(long sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashSessionAssigneeResponse>> GetSessionAssigneesAsync(CancellationToken cancellationToken);
    Task<CashProposalResponse> CreateCashProposalAsync(long sessionId, CreateCashProposalRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashProposalResponse>> GetCashProposalsAsync(DateOnly? date, CancellationToken cancellationToken);
    Task<CashProposalResponse> ApproveCashProposalAsync(long id, ApproveCashProposalRequest request, CancellationToken cancellationToken);
    Task<CashProposalResponse> ReceiveCashProposalAsync(long id, CashProposalActionRequest request, CancellationToken cancellationToken);
    Task<CashProposalResponse> RejectCashProposalAsync(long id, CashProposalActionRequest request, CancellationToken cancellationToken);
    Task<CashPositionSessionResponse> TransferCashAsync(long sessionId, TransferCashRequest request, CancellationToken cancellationToken);
    Task<CashCountResponse> SubmitCountAsync(long sessionId, SubmitCashCountRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashHandoffRecipientResponse>> GetHandoffRecipientsAsync(CancellationToken cancellationToken, long? sourceSessionId = null);
    Task<IReadOnlyList<CashHandoffResponse>> GetCashHandoffsAsync(DateOnly? date, CancellationToken cancellationToken);
    Task<CashHandoffDetailResponse> GetCashHandoffDetailAsync(long handoffId, CancellationToken cancellationToken);
    Task<CashHandoffResponse> AcceptCashHandoffAsync(long handoffId, CashHandoffActionRequest request, CancellationToken cancellationToken);
    Task<CashHandoffResponse> DeclineCashHandoffAsync(long handoffId, CashHandoffActionRequest request, CancellationToken cancellationToken);
    Task<CashHandoffResponse> ReassignCashHandoffAsync(long handoffId, ReassignCashHandoffRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashAdjustmentResponse>> GetAdjustmentsAsync(DateOnly? date, string? status, CancellationToken cancellationToken);
    Task<CashAdjustmentResponse> RequestAdjustmentAsync(long sessionId, RequestCashAdjustmentRequest request, CancellationToken cancellationToken);
    Task<CashAdjustmentResponse> ApproveAdjustmentAsync(long adjustmentId, CancellationToken cancellationToken);
    Task<CashAdjustmentResponse> RejectAdjustmentAsync(long adjustmentId, RejectCashAdjustmentRequest request, CancellationToken cancellationToken);
}
