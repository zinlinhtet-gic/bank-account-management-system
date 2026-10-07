namespace bams.desktop.DTOs.Accounting;

public sealed record AccountReconciliationRequest(DateOnly FromDate, DateOnly ToDate, long? AccountId);
public sealed record AccountReconciliationResultResponse(long AccountId, string AccountNo, DateOnly BusinessDate,
    decimal OperationalBalance, decimal LedgerBalance, decimal Difference, string Status);
public sealed record AccountReconciliationRunResponse(long RunId, DateOnly FromDate, DateOnly ToDate, string Status,
    DateTime PerformedAtUtc, IReadOnlyList<AccountReconciliationResultResponse> Results);
public sealed record ReconciliationExceptionResponse(long Id, string Type, string Source, DateOnly BusinessDate,
    long? AccountId, long? PositionSessionId, decimal ExpectedAmount, decimal ActualAmount, decimal Difference, string Severity,
    string Status, long? AssignedTo, long? RelatedTransactionId, long? CorrectionTransactionId, string? Notes,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string? CorrectionRequestStatus = null,
    long? RequestedCorrectionTransactionId = null, long? CorrectionRequestedBy = null,
    DateTime? CorrectionRequestedAtUtc = null, long? CorrectionReviewedBy = null,
    DateTime? CorrectionReviewedAtUtc = null, string? CorrectionRequestReason = null,
    string? CorrectionExternalRecoveryReference = null, string? CorrectionReviewNote = null);
public sealed record RequestTransactionCorrectionRequest(long TransactionId, string Reason, string? ExternalRecoveryReference);
public sealed record ReviewTransactionCorrectionRequest(bool Approve, string Note, string? RecoveryAttestation);
public sealed record CorrectionTransactionCandidateResponse(long Id, string TransactionNo, string Type, string Status,
    decimal Amount, DateTime? PostedAt, string? Description, string JournalPreview);
public sealed record ReconciliationAccountOptionResponse(long Id, string AccountNo, string AccountType, string Status)
{
    public string DisplayLabel => $"{AccountNo}  ·  {AccountType}  ·  {Status}";
}
public sealed record ReconciliationStaffOptionResponse(long Id, string FullName, string Username, string Role)
{
    public string DisplayLabel => $"{FullName}  ·  {Username}  ·  {Role}";
}
public sealed record UpdateReconciliationExceptionRequest(string Status, string? Notes, long? AssignedTo, long? CorrectionTransactionId);
public sealed record ReconciliationExceptionHistoryResponse(long Id, string OldStatus, string NewStatus,
    string? Note, long ActorId, DateTime CreatedAtUtc);
public sealed record ReconciliationExceptionDetailResponse(ReconciliationExceptionResponse Exception,
    IReadOnlyList<ReconciliationExceptionHistoryResponse> Timeline);
