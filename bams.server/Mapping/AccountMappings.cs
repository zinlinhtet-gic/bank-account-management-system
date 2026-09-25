using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;

namespace bams.server.Mapping;

public static class AccountMappings
{
    // Converts an Account entity (with AccountType loaded) into the detailed API response contract.
    public static AccountResponse ToResponse(this Account account)
    {
        return new AccountResponse(
            account.Id,
            account.AccountNo,
            account.AccountTypeId,
            account.AccountType?.Code ?? string.Empty,
            account.Status,
            account.AvailableBalance,
            account.LedgerBalance,
            account.OpenedAt,
            account.CreatedAt,
            account.Version);
    }
}
