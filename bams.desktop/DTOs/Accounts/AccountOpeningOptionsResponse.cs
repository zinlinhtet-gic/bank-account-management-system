namespace bams.desktop.DTOs.Accounts;

public sealed record AccountOpeningOptionsResponse(
    IReadOnlyList<AccountTypeResponse> AccountTypes,
    IReadOnlyList<AccountTypeRequiredDocumentResponse> RequiredDocuments,
    IReadOnlyList<OwnedAccountOptionResponse> PayoutAccounts,
    IReadOnlyList<ReusableCustomerDocumentResponse> ReusableDocuments);

/// <summary>Describes a server-selected customer file available for copying during account opening.</summary>
public sealed record ReusableCustomerDocumentResponse(long Id, long CustomerId, string HolderName,
    string DocumentType, string? DocumentNumber, DateOnly? ExpiryDate);
