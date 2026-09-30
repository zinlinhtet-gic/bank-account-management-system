using bams.server.DTO.Products;
using bams.server.Models.Accounts;
using bams.server.Models.Customers;
using bams.server.Models.Products;

namespace bams.server.Services.Interfaces;

public interface IAccountTypeService
{
    Task<IReadOnlyList<Account>> GetAccountsByCategoryAsync(
        AccountTypeCategory category,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetAccountsNotInCategoryAsync(
        AccountTypeCategory category,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetAccountsByCategoryPageAsync(
        AccountTypeCategory category,
        long? afterAccountId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetAccountsNotInCategoryPageAsync(
        AccountTypeCategory category,
        long? afterAccountId,
        int pageSize,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IReadOnlyList<Account>> GetAccountBatchesByCategoryAsync(
        AccountTypeCategory category,
        int batchSize,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IReadOnlyList<Account>> GetAccountBatchesNotInCategoryAsync(
        AccountTypeCategory category,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active account products available for account opening.
    /// </summary>
    Task<IReadOnlyList<AccountTypeResponse>> GetAvailableAccountTypesAsync(
        CancellationToken cancellationToken);

    /// <summary>Gets active products whose required-product rule is met by at least one selected holder.</summary>
    Task<IReadOnlyList<AccountTypeResponse>> GetAvailableAccountTypesForHoldersAsync(
        IReadOnlyCollection<long> holderCustomerIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets an active account type by its identifier and rejects unknown or inactive identifiers.
    /// </summary>
    Task<AccountType> GetAccountTypeByIdAsync(
        long accountTypeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates the opening balance against the account type's configured minimum.
    /// </summary>
    void ValidateOpeningBalance(decimal openingBalance, AccountType accountType);

    void ValidateCustomerTypeEligibility(AccountType accountType, IReadOnlyList<Customer> customers);

    int GetRequiredRefererCount(AccountType accountType, IReadOnlyList<Customer> accountOwners);
}
