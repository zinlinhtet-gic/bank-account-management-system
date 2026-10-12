namespace bams.server.DTO.Accounting;

public sealed record AccountReconciliationResultResponse(
    long AccountId,
    string AccountNo,
    DateOnly BusinessDate,
    decimal OperationalBalance,
    decimal LedgerBalance,
    decimal Difference,
    string Status);

public sealed record AccountReconciliationRunResponse(
    long RunId,
    DateOnly FromDate,
    DateOnly ToDate,
    string Status,
    DateTime PerformedAtUtc,
    IReadOnlyList<AccountReconciliationResultResponse> Results);

public sealed record ReconciliationExceptionResponse(
    long Id,
    string Type,
    string Source,
    DateOnly BusinessDate,
    long? AccountId,
    long? PositionSessionId,
    decimal ExpectedAmount,
    decimal ActualAmount,
    decimal Difference,
    string Severity,
    string Status,
    long? AssignedTo,
    long? RelatedTransactionId,
    long? CorrectionTransactionId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? CorrectionRequestStatus = null,
    long? RequestedCorrectionTransactionId = null,
    long? CorrectionRequestedBy = null,
    DateTime? CorrectionRequestedAtUtc = null,
    long? CorrectionReviewedBy = null,
    DateTime? CorrectionReviewedAtUtc = null,
    string? CorrectionRequestReason = null,
    string? CorrectionExternalRecoveryReference = null,
    string? CorrectionReviewNote = null);

public sealed record ReconciliationExceptionHistoryResponse(long Id, string OldStatus, string NewStatus,
    string? Note, long ActorId, DateTime CreatedAtUtc);
public sealed record ReconciliationExceptionDetailResponse(ReconciliationExceptionResponse Exception,
    IReadOnlyList<ReconciliationExceptionHistoryResponse> Timeline);
public sealed record CorrectionTransactionCandidateResponse(long Id, string TransactionNo, string Type, string Status,
    decimal Amount, DateTime? PostedAt, string? Description, string JournalPreview);
public sealed record ReconciliationAccountOptionResponse(long Id, string AccountNo, string AccountType, string Status);
public sealed record ReconciliationStaffOptionResponse(long Id, string FullName, string Username, string Role);
