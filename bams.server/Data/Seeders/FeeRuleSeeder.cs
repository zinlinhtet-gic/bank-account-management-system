using bams.server.Data;
using bams.server.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

/// <summary>Seeds monthly maintenance and dormant-account demo fees for saving products.</summary>
public sealed class FeeRuleSeeder(ApplicationDbContext dbContext)
{
    private const string ActiveStatus = "Active";

    public async Task SeedAsync(IReadOnlyList<(long Id, AccountTypeCategory Category)> products,
        CancellationToken cancellationToken = default)
    {
        var savingProducts = products.Where(product => product.Category == AccountTypeCategory.SAVING).ToArray();
        var savingProductIds = savingProducts.Select(product => product.Id).ToArray();
        var existingRules = await dbContext.FeeRules
            .Where(rule => savingProductIds.Contains(rule.AccountTypeId) &&
                (rule.FeeType == FeeType.Maintenance || rule.FeeType == FeeType.DormantAccount))
            .ToListAsync(cancellationToken);
        var existingKeys = existingRules.Select(rule => (rule.AccountTypeId, rule.FeeType)).ToHashSet();
        // Seeded demo fees cover historical periods used by scheduled operations.
        var effectiveFrom = new DateOnly(2000, 1, 1);
        var missingRules = savingProducts.SelectMany(product => new[]
            {
                (Type: FeeType.Maintenance, Amount: 1_000m),
                (Type: FeeType.DormantAccount, Amount: 5_000m)
            }
            .Where(rule => !existingKeys.Contains((product.Id, rule.Type)))
            .Select(rule => new FeeRule
            {
                AccountTypeId = product.Id,
                FeeType = rule.Type,
                Amount = rule.Amount,
                EffectiveFrom = effectiveFrom,
                Status = ActiveStatus
            }))
            .ToArray();

        // Repair the exact demo rows created by the previous startup seeder version.
        foreach (var existingRule in existingRules)
        {
            var seededAmount = existingRule.FeeType switch
            {
                FeeType.Maintenance => 1_000m,
                FeeType.DormantAccount => 5_000m,
                _ => (decimal?)null
            };
            if (existingRule.Amount == seededAmount && existingRule.Status == ActiveStatus &&
                !existingRule.EffectiveTo.HasValue && existingRule.EffectiveFrom > effectiveFrom)
            {
                existingRule.EffectiveFrom = effectiveFrom;
            }
        }

        if (missingRules.Length > 0)
            await dbContext.FeeRules.AddRangeAsync(missingRules, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
