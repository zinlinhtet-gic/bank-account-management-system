namespace bams.server.Models.Accounting;

public enum DiscrepancyKind
{
    MissingGlPosting = 1, MissingAccountPosting, IncorrectAmount, WrongCustomerAccount,
    WrongGlAccount, DuplicatePosting, WrongDirection, PostingDateMismatch,
    IncompleteJournal, BalanceSnapshot, OpeningBalance
}

public enum ProposalStatus { Draft, PendingApproval, Rejected, Superseded, Executed }
public enum VerificationStatus { Pending, Passed, Failed }

/// <summary>Versioned investigation proposal; submitted inputs and evidence are immutable.</summary>
public sealed class CorrectionProposal
{
    public long Id { get; set; }
    public long ExceptionId { get; set; }
    public ReconciliationException? Exception { get; set; }
    public DiscrepancyKind Kind { get; set; }
    public ProposalStatus Status { get; set; }
    public VerificationStatus Verification { get; set; }
    public long ProposerId { get; set; }
    public int Revision { get; set; }
    public string InputsJson { get; set; } = "";
    public string PreviewJson { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public long? ReviewerId { get; set; }
    public string? ReviewNote { get; set; }
    public bool SoleManagerApproval { get; set; }
    public long? ExecutionTransactionId { get; set; }
    public string? VerificationNote { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}

/// <summary>Append-only effective balance calculation for an immutable account entry.</summary>
public sealed class EffectiveAccountBalance
{
    public string ProposalSource { get; set; } = "Account";
    public long Id { get; set; }
    public long AccountId { get; set; }
    public long AccountTransactionId { get; set; }
    public long ProposalId { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
}

/// <summary>Retains provisional summary revisions without overwriting finalized daily summaries.</summary>
public sealed class DailySummaryRevision
{
    public long Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    public int Revision { get; set; }
    public bool IsFinal { get; set; }
    public bool IsStale { get; set; }
    public string LinesJson { get; set; } = "[]";
    public string ExceptionsJson { get; set; } = "[]";
    public DateTime GeneratedAtUtc { get; set; }
}
