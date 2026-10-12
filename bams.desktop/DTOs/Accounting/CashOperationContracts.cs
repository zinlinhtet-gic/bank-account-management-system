namespace bams.desktop.DTOs.Accounting;

public sealed record OpenCashSessionRequest(string PositionType, decimal OpeningCash);
public sealed record TransferCashRequest(long DestinationSessionId, decimal Amount, string? Note);
public sealed record SubmitCashCountRequest(decimal ActualAmount, string? Notes, long? HandoffRecipientUserId, long ExpectedSessionVersion);
public sealed record CashPositionSessionResponse(long Id, string PositionType, long? TellerId,
    DateOnly BusinessDate, decimal OpeningCash, decimal ExpectedClosingCash, string Status, long Version);
public sealed record CashCountResponse(long Id, long SessionId, decimal ExpectedAmount, decimal ActualAmount,
    decimal Difference, string Status, DateTime CountedAtUtc, long? HandoffId = null, string? HandoffStatus = null);
public sealed record CashHandoffRecipientResponse(long UserId, string FullName, string Username);
public sealed record CashHandoffResponse(long Id, long SessionId, DateOnly BusinessDate, long SenderUserId,
    string SenderName, long RecipientUserId, string RecipientName, decimal Amount, string Status,
    DateTime CreatedAtUtc, DateTime? AcceptedAtUtc, long Version);
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
    IReadOnlyList<CashMovementHistoryResponse> Movements, IReadOnlyList<CashCountHistoryResponse> Counts);
public sealed record RequestCashAdjustmentRequest(decimal SignedAmount, long CorrectionTransactionId, string? Note);
public sealed record RejectCashAdjustmentRequest(string Reason);
public sealed record CashAdjustmentResponse(long Id, long SessionId, decimal SignedAmount, long CorrectionTransactionId,
    string Status, long RequestedBy, DateTime RequestedAtUtc, long? ApprovedBy, DateTime? ApprovedAtUtc, string? Note);
