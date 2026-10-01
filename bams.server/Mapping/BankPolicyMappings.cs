using bams.server.DTO.Configuration;
using bams.server.Models.Products;

namespace bams.server.Mapping;

public static class BankPolicyMappings
{
    // Converts an AccountType entity into the bank policy API response contract.
    public static BankPolicyResponse ToResponse(this AccountType accountType)
    {
        return new BankPolicyResponse(
            accountType.Id,
            accountType.Code,
            accountType.Name,
            accountType.Category,
            accountType.MinimumOpeningBalance,
            accountType.MinimumMaintainedBalance,
            accountType.DailyTransactionLimit,
            accountType.MonthlyTransactionLimit,
            accountType.AllowWithdrawal,
            accountType.AllowTransfer,
            accountType.AllowPartialWithdrawal,
            accountType.AllowCitizen,
            accountType.AllowForeigner,
            accountType.CitizenRequiredRefer,
            accountType.ForeignRequiredRefer,
            accountType.Status);
    }
}
