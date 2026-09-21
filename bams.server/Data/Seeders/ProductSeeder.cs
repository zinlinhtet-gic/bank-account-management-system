using bams.server.Models.Customers;
using bams.server.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

public sealed class ProductSeeder
{
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
    }

    // Defines the initial account products independently from EF model configuration.
    private static IReadOnlyList<AccountType> CreateProducts()
    {
        return
        [
            CreateProduct(1, "CURRENT", "Current", "Current", true),
            CreateProduct(2, "NORMAL_SAVING", "Normal Saving", "Saving", true),
            CreateProduct(3, "SPECIAL_SAVING", "Special Saving", "Saving", true),
            CreateProduct(4, "NORMAL_DEPOSIT", "Normal Deposit", "Deposit", false),
            CreateProduct(5, "SPECIAL_DEPOSIT", "Special Deposit", "Deposit", false),
            CreateProduct(6, "HUNDRED_DAYS_DEPOSIT", "Hundred-Days Deposit", "Deposit", false)
        ];
    }

    // Creates neutral configuration for a seeded account product.
    private static AccountType CreateProduct(
        long id,
        string code,
        string name,
        string category,
        bool allowsTransactions)
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
            Status = "Active"
        };
    }
}
