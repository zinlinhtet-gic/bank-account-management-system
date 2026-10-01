using System.Runtime.CompilerServices;
using bams.server.Data;
using bams.server.DTO.Products;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Products;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Customers;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class AccountTypeService : IAccountTypeService
{
    private const string AvailableAccountTypeStatus = "Active";

    private readonly ApplicationDbContext _dbContext;

    public AccountTypeService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountTypeResponse>> GetAvailableAccountTypesAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.AccountTypes
            .AsNoTracking()
            .Where(accountType => accountType.Status == AvailableAccountTypeStatus)
            .OrderBy(accountType => accountType.Id)
            .Select(accountType => new AccountTypeResponse(
                accountType.Id,
                accountType.Code,
                accountType.Name,
                accountType.Category,
                accountType.MinimumOpeningBalance,
                accountType.MinimumMaintainedBalance,
                accountType.DailyTransactionLimit,
                accountType.MonthlyTransactionLimit,
                accountType.AllowDeposit,
                accountType.AllowWithdrawal,
                accountType.AllowTransfer,
                accountType.AllowPartialWithdrawal,
                accountType.RequiredProductId,
                accountType.Category == AccountTypeCategory.FIXED,
                accountType.AllowForeigner,
                accountType.AllowCitizen,
                accountType.CitizenRequiredRefer,
                accountType.ForeignRequiredRefer))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Loads all accounts whose product category matches the requested category.</summary>
    public async Task<IReadOnlyList<Account>> GetAccountsByCategoryAsync(
        AccountTypeCategory category,
        CancellationToken cancellationToken = default) =>
        await GetAccountsQuery(category, excludeCategory: false).ToListAsync(cancellationToken);

    /// <summary>Loads all accounts whose product category differs from the requested category.</summary>
    public async Task<IReadOnlyList<Account>> GetAccountsNotInCategoryAsync(
        AccountTypeCategory category,
        CancellationToken cancellationToken = default) =>
        await GetAccountsQuery(category, excludeCategory: true).ToListAsync(cancellationToken);

    /// <summary>Loads a stable keyset page of accounts matching the category.</summary>
    public async Task<IReadOnlyList<Account>> GetAccountsByCategoryPageAsync(
        AccountTypeCategory category,
        long? afterAccountId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountBatchSize(pageSize);
        var query = GetAccountsQuery(category, excludeCategory: false);
        if (afterAccountId.HasValue)
        {
            query = query.Where(account => account.Id > afterAccountId.Value);
        }

        return await query.OrderBy(account => account.Id).Take(pageSize).ToListAsync(cancellationToken);
    }

    /// <summary>Loads a stable keyset page of accounts outside the category.</summary>
    public async Task<IReadOnlyList<Account>> GetAccountsNotInCategoryPageAsync(
        AccountTypeCategory category,
        long? afterAccountId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountBatchSize(pageSize);
        var query = GetAccountsQuery(category, excludeCategory: true);
        if (afterAccountId.HasValue)
        {
            query = query.Where(account => account.Id > afterAccountId.Value);
        }

        return await query.OrderBy(account => account.Id).Take(pageSize).ToListAsync(cancellationToken);
    }

    /// <summary>Streams matching accounts in bounded keyset batches.</summary>
    public IAsyncEnumerable<IReadOnlyList<Account>> GetAccountBatchesByCategoryAsync(
        AccountTypeCategory category,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        GetAccountBatchesAsync(category, excludeCategory: false, batchSize: batchSize, cancellationToken: cancellationToken);

    /// <summary>Streams accounts outside the category in bounded keyset batches.</summary>
    public IAsyncEnumerable<IReadOnlyList<Account>> GetAccountBatchesNotInCategoryAsync(
        AccountTypeCategory category,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        GetAccountBatchesAsync(category, excludeCategory: true, batchSize: batchSize, cancellationToken: cancellationToken);

    private IQueryable<Account> GetAccountsQuery(AccountTypeCategory category, bool excludeCategory)
    {
        var query = _dbContext.Accounts.AsNoTracking().Include(account => account.AccountType);
        return excludeCategory
            ? query.Where(account => account.AccountType != null && account.AccountType.Category != category)
            : query.Where(account => account.AccountType != null && account.AccountType.Category == category);
    }

    private async IAsyncEnumerable<IReadOnlyList<Account>> GetAccountBatchesAsync(
        AccountTypeCategory category,
        bool excludeCategory,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ValidateAccountBatchSize(batchSize);
        long lastAccountId = 0;
        while (true)
        {
            var batch = await GetAccountsQuery(category, excludeCategory)
                .Where(account => account.Id > lastAccountId)
                .OrderBy(account => account.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                yield break;
            }

            lastAccountId = batch[^1].Id;
            yield return batch;
        }
    }

    private static void ValidateAccountBatchSize(int size)
    {
        if (size is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Account query size must be between 1 and 1000.");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountTypeResponse>> GetAvailableAccountTypesForHoldersAsync(
        IReadOnlyCollection<long> holderCustomerIds,
        CancellationToken cancellationToken)
    {
        if (holderCustomerIds.Count == 0)
        {
            return [];
        }

        var selectedCustomerIds = holderCustomerIds.Distinct().ToArray();
        var selectedCustomerTypes = await _dbContext.Customers.AsNoTracking()
            .Where(customer => selectedCustomerIds.Contains(customer.Id))
            .Select(customer => customer.CustomerType)
            .Distinct()
            .ToListAsync(cancellationToken);
        var includesCitizen = selectedCustomerTypes.Contains(CustomerType.Citizen);
        var includesForeigner = selectedCustomerTypes.Contains(CustomerType.Foreigner);
        // Read active holdings once to check prerequisites and exclude types already held by every selected customer.
        var activeHoldings = await _dbContext.AccountHolders.AsNoTracking()
            .Where(holder => holderCustomerIds.Contains(holder.CustomerId) &&
                holder.Account != null && holder.Account.Status == AccountStatus.Active)
            .Select(holder => new
            {
                holder.CustomerId,
                AccountTypeId = holder.Account!.AccountTypeId
            })
            .ToListAsync(cancellationToken);

        var ownedProductIds = activeHoldings
            .Select(holding => holding.AccountTypeId)
            .Distinct()
            .ToArray();
        var ownedByEverySelectedCustomer = activeHoldings
            .Where(holding => holding.CustomerId == selectedCustomerIds[0])
            .Select(holding => holding.AccountTypeId)
            .ToHashSet();

        foreach (var selectedCustomerId in selectedCustomerIds.Skip(1))
        {
            var customerAccountTypeIds = activeHoldings
                .Where(holding => holding.CustomerId == selectedCustomerId)
                .Select(holding => holding.AccountTypeId)
                .ToHashSet();
            ownedByEverySelectedCustomer.IntersectWith(customerAccountTypeIds);
        }

        var excludedAccountTypeIds = ownedByEverySelectedCustomer.ToArray();

        return await _dbContext.AccountTypes.AsNoTracking()
            .Where(accountType => accountType.Status == AvailableAccountTypeStatus &&
                (!includesCitizen || accountType.AllowCitizen) &&
                (!includesForeigner || accountType.AllowForeigner) &&
                !excludedAccountTypeIds.Contains(accountType.Id) &&
                (!accountType.RequiredProductId.HasValue || ownedProductIds.Contains(accountType.RequiredProductId.Value)))
            .OrderBy(accountType => accountType.Id)
            .Select(accountType => new AccountTypeResponse(
                accountType.Id,
                accountType.Code,
                accountType.Name,
                accountType.Category,
                accountType.MinimumOpeningBalance,
                accountType.MinimumMaintainedBalance,
                accountType.DailyTransactionLimit,
                accountType.MonthlyTransactionLimit,
                accountType.AllowDeposit,
                accountType.AllowWithdrawal,
                accountType.AllowTransfer,
                accountType.AllowPartialWithdrawal,
                accountType.RequiredProductId,
                accountType.Category == AccountTypeCategory.FIXED,
                accountType.AllowForeigner,
                accountType.AllowCitizen,
                accountType.CitizenRequiredRefer,
                accountType.ForeignRequiredRefer))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AccountType> GetAccountTypeByIdAsync(
        long accountTypeId,
        CancellationToken cancellationToken)
    {
        var accountType = await _dbContext.AccountTypes
            .AsNoTracking()
            // Only active products can be opened; retired products are treated as unknown, matching the listings.
            .FirstOrDefaultAsync(
                type => type.Id == accountTypeId && type.Status == AvailableAccountTypeStatus,
                cancellationToken);

        if (accountType is null)
        {
            throw new NotFoundException(MessageCode.AccountTypeNotFound);
        }

        return accountType;
    }

    /// <inheritdoc />
    public void ValidateOpeningBalance(decimal openingBalance, AccountType accountType)
    {
        if (openingBalance < accountType.MinimumOpeningBalance)
        {
            throw new ValidationException(MessageCode.OpeningBalanceInvalid);
        }
    }

    /// <summary>Rejects account holders whose customer type is not allowed by the selected product.</summary>
    public void ValidateCustomerTypeEligibility(AccountType accountType, IReadOnlyList<Customer> customers)
    {
        if (customers.Any(customer =>
                (customer.CustomerType == CustomerType.Citizen && !accountType.AllowCitizen) ||
                (customer.CustomerType == CustomerType.Foreigner && !accountType.AllowForeigner)))
        {
            throw new ValidationException(MessageCode.AccountTypeCustomerTypeNotAllowed);
        }
    }

    /// <summary>Calculates the account-wide minimum by adding each holder's configured requirement.</summary>
    public int GetRequiredRefererCount(AccountType accountType, IReadOnlyList<Customer> accountOwners)
    {
        if (accountType.CitizenRequiredRefer < 0 || accountType.ForeignRequiredRefer < 0)
        {
            throw new InvalidOperationException("Account type referrer requirements cannot be negative.");
        }

        return accountOwners.Sum(customer => customer.CustomerType switch
        {
            CustomerType.Citizen => accountType.CitizenRequiredRefer,
            CustomerType.Foreigner => accountType.ForeignRequiredRefer,
            _ => 0
        });
    }
}
