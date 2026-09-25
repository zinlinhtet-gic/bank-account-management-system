namespace bams.desktop.DTOs.Accounts;

public sealed record CreateAccountCommand(
    long AccountTypeId,
    decimal OpeningBalance,
    bool IsSharedAccount,
    string? HolderNrc1,
    string? HolderNrc2,
    decimal? OwnershipPercentage1,
    decimal? OwnershipPercentage2,
    string? SigningRule,
    IReadOnlyList<AccountDocumentUpload> Documents,
    long? PayoutAccountId,
    long? InterestRateRuleId,
    string? RenewalInstruction,
    bool? CalculateFromCurrent,
    IReadOnlyList<string> RefererNrcs);
