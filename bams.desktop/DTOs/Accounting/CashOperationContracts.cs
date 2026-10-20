namespace bams.desktop.DTOs.Accounting;

public sealed record OpenCashSessionRequest(string PositionType, decimal OpeningCash, long? TellerId = null,
    DateTime? ScheduledStartAtMyanmar = null, DateTime? ScheduledEndAtMyanmar = null);
public sealed record ActivateCashSessionRequest(long ExpectedSessionVersion);
public sealed record CreateCashProposalRequest(decimal? RequestedAmount, string? Note);
public sealed record ApproveCashProposalRequest(decimal ApprovedAmount, long ExpectedVersion);
public sealed record CashProposalActionRequest(long ExpectedVersion, string? Note);
public sealed record CashProposalResponse(long Id, long SessionId, long VaultSessionId, long OfficerId,
    decimal? RequestedAmount, decimal? ApprovedAmount, string Status, string? Note, DateTime CreatedAtUtc,
    DateTime? ApprovedAtUtc, long? ApprovedBy, DateTime? ResolvedAtUtc, long Version);
public sealed record CashSessionAssigneeResponse(long UserId, string FullName, string Username);
public sealed record TransferCashRequest(long DestinationSessionId, decimal Amount, string? Note);
public sealed record SubmitCashCountRequest(decimal ActualAmount, string? Notes, long? HandoffRecipientUserId, long ExpectedSessionVersion, bool ReturnToVault = false);
public sealed record CashPositionSessionResponse(long Id, string PositionType, long? TellerId,
    DateOnly BusinessDate, decimal OpeningCash, decimal ExpectedClosingCash, string Status, long Version,
    DateTime? ScheduledStartAtMyanmar = null, DateTime? ScheduledEndAtMyanmar = null,
    DateTime? ActivatedAtUtc = null, decimal ReservedCash = 0m, string? OfficerName = null, DateTime? ClosedAtUtc = null, long? ClosedBy = null)
{
    public decimal ScheduledStartingCash { get; init; }
    public bool CanAccept { get; init; }
    public bool CanRequestCash { get; init; }
    public bool CanClose { get; init; }
    public bool CanCancel { get; init; }
}
public sealed record CashCountResponse(long Id, long SessionId, decimal ExpectedAmount, decimal ActualAmount,
    decimal Difference, string Status, DateTime CountedAtUtc, long? HandoffId = null, string? HandoffStatus = null);
public sealed record CashHandoffRecipientResponse(long UserId, string FullName, string Username);
public sealed record CashHandoffResponse(long Id, long SessionId, DateOnly BusinessDate, long SenderUserId,
    string SenderName, long RecipientUserId, string RecipientName, decimal Amount, string Status,
    DateTime CreatedAtUtc, DateTime? AcceptedAtUtc, long Version, long? DestinationSessionId = null);
public sealed record CashHandoffHistoryResponse(long Id, string OldStatus, string NewStatus,
    long? PreviousRecipientUserId, long? RecipientUserId, string? Note, long ActorUserId, DateTime CreatedAtUtc);
public sealed record CashHandoffDetailResponse(CashHandoffResponse Handoff,
    IReadOnlyList<CashHandoffHistoryResponse> History);
public sealed record CashHandoffActionRequest(string? Note, long ExpectedVersion);
public sealed record ReassignCashHandoffRequest(long RecipientUserId, string? Note, long ExpectedVersion);
public sealed record CashMovementHistoryResponse(long Id, string Type, string Status, decimal Amount,
    long? DestinationSessionId, long? TransactionId, long? CorrectionTransactionId, long ActorId,
    long? ApprovedBy, DateTime CreatedAtUtc, string? Note);
public sealed record CashCountHistoryResponse(long Id, decimal ExpectedAmount, decimal ActualAmount,
    decimal Difference, long CountedBy, DateTime CountedAtUtc, string? Notes);
public sealed record CashPositionSessionDetailResponse(CashPositionSessionResponse Session,
    IReadOnlyList<CashMovementHistoryResponse> Movements, IReadOnlyList<CashCountHistoryResponse> Counts, IReadOnlyList<SessionTransactionResponse>? Transactions = null, IReadOnlyList<CashProposalResponse>? Requests = null);
public sealed record RequestCashAdjustmentRequest(decimal SignedAmount, long CorrectionTransactionId, string? Note);
public sealed record RejectCashAdjustmentRequest(string Reason);
public sealed record CashAdjustmentResponse(long Id, long SessionId, decimal SignedAmount, long CorrectionTransactionId,
    string Status, long RequestedBy, DateTime RequestedAtUtc, long? ApprovedBy, DateTime? ApprovedAtUtc, string? Note);

public sealed record ScheduledCashSessionRequest(long OfficerId, DateTime StartAtMyanmar, DateTime EndAtMyanmar, decimal StartingCash);
public sealed record CreateSessionSchedulesRequest(IReadOnlyList<ScheduledCashSessionRequest> Sessions, string IdempotencyKey, DateOnly? ExpectedDate = null);
public sealed record SessionTransactionResponse(long Id, string TransactionNo, string Type, string Status, decimal Amount, DateTime TransactionAt);
