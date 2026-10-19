
using bams.desktop.DTOs.Transactions;

namespace bams.desktop.DTOs.Accounting;

/// <summary>Investigator evidence and case-specific parameters; posting lines are server generated.</summary>
public sealed record CorrectionInputs(
    DiscrepancyKind Kind, long TransactionId, string Findings, string EvidenceReference,
    long? AccountEntryId = null, long? GlEntryId = null, long? CorrectAccountId = null,
    long? CorrectGlAccountId = null, long? OffsetGlAccountId = null,
    decimal? CorrectAmount = null, DateOnly? CorrectPostingDate = null,
    EntryType? MissingEntryDirection = null, decimal? MissingEntryAmount = null,
    string? ExternalRecoveryReference = null);

public sealed record CorrectionLine(string Ledger, long AccountId, long? CustomerAccountId,
    EntryType Direction, decimal Amount, DateOnly PostingDate);
public sealed record CorrectionBalanceImpact(long AccountId, decimal Before, decimal Change, decimal After);
public sealed record CorrectionPreview(DiscrepancyKind Kind, DateOnly PostingDate, decimal SuggestedAmount,
    string Explanation, string SourceHash, IReadOnlyList<CorrectionLine> Lines,
    IReadOnlyList<CorrectionBalanceImpact> Balances, bool IsJournalRepair, bool IsBalanceRepair);
public sealed record SaveCorrectionRequest(CorrectionInputs Inputs, bool Submit, string? ExpectedSourceHash = null);
public sealed record ReviewCorrectionRequest(bool Approve, string Note, string? RecoveryAttestation = null);
public sealed record CorrectionProposalResponse(long Id, long ExceptionId, int Revision, DiscrepancyKind Kind,
    ProposalStatus Status, VerificationStatus Verification, long ProposerId, long? ReviewerId,
    bool SoleManagerApproval, CorrectionInputs Inputs, CorrectionPreview? Preview,
    long? ExecutionTransactionId, string? ReviewNote, string? VerificationNote, DateTime CreatedAtUtc);
public sealed record InvestigationRequest(long InvestigatorId, string Findings, DiscrepancyKind? Kind, DateOnly? ReviewDueDate);
public sealed record DiscrepancyDefinition(DiscrepancyKind Kind, string Name, string RequiredInputs);
public sealed record SummaryRevisionResponse(long Id, DateOnly BusinessDate, int Revision, bool IsFinal,
    bool IsStale, string LinesJson, string ExceptionsJson, DateTime GeneratedAtUtc);

public sealed record CorrectionGlOption(long Id, string Code, string Name);

public enum DiscrepancyKind { MissingGlPosting = 1, MissingAccountPosting, IncorrectAmount, WrongCustomerAccount, WrongGlAccount, DuplicatePosting, WrongDirection, PostingDateMismatch, IncompleteJournal, BalanceSnapshot, OpeningBalance }
public enum ProposalStatus { Draft, PendingApproval, Rejected, Superseded, Executed }
public enum VerificationStatus { Pending, Passed, Failed }
public sealed record CorrectionAccountEvidence(long Id, long AccountId, EntryType EntryType, decimal Amount, DateOnly PostingDate, decimal LedgerBalanceBefore, decimal LedgerBalanceAfter)
{ public string Label => $"Entry #{Id} / Account #{AccountId}: {EntryType} {Amount:N2} ({PostingDate})"; }
public sealed record CorrectionJournalEvidence(long Id, long GlAccountId, long? CustomerAccountId, EntryType EntryType, decimal Amount, DateOnly PostingDate)
{ public string Label => $"Entry #{Id} / GL #{GlAccountId}: {EntryType} {Amount:N2} ({PostingDate})"; }
public sealed record CorrectionEvidence(IReadOnlyList<CorrectionAccountEvidence> AccountEntries, IReadOnlyList<CorrectionJournalEvidence> JournalEntries);
