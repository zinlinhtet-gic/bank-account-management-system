namespace bams.desktop.DTOs.Accounts;

public sealed record AccountOpeningOptionsResponse(
    IReadOnlyList<AccountTypeResponse> AccountTypes,
    IReadOnlyList<AccountTypeRequiredDocumentResponse> RequiredDocuments,
    IReadOnlyList<OwnedAccountOptionResponse> OwnedAccounts);
