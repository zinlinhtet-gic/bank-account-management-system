using bams.server.Models.Transactions;

namespace bams.server.Models.Accounting;

public enum CashDiscrepancyKind
{
    CountEntryError = 1,
    UnrecordedDeposit,
    UnrecordedWithdrawal,
    MissingMovement,
    DuplicateMovement,
    IncorrectMovementAmount,
    WrongCashPosition,
    TransferMismatch,
    HandoffMismatch,
    UnexplainedOverShort,
    BankExpenseNotRecorded
}

/// <summary>An approved cash investigation is executed at most once.</summary>
public sealed class CashCorrectionProposal
{
    public long Id { get; set; }
    public long ExceptionId { get; set; }
    public ReconciliationException? Exception { get; set; }
    public CashDiscrepancyKind Kind { get; set; }
    public ProposalStatus Status { get; set; }
    public VerificationStatus Verification { get; set; }
    public long ProposerId { get; set; }
    public long? ReviewerId { get; set; }
    public string InputsJson { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string? ReviewNote { get; set; }
    public string? VerificationNote { get; set; }
    public long? ExecutionTransactionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
}

/// <summary>Audited exclusion of an exact reconciliation source snapshot.</summary>
public sealed class ReconciliationExclusion
{
    public long Id { get; set; }
    public long ExceptionId { get; set; }
    public ReconciliationException? Exception { get; set; }
    public string SourceHash { get; set; } = "";
    public string SourceIdsJson { get; set; } = "[]";
    public string Reason { get; set; } = "";
    public long ManagerId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<ReconciliationExcludedSource> Sources { get; set; } = [];
}

public sealed class ReconciliationExcludedSource
{
    public long Id { get; set; }
    public long ExclusionId { get; set; }
    public ReconciliationExclusion? Exclusion { get; set; }
    public string SourceType { get; set; } = "";
    public long SourceId { get; set; }
}

/// <summary>Receipt and review record for bank operating cash already spent.</summary>
public sealed class BankExpenseRequest
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public CashPositionSession? Session { get; set; }
    public long? ExceptionId { get; set; }
    public long? CashProposalId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public decimal Amount { get; set; }
    public string Payee { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string? ReceiptFileName { get; set; }
    public string? ReceiptContentType { get; set; }
    public byte[]? ReceiptData { get; set; }
    public string? MissingReceiptReason { get; set; }
    public string Status { get; set; } = "PendingApproval";
    public long RequestedBy { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public long? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
    public long? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
}
