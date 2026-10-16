

namespace bams.desktop.DTOs.Accounting;

public enum CashDiscrepancyKind { CountEntryError = 1, UnrecordedDeposit, UnrecordedWithdrawal, MissingMovement, DuplicateMovement, IncorrectMovementAmount, WrongCashPosition, TransferMismatch, HandoffMismatch, UnexplainedOverShort, BankExpenseNotRecorded }

public sealed record UnifiedReconciliationRequest(DateOnly FromDate, DateOnly ToDate);
public sealed record UnifiedExceptionRow(long Id, string Type, DateOnly BusinessDate, long? AccountId,
    long? PositionSessionId, long? RelatedTransactionId, decimal ExpectedAmount, decimal ActualAmount,
    decimal Difference, string Status, long? AssignedTo, string? Notes);
public sealed record UnifiedReconciliationResponse(long AccountRunId, DateOnly FromDate, DateOnly ToDate,
    IReadOnlyList<UnifiedExceptionRow> AccountAndGl, IReadOnlyList<UnifiedExceptionRow> Cash);
public sealed record AssignReconciliationRequest(long InvestigatorId, string? Note);
public sealed record FinalVaultCountRequest(DateOnly BusinessDate, decimal ActualAmount, string? Note);
public sealed record ForcePassRequest(string Reason);
public sealed record CashCorrectionInputs(CashDiscrepancyKind Kind, string Findings, string EvidenceReference,
    decimal? Amount = null, long? AccountId = null, long? TransactionId = null,
    long? MovementId = null, long? DestinationSessionId = null, string? Payee = null,
    string? Purpose = null, string? ReceiptFileName = null, string? ReceiptContentType = null,
    string? ReceiptBase64 = null, string? MissingReceiptReason = null);
public sealed record CashCorrectionPreview(CashDiscrepancyKind Kind, DateOnly BusinessDate,
    decimal SuggestedAmount, decimal CashChange, string Explanation, string SourceHash);
public sealed record SubmitCashCorrectionRequest(CashCorrectionInputs Inputs, string ExpectedSourceHash);
public sealed record CashCorrectionProposalResponse(long Id, long ExceptionId, CashDiscrepancyKind Kind,
    ProposalStatus Status, VerificationStatus Verification, long ProposerId, long? ReviewerId,
    CashCorrectionInputs Inputs, long? ExecutionTransactionId, string? ReviewNote, string? VerificationNote,
    DateTime CreatedAtUtc);
public sealed record CashDiscrepancyDefinition(CashDiscrepancyKind Kind, string Name, string RequiredInputs);

