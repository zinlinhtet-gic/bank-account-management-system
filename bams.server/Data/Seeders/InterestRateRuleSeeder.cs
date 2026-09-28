using bams.server.Data;
using bams.server.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

/// <summary>Seeds demo annual interest rules without replacing configured product rates.</summary>
public sealed class InterestRateRuleSeeder(ApplicationDbContext dbContext)
{
    private const string ActiveStatus = "Active";

    public async Task SeedAsync(IReadOnlyDictionary<string, long> productIdsByCode,
        CancellationToken cancellationToken = default)
    {
        var accountTypeIds = productIdsByCode.Values.ToArray();
        var existingRules = await dbContext.InterestRateRules
            .Where(rule => accountTypeIds.Contains(rule.AccountTypeId))
            .ToListAsync(cancellationToken);
        var existingKeys = existingRules
            .Select(rule => (rule.AccountTypeId, rule.TermDays, rule.TermMonths, rule.BalanceMin, rule.BalanceMax))
            .ToHashSet();
        // Seeded demo rates cover historical periods used by scheduled accrual jobs.
        var effectiveFrom = new DateOnly(2000, 1, 1);
        var seeds = CreateSeeds();
        var missingRules = seeds
            .Select(seed =>
            {
                var accountTypeId = productIdsByCode[seed.AccountTypeCode];
                return (Seed: seed, AccountTypeId: accountTypeId,
                    Key: (accountTypeId, seed.TermDays, seed.TermMonths, (decimal?)null, (decimal?)null));
            })
            .Where(item => !existingKeys.Contains(item.Key))
            .Select(item => new InterestRateRule
            {
                AccountTypeId = item.AccountTypeId,
                TermDays = item.Seed.TermDays,
                TermMonths = item.Seed.TermMonths,
                AnnualRate = item.Seed.AnnualRate,
                EffectiveFrom = effectiveFrom,
                Status = ActiveStatus
            })
            .ToList();

        // Older versions seeded these exact demo rows effective only from startup day. Backdate those rows
        // so a restart repairs the previous-month rule lookup that triggered the scheduled-job failures.
        foreach (var existingRule in existingRules)
        {
            var seed = seeds.FirstOrDefault(item =>
                productIdsByCode[item.AccountTypeCode] == existingRule.AccountTypeId &&
                item.TermDays == existingRule.TermDays && item.TermMonths == existingRule.TermMonths &&
                existingRule.BalanceMin is null && existingRule.BalanceMax is null &&
                item.AnnualRate == existingRule.AnnualRate);
            if (seed is not null && existingRule.Status == ActiveStatus &&
                !existingRule.EffectiveTo.HasValue && existingRule.EffectiveFrom > effectiveFrom)
            {
                existingRule.EffectiveFrom = effectiveFrom;
            }
        }

        if (missingRules.Count > 0)
            await dbContext.InterestRateRules.AddRangeAsync(missingRules, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<InterestRateRuleSeed> CreateSeeds() =>
    [
        new("NORMAL_SAVING", null, null, 6.5m),
        new("SPECIAL_SAVING", null, null, 8m),
        new("NORMAL_DEPOSIT", null, 1, 8.5m),
        new("NORMAL_DEPOSIT", null, 3, 9m),
        new("NORMAL_DEPOSIT", null, 6, 9.5m),
        new("NORMAL_DEPOSIT", null, 12, 10m),
        new("SPECIAL_DEPOSIT", null, 1, 9m),
        new("SPECIAL_DEPOSIT", null, 3, 9.5m),
        new("SPECIAL_DEPOSIT", null, 6, 10m),
        new("SPECIAL_DEPOSIT", null, 12, 10.5m),
        new("HUNDRED_DAYS_DEPOSIT", 100, null, 9.25m)
    ];

    private sealed record InterestRateRuleSeed(string AccountTypeCode, int? TermDays, int? TermMonths, decimal AnnualRate);
}
