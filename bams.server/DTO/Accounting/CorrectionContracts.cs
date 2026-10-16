using bams.server.Models.Accounting;
using bams.server.Models.Transactions;

namespace bams.server.DTO.Accounting;

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
