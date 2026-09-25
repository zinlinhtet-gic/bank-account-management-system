using bams.server.Data;
using bams.server.DTO.Products;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Products;
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
                accountType.AllowWithdrawal,
                accountType.AllowTransfer,
                accountType.AllowPartialWithdrawal,
                accountType.RequiredProductId,
                accountType.IsFixedDeposit,
                accountType.AllowForeigner,
                accountType.AllowCitizen,
                accountType.CitizenRequiredRefer,
                accountType.ForeignRequiredRefer))
            .ToListAsync(cancellationToken);
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
                accountType.AllowWithdrawal,
                accountType.AllowTransfer,
                accountType.AllowPartialWithdrawal,
                accountType.RequiredProductId,
                accountType.IsFixedDeposit,
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
            .FirstOrDefaultAsync(type => type.Id == accountTypeId, cancellationToken);

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

    /// <inheritdoc />
    public bool IsFixedDeposit(AccountType accountType)
    {
        return accountType.IsFixedDeposit;
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
