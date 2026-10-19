

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
    string? ReceiptBase64 = null, string? MissingReceiptReason = null,
    long? CounterpartMovementId = null, long? HandoffId = null);
public sealed record CashPositionCorrectionEffect(long SessionId, decimal CashChange)
{
    public decimal BeforeExpected { get; init; }
    public decimal AfterExpected { get; init; }
    public decimal? BeforeCounted { get; init; }
    public decimal? AfterCounted { get; init; }
}
public sealed record CashCorrectionPreview(CashDiscrepancyKind Kind, DateOnly BusinessDate,
    decimal SuggestedAmount, decimal CashChange, string Explanation, string SourceHash, string PreviewHash)
{
    public DateOnly PostingDate { get; init; }
    public IReadOnlyList<CashPositionCorrectionEffect> PositionEffects { get; init; } = [];
    public IReadOnlyList<CashPlannedJournalLine> JournalLines { get; init; } = [];
    public IReadOnlyList<CashAccountEffect> AccountEffects { get; init; } = [];
}
public sealed record SubmitCashCorrectionRequest(CashCorrectionInputs Inputs, string ExpectedPreviewHash);
public sealed record CashCorrectionProposalResponse(long Id, long ExceptionId, CashDiscrepancyKind Kind,
    ProposalStatus Status, VerificationStatus Verification, long ProposerId, long? ReviewerId,
    CashCorrectionInputs Inputs, long? ExecutionTransactionId, string? ReviewNote, string? VerificationNote,
    DateTime CreatedAtUtc)
{
    public DateOnly? SourceBusinessDate { get; init; }
    public DateOnly? PostingDate { get; init; }
}
public sealed record CashDiscrepancyDefinition(CashDiscrepancyKind Kind, string Name, string RequiredInputs);

public sealed record ReconciliationVerificationTask(long ProposalId, long ExceptionId, bool IsCash, string Discrepancy,
    DateOnly SourceBusinessDate, DateOnly? PostingDate, long? ExecutionTransactionId,
    VerificationStatus Verification, string? VerificationNote, DateTime? VerifiedAtUtc);


public sealed record CashSourceOption(long Id, string Label, long? SessionId = null, decimal? Amount = null,
    long? TransactionId = null, long? DestinationSessionId = null, string? Type = null);
public sealed record CashSourceCatalog(long ExceptionId, long SourceSessionId,
    IReadOnlyList<CashSourceOption> Accounts, IReadOnlyList<CashSourceOption> Transactions,
    IReadOnlyList<CashSourceOption> Movements, IReadOnlyList<CashSourceOption> Sessions, IReadOnlyList<CashSourceOption> Handoffs)
{
    public IReadOnlyList<CashSourceJournalLine> JournalLines { get; init; } = [];
}
public sealed record CashSourceJournalLine(long TransactionId, string GlCode, string Direction, decimal Amount);
public sealed record CashProposalEvidence(CashCorrectionProposalResponse Proposal, CashCorrectionPreview? Preview,
    bool SourceUnchanged, string? Warning)
{
    public IReadOnlyList<CashSourceJournalLine> OriginalJournal { get; init; } = [];
}
public sealed record CashPlannedJournalLine(string GlCode, string Direction, decimal Amount, long? CustomerAccountId = null);
public sealed record CashAccountEffect(long AccountId, decimal Before, decimal Change, decimal After,
    decimal BeforeAvailable, decimal AfterAvailable);
public sealed record EndOfDayAuditFinding(string Kind, long? SourceId, decimal? Debit, decimal? Credit, string Detail);
public sealed record EndOfDayAuditReport(long BatchId, DateOnly BusinessDate, string Status, decimal RawDebit,
    decimal RawCredit, decimal? EffectiveDebit, decimal? EffectiveCredit, bool? IsReconciled,
    IReadOnlyList<EndOfDayAuditFinding> Findings);
