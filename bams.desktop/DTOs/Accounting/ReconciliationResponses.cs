namespace bams.desktop.DTOs.Accounting;

public sealed record AccountReconciliationRequest(DateOnly FromDate, DateOnly ToDate, long? AccountId);
public sealed record AccountReconciliationResultResponse(long AccountId, string AccountNo, DateOnly BusinessDate,
    decimal OperationalBalance, decimal LedgerBalance, decimal Difference, string Status);
public sealed record AccountReconciliationRunResponse(long RunId, DateOnly FromDate, DateOnly ToDate, string Status,
    DateTime PerformedAtUtc, IReadOnlyList<AccountReconciliationResultResponse> Results);
public sealed record ReconciliationExceptionResponse(long Id, string Type, string Source, DateOnly BusinessDate,
    long? BranchId, long? AccountId, long? PositionSessionId, decimal ExpectedAmount, decimal ActualAmount, decimal Difference, string Severity,
    string Status, long? AssignedTo, long? RelatedTransactionId, long? CorrectionTransactionId, string? Notes,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record UpdateReconciliationExceptionRequest(string Status, string? Notes, long? AssignedTo, long? CorrectionTransactionId);
public sealed record ReconciliationExceptionHistoryResponse(long Id, string OldStatus, string NewStatus,
    string? Note, long ActorId, DateTime CreatedAtUtc);
public sealed record ReconciliationExceptionDetailResponse(ReconciliationExceptionResponse Exception,
    IReadOnlyList<ReconciliationExceptionHistoryResponse> Timeline);
