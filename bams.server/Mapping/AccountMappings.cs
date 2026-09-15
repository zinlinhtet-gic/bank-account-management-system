using bams.server.DTO.Accounts;
using bams.server.Models;

namespace bams.server.Mapping;

public static class AccountMappings
{
    // Converts an Account entity into the detailed API response contract.
    public static AccountResponse ToResponse(this Account account)
    {
        return new AccountResponse(
            account.Id,
            account.AccountNumber,
            account.Name,
            account.Type,
            account.Status,
            account.Balance,
            account.CreatedAtUtc);
    }

    // Converts an Account entity into the lightweight API summary contract.
    public static AccountSummaryResponse ToSummaryResponse(this Account account)
    {
        return new AccountSummaryResponse(
            account.Id,
            account.AccountNumber,
            account.Name,
            account.Type,
            account.Status,
            account.Balance);
    }
}
