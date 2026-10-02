namespace bams.server.DTO.Accounting;

public sealed record OpenCashSessionRequest(long BranchId, string PositionType, decimal OpeningCash);
public sealed record TransferCashRequest(long DestinationSessionId, decimal Amount, string? Note);
public sealed record SubmitCashCountRequest(decimal ActualAmount, string? Notes);
public sealed record RequestCashAdjustmentRequest(decimal SignedAmount, long CorrectionTransactionId, string? Note);
public sealed record CashAdjustmentResponse(long Id, long SessionId, decimal SignedAmount, long CorrectionTransactionId,
    string Status, long RequestedBy, DateTime RequestedAtUtc, long? ApprovedBy, DateTime? ApprovedAtUtc, string? Note);
public sealed record CashPositionSessionResponse(long Id, long BranchId, string PositionType, long? TellerId,
    DateOnly BusinessDate, decimal OpeningCash, decimal ExpectedClosingCash, string Status);
public sealed record CashCountResponse(long Id, long SessionId, decimal ExpectedAmount, decimal ActualAmount,
    decimal Difference, string Status, DateTime CountedAtUtc);
public sealed record CashMovementHistoryResponse(long Id, string Type, string Status, decimal Amount,
    long? DestinationSessionId, long? TransactionId, long? CorrectionTransactionId, long ActorId,
    long? ApprovedBy, DateTime CreatedAtUtc, string? Note);
public sealed record CashCountHistoryResponse(long Id, decimal ExpectedAmount, decimal ActualAmount,
    decimal Difference, long CountedBy, DateTime CountedAtUtc, string? Notes);
public sealed record CashPositionSessionDetailResponse(CashPositionSessionResponse Session,
    IReadOnlyList<CashMovementHistoryResponse> Movements, IReadOnlyList<CashCountHistoryResponse> Counts);
