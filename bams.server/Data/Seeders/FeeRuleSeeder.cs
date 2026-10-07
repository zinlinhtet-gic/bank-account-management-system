using bams.server.Data;
using bams.server.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

/// <summary>
/// Seeds the bank-policy fees of current and saving products: monthly maintenance (0 MMK), monthly dormant-account
/// penalty (3,000 MMK), transfer fee to another customer's account in this bank (0.2%) and interbank transfer fee
/// (0.5%). Fixed products take no teller transactions and are charged no fees.
/// </summary>
public sealed class FeeRuleSeeder(ApplicationDbContext dbContext)
{
    private const string ActiveStatus = "Active";

    private static readonly AccountTypeCategory[] FeeChargingCategories =
    [
        AccountTypeCategory.CURRENT,
        AccountTypeCategory.SAVING
    ];

    // Policy fees: fixed monthly amounts in MMK, transfer fees as a percentage of the transferred amount.
    private static readonly SeededFee[] PolicyFees =
    [
        new(FeeType.Maintenance, Amount: 0m, Percentage: null, PreviousDemoAmount: 1_000m),
        new(FeeType.DormantAccount, Amount: 3_000m, Percentage: null, PreviousDemoAmount: 5_000m),
        new(FeeType.Transfer, Amount: null, Percentage: 0.2m, PreviousDemoAmount: null),
        new(FeeType.InterbankTransfer, Amount: null, Percentage: 0.5m, PreviousDemoAmount: null)
    ];

    public async Task SeedAsync(IReadOnlyList<(long Id, AccountTypeCategory Category)> products,
        CancellationToken cancellationToken = default)
    {
        var productIds = products
            .Where(product => FeeChargingCategories.Contains(product.Category))
            .Select(product => product.Id)
            .ToArray();
        var feeTypes = PolicyFees.Select(fee => fee.FeeType).ToArray();
        var existingRules = await dbContext.FeeRules
            .Where(rule => productIds.Contains(rule.AccountTypeId) && feeTypes.Contains(rule.FeeType))
            .ToListAsync(cancellationToken);
        var existingKeys = existingRules.Select(rule => (rule.AccountTypeId, rule.FeeType)).ToHashSet();

        // Seeded fees cover historical periods used by scheduled operations.
        var effectiveFrom = new DateOnly(2000, 1, 1);
        var missingRules = productIds.SelectMany(productId => PolicyFees
            .Where(fee => !existingKeys.Contains((productId, fee.FeeType)))
            .Select(fee => new FeeRule
            {
                AccountTypeId = productId,
                FeeType = fee.FeeType,
                Amount = fee.Amount,
                Percentage = fee.Percentage,
                EffectiveFrom = effectiveFrom,
                Status = ActiveStatus
            }))
            .ToArray();

        RepairPreviousDemoRules(existingRules, effectiveFrom);

        if (missingRules.Length > 0)
            await dbContext.FeeRules.AddRangeAsync(missingRules, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Moves the exact demo rows created by earlier seeder versions (1,000 MMK maintenance, 5,000 MMK dormant penalty)
    // onto the bank-policy amounts. A rule an administrator changed no longer matches and is left alone.
    private static void RepairPreviousDemoRules(IEnumerable<FeeRule> existingRules, DateOnly effectiveFrom)
    {
        foreach (var existingRule in existingRules)
        {
            var policyFee = PolicyFees.First(fee => fee.FeeType == existingRule.FeeType);
            if (policyFee.PreviousDemoAmount is null || existingRule.Amount != policyFee.PreviousDemoAmount ||
                existingRule.Percentage.HasValue || existingRule.Status != ActiveStatus ||
                existingRule.EffectiveTo.HasValue)
            {
                continue;
            }

            existingRule.Amount = policyFee.Amount;
            if (existingRule.EffectiveFrom > effectiveFrom)
            {
                existingRule.EffectiveFrom = effectiveFrom;
            }
        }
    }

    // One fee a policy charges; PreviousDemoAmount is what an earlier seeder version stored for it.
    private sealed record SeededFee(FeeType FeeType, decimal? Amount, decimal? Percentage, decimal? PreviousDemoAmount);
}
