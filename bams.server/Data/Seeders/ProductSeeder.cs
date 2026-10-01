using bams.server.Models.Customers;
using bams.server.Models.Accounting;
using bams.server.Models.Products;
using bams.server.Models.Security;
using bams.server.Utils.Security;
using bams.server.Constants;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

public sealed class ProductSeeder
{
    private const string ActiveStatus = "Active";
    private const string NormalSavingProductCode = "NORMAL_SAVING";

    private static readonly DocumentType[] RequiredDocumentTypes =
    [
        DocumentType.Nrc,
        DocumentType.Photo,
        DocumentType.ProofOfAddress,
        DocumentType.HouseholdRegistration,
        DocumentType.SourceOfFunds
    ];

    private readonly ApplicationDbContext _dbContext;
    private readonly InterestRateRuleSeeder _interestRateRuleSeeder;
    private readonly FeeRuleSeeder _feeRuleSeeder;

    public ProductSeeder(ApplicationDbContext dbContext,
        InterestRateRuleSeeder interestRateRuleSeeder, FeeRuleSeeder feeRuleSeeder)
    {
        _dbContext = dbContext;
        _interestRateRuleSeeder = interestRateRuleSeeder;
        _feeRuleSeeder = feeRuleSeeder;
    }

    /// <summary>
    /// Creates missing account products and their required document definitions.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var productSeeds = CreateProducts();
        var existingProductIdsByCode = await _dbContext.AccountTypes
            .ToDictionaryAsync(accountType => accountType.Code, accountType => accountType.Id, cancellationToken);

        var missingSeeds = productSeeds
            .Where(seed => !existingProductIdsByCode.ContainsKey(seed.Product.Code))
            .ToList();
        if (missingSeeds.Count > 0)
        {
            LinkRequiredProducts(missingSeeds, existingProductIdsByCode);
            await _dbContext.AccountTypes.AddRangeAsync(missingSeeds.Select(seed => seed.Product), cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var productCodes = productSeeds.Select(seed => seed.Product.Code).ToArray();
        var persistedProducts = await _dbContext.AccountTypes
            .Where(accountType => productCodes.Contains(accountType.Code))
            .Select(accountType => new PersistedProduct(accountType.Id, accountType.Code, accountType.Category))
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

        await _interestRateRuleSeeder.SeedAsync(
            persistedProducts.ToDictionary(product => product.Code, product => product.Id),
            cancellationToken);
        await _feeRuleSeeder.SeedAsync(
            persistedProducts.Select(product => (product.Id, product.Category)).ToArray(),
            cancellationToken);
        await SeedScheduledOperationLedgerAccountsAsync(cancellationToken);
        await SeedScheduledJobActorAsync(cancellationToken);
    }

    private async Task SeedScheduledOperationLedgerAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = new[]
        {
            // Customer Deposits is seeded by ChartOfAccountsSeeder and shared with teller postings.
            new GlAccount { Code = AccountingConstants.MaintenanceFeeReceivableGlCode, Name = "Maintenance Fee Receivable", AccountClass = GlAccountClass.Asset, Status = AccountingConstants.ActiveGlAccountStatus },
            new GlAccount { Code = AccountingConstants.DormantPenaltyReceivableGlCode, Name = "Dormant Penalty Receivable", AccountClass = GlAccountClass.Asset, Status = AccountingConstants.ActiveGlAccountStatus },
            new GlAccount { Code = AccountingConstants.InterestPayableGlCode, Name = "Interest Payable", AccountClass = GlAccountClass.Liability, Status = AccountingConstants.ActiveGlAccountStatus },
            new GlAccount { Code = AccountingConstants.InterestExpenseGlCode, Name = "Interest Expense", AccountClass = GlAccountClass.Expense, Status = AccountingConstants.ActiveGlAccountStatus },
            new GlAccount { Code = AccountingConstants.MaintenanceFeeIncomeGlCode, Name = "Maintenance Fee Income", AccountClass = GlAccountClass.Income, Status = AccountingConstants.ActiveGlAccountStatus },
            new GlAccount { Code = AccountingConstants.DormantPenaltyIncomeGlCode, Name = "Dormant Account Penalty Income", AccountClass = GlAccountClass.Income, Status = AccountingConstants.ActiveGlAccountStatus }
        };
        var codes = accounts.Select(account => account.Code).ToArray();
        var existingCodes = await _dbContext.GlAccounts
            .Where(account => codes.Contains(account.Code))
            .Select(account => account.Code)
            .ToHashSetAsync(cancellationToken);
        var missingAccounts = accounts.Where(account => !existingCodes.Contains(account.Code)).ToArray();
        if (missingAccounts.Length > 0)
        {
            await _dbContext.GlAccounts.AddRangeAsync(missingAccounts, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedScheduledJobActorAsync(CancellationToken cancellationToken)
    {
        var username = ScheduledJobConstants.SystemActorUsername;
        var existingActor = await _dbContext.Users.FirstOrDefaultAsync(user => user.Username == username, cancellationToken);
        if (existingActor is not null)
        {
            // Scheduled postings only accept a Disabled actor (it must never sign in), so repair any other status.
            if (existingActor.Status != UserStatus.Disabled)
            {
                existingActor.Status = UserStatus.Disabled;
                existingActor.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var now = DateTime.UtcNow;
        _dbContext.Users.Add(new User
        {
            Username = username,
            Email = "system-scheduled-jobs@invalid.local",
            FullName = "Scheduled Jobs",
            PasswordHash = PasswordHasher.HashPassword(Guid.NewGuid().ToString("N")),
            Status = UserStatus.Disabled,
            OnlineStatus = OnlineStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Defines the initial account products independently from EF model configuration. Ids are left to the
    // database so seeding never collides with rows that already exist; prerequisites are linked by product code.
    private static IReadOnlyList<ProductSeed> CreateProducts()
    {
        return
        [
            new(CreateProduct("CURRENT", "Current", AccountTypeCategory.CURRENT, true), null),
            new(CreateProduct(NormalSavingProductCode, "Normal Saving", AccountTypeCategory.SAVING, true), null),
            new(CreateProduct("SPECIAL_SAVING", "Special Saving", AccountTypeCategory.SAVING, true), null),
            new(CreateProduct("NORMAL_DEPOSIT", "Normal Deposit", AccountTypeCategory.FIXED, false), NormalSavingProductCode),
            new(CreateProduct("SPECIAL_DEPOSIT", "Special Deposit", AccountTypeCategory.FIXED, false), NormalSavingProductCode),
            new(CreateProduct("HUNDRED_DAYS_DEPOSIT", "Hundred-Days Deposit", AccountTypeCategory.FIXED, false), NormalSavingProductCode)
        ];
    }

    // Points each new product at its prerequisite: a product inserted in the same batch is linked through the
    // navigation (EF orders the inserts), an already persisted one through its database id.
    private static void LinkRequiredProducts(
        IReadOnlyList<ProductSeed> missingSeeds,
        IReadOnlyDictionary<string, long> existingProductIdsByCode)
    {
        var newProductsByCode = missingSeeds.ToDictionary(seed => seed.Product.Code, seed => seed.Product);

        foreach (var seed in missingSeeds.Where(seed => seed.RequiredProductCode is not null))
        {
            var requiredCode = seed.RequiredProductCode!;
            if (newProductsByCode.TryGetValue(requiredCode, out var requiredProduct))
            {
                seed.Product.RequiredProduct = requiredProduct;
            }
            else if (existingProductIdsByCode.TryGetValue(requiredCode, out var requiredProductId))
            {
                seed.Product.RequiredProductId = requiredProductId;
            }
            else
            {
                throw new InvalidOperationException($"Seeded product '{seed.Product.Code}' requires unknown product '{requiredCode}'.");
            }
        }
    }

    // Creates neutral configuration for a seeded account product.
    private static AccountType CreateProduct(
        string code,
        string name,
        AccountTypeCategory category,
        bool allowsTransactions)
    {
        return new AccountType
        {
            Code = code,
            Name = name,
            Category = category,
            MinimumOpeningBalance = 0m,
            MinimumMaintainedBalance = 0m,
            DailyTransactionLimit = null,
            MonthlyTransactionLimit = null,
            AllowDeposit = allowsTransactions,
            AllowWithdrawal = allowsTransactions,
            AllowTransfer = allowsTransactions,
            AllowPartialWithdrawal = allowsTransactions,
            AllowCitizen = true,
            AllowForeigner = true,
            CitizenRequiredRefer = 0,
            ForeignRequiredRefer = 0,
            Status = ActiveStatus
        };
    }

    // A seeded product and the code of the product a customer must already hold to open it.
    private sealed record ProductSeed(AccountType Product, string? RequiredProductCode);

    private sealed record PersistedProduct(long Id, string Code, AccountTypeCategory Category);
}
