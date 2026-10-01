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
            new GlAccount { Code = "1101", Name = "Maintenance Fee Receivable", AccountClass = GlAccountClass.Asset, Status = ActiveStatus },
            new GlAccount { Code = "1102", Name = "Dormant Penalty Receivable", AccountClass = GlAccountClass.Asset, Status = ActiveStatus },
            new GlAccount { Code = "2001", Name = "Customer Deposit Liabilities", AccountClass = GlAccountClass.Liability, Status = ActiveStatus },
            new GlAccount { Code = "2101", Name = "Interest Payable", AccountClass = GlAccountClass.Liability, Status = ActiveStatus },
            new GlAccount { Code = "6001", Name = "Interest Expense", AccountClass = GlAccountClass.Expense, Status = ActiveStatus },
            new GlAccount { Code = "4001", Name = "Maintenance Fee Income", AccountClass = GlAccountClass.Income, Status = ActiveStatus },
            new GlAccount { Code = "4002", Name = "Dormant Account Penalty Income", AccountClass = GlAccountClass.Income, Status = ActiveStatus }
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
        if (await _dbContext.Users.AnyAsync(user => user.Username == username, cancellationToken))
        {
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

    // Defines the initial account products independently from EF model configuration.
    private static IReadOnlyList<AccountType> CreateProducts()
    {
        return
        [
            CreateProduct(1, "CURRENT", "Current", AccountTypeCategory.CURRENT, true, null),
            CreateProduct(2, "NORMAL_SAVING", "Normal Saving", AccountTypeCategory.SAVING, true, null),
            CreateProduct(3, "SPECIAL_SAVING", "Special Saving", AccountTypeCategory.SAVING, true, null),
            CreateProduct(4, "NORMAL_DEPOSIT", "Normal Deposit", AccountTypeCategory.FIXED, false, 2),
            CreateProduct(5, "SPECIAL_DEPOSIT", "Special Deposit", AccountTypeCategory.FIXED, false, 2),
            CreateProduct(6, "HUNDRED_DAYS_DEPOSIT", "Hundred-Days Deposit", AccountTypeCategory.FIXED, false, 2)
        ];
    }

    // Creates neutral configuration for a seeded account product.
    private static AccountType CreateProduct(
        long id,
        string code,
        string name,
        AccountTypeCategory category,
        bool allowsTransactions,
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
            AllowCitizen = true,
            AllowForeigner = true,
            CitizenRequiredRefer = 0,
            ForeignRequiredRefer = 0,
            RequiredProductId = requiredProductId,
            Status = ActiveStatus
        };
    }

    private sealed record PersistedProduct(long Id, string Code, AccountTypeCategory Category);
}
