namespace bams.server.DTO.Accounts;

public sealed record CreateAccountRequest(
    long AccountTypeId,
    decimal OpeningBalance,
    bool IsSharedAccount,
    string? HolderNRC1,
    string? HolderNRC2,
    decimal? OwnershipPercentage1,
    decimal? OwnershipPercentage2,
    string? SigningRule,
    List<AccountDocumentUploadRequest>? Documents
    );
