using bams.server.Models.Customers;
using bams.server.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

public sealed class ProductSeeder
{
    private const string ActiveStatus = "Active";

    private static readonly DocumentType[] RequiredDocumentTypes =
    [
        DocumentType.Nrc,
        DocumentType.Photo,
        DocumentType.ProofOfAddress,
        DocumentType.HouseholdRegistration,
        DocumentType.SourceOfFunds
    ];

    private readonly ApplicationDbContext _dbContext;

    public ProductSeeder(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Creates missing account products and their required document definitions.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var products = CreateProducts();
        var existingCodes = await _dbContext.AccountTypes
            .Select(accountType => accountType.Code)
            .ToHashSetAsync(cancellationToken);

        var missingProducts = products
            .Where(product => !existingCodes.Contains(product.Code))
            .ToList();
        if (missingProducts.Count > 0)
        {
            await _dbContext.AccountTypes.AddRangeAsync(missingProducts, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var productCodes = products.Select(product => product.Code).ToArray();
        var persistedProducts = await _dbContext.AccountTypes
            .Where(accountType => productCodes.Contains(accountType.Code))
            .Select(accountType => new { accountType.Id, accountType.Code })
            .ToListAsync(cancellationToken);
        var persistedProductIds = persistedProducts
            .Select(product => product.Id)
            .ToArray();
        var existingRequirements = await _dbContext.AccountTypeRequiredDocuments
            .Where(requirement => persistedProductIds.Contains(requirement.AccountTypeId))
            .Select(requirement => new { requirement.AccountTypeId, requirement.DocumentType })
            .ToListAsync(cancellationToken);

        var requirementKeys = existingRequirements
            .Select(requirement => (requirement.AccountTypeId, requirement.DocumentType))
            .ToHashSet();
        var missingRequirements = persistedProducts
            .SelectMany(product => RequiredDocumentTypes.Select(documentType =>
                new AccountTypeRequiredDocument
                {
                    AccountTypeId = product.Id,
                    DocumentType = documentType
                }))
            .Where(requirement => !requirementKeys.Contains(
                (requirement.AccountTypeId, requirement.DocumentType)))
            .ToList();

        if (missingRequirements.Count > 0)
        {
            await _dbContext.AccountTypeRequiredDocuments.AddRangeAsync(
                missingRequirements,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await SeedInterestRateRulesAsync(
            persistedProducts.ToDictionary(product => product.Code, product => product.Id),
            cancellationToken);
    }

    // Adds missing demo rates by product, term, and balance band without overwriting configured rules.
    private async Task SeedInterestRateRulesAsync(
        IReadOnlyDictionary<string, long> productIdsByCode,
        CancellationToken cancellationToken)
    {
        var accountTypeIds = productIdsByCode.Values.ToArray();
        var existingRules = await _dbContext.InterestRateRules
            .Where(rule => accountTypeIds.Contains(rule.AccountTypeId))
            .Select(rule => new
            {
                rule.AccountTypeId,
                rule.TermDays,
                rule.TermMonths,
                rule.BalanceMin,
                rule.BalanceMax
            })
            .ToListAsync(cancellationToken);
        var existingKeys = existingRules
            .Select(rule => (rule.AccountTypeId, rule.TermDays, rule.TermMonths, rule.BalanceMin, rule.BalanceMax))
            .ToHashSet();
        var effectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow);
        var missingRules = CreateInterestRateRuleSeeds()
            .Select(seed =>
            {
                var accountTypeId = productIdsByCode[seed.AccountTypeCode];
                var key = (accountTypeId, seed.TermDays, seed.TermMonths, (decimal?)null, (decimal?)null);
                return (Seed: seed, AccountTypeId: accountTypeId, Key: key);
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

        if (missingRules.Count == 0)
        {
            return;
        }

        await _dbContext.InterestRateRules.AddRangeAsync(missingRules, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Keeps the requested demo rates as annual percentages and leaves Current without a rule.
    private static IReadOnlyList<InterestRateRuleSeed> CreateInterestRateRuleSeeds()
    {
        return
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
    }

    // Defines the initial account products independently from EF model configuration.
    private static IReadOnlyList<AccountType> CreateProducts()
    {
        return
        [
            CreateProduct(1, "CURRENT", "Current", "Current", true, false, null),
            CreateProduct(2, "NORMAL_SAVING", "Normal Saving", "Saving", true, false, null),
            CreateProduct(3, "SPECIAL_SAVING", "Special Saving", "Saving", true, false, null),
            CreateProduct(4, "NORMAL_DEPOSIT", "Normal Deposit", "Deposit", false, true, 2),
            CreateProduct(5, "SPECIAL_DEPOSIT", "Special Deposit", "Deposit", false, true, 2),
            CreateProduct(6, "HUNDRED_DAYS_DEPOSIT", "Hundred-Days Deposit", "Deposit", false, true, 2)
        ];
    }

    // Creates neutral configuration for a seeded account product.
    private static AccountType CreateProduct(
        long id,
        string code,
        string name,
        string category,
        bool allowsTransactions,
        bool isFixedDeposit,
        long? requiredProductId)
    {
        return new AccountType
        {
            Id = id,
            Code = code,
            Name = name,
            Category = category,
            MinimumOpeningBalance = 0m,
            MinimumMaintainedBalance = 0m,
            DailyTransactionLimit = null,
            MonthlyTransactionLimit = null,
            AllowWithdrawal = allowsTransactions,
            AllowTransfer = allowsTransactions,
            AllowPartialWithdrawal = allowsTransactions,
            IsFixedDeposit = isFixedDeposit,
            RequiredProductId = requiredProductId,
            Status = ActiveStatus
        };
    }

    private sealed record InterestRateRuleSeed(
        string AccountTypeCode,
        int? TermDays,
        int? TermMonths,
        decimal AnnualRate);
}
