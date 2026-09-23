using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Customers;
using bams.server.Models.Products;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class AccountHolderService : IAccountHolderService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;

    public AccountHolderService(
        ApplicationDbContext dbContext,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Customer>> ResolveAndValidateHoldersAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.IsSharedAccount)
        {
            var customer = await ValidateIndividualAccountHolderAsync(
                request.HolderNRC1,
                request.AccountTypeId,
                cancellationToken);
            return [customer];
        }

        if (string.IsNullOrWhiteSpace(request.HolderNRC1) ||
            string.IsNullOrWhiteSpace(request.HolderNRC2))
        {
            throw new ValidationException(MessageCode.SharedAccountRequiresTwoHolders);
        }

        var firstCustomer = await GetCustomerByNrcAsync(request.HolderNRC1, cancellationToken);
        var secondCustomer = await GetCustomerByNrcAsync(request.HolderNRC2, cancellationToken);

        if (firstCustomer.Id == secondCustomer.Id)
        {
            throw new ValidationException(MessageCode.SharedAccountRequiresTwoHolders);
        }

        return [firstCustomer, secondCustomer];
    }

    /// <inheritdoc />
    public IReadOnlyList<decimal> ValidateOwnershipPercentages(CreateAccountRequest request)
    {
        if (!request.IsSharedAccount)
        {
            return [AccountConstants.FullOwnershipPercentage];
        }

        if (!request.OwnershipPercentage1.HasValue || !request.OwnershipPercentage2.HasValue)
        {
            throw new ValidationException(MessageCode.SharedAccountRequiresTwoOwnershipPercentages);
        }

        var ownershipPercentages = new[]
        {
            request.OwnershipPercentage1.Value,
            request.OwnershipPercentage2.Value
        };
        ValidateJointOwnershipPercentages(ownershipPercentages);
        return ownershipPercentages;
    }

    /// <inheritdoc />
    public async Task ValidateRequiredProductsAsync(
        AccountType accountType,
        IReadOnlyList<Customer> customers,
        CancellationToken cancellationToken)
    {
        if (!accountType.RequiredProductId.HasValue)
        {
            return;
        }

        foreach (var customer in customers)
        {
            var hasRequiredProduct = await _dbContext.AccountHolders
                .AsNoTracking()
                .AnyAsync(holder =>
                    holder.CustomerId == customer.Id &&
                    holder.Account != null &&
                    holder.Account.Status == AccountStatus.Active &&
                    holder.Account.AccountTypeId == accountType.RequiredProductId.Value,
                    cancellationToken);

            if (hasRequiredProduct)
            {
                return;
            }
        }

        throw new ValidationException(MessageCode.CustomerDoesNotHaveRequiredProducts);
    }

    /// <inheritdoc />
    public async Task CreateAccountHoldersAsync(
        Account account,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<decimal> ownershipPercentages,
        CreateAccountRequest request,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        var signingRule = NormalizeAndValidateSigningRule(request.SigningRule);

        for (var holderIndex = 0; holderIndex < customers.Count; holderIndex++)
        {
            var accountHolder = new AccountHolder
            {
                Account = account,
                CustomerId = customers[holderIndex].Id,
                OwnershipType = request.IsSharedAccount ? OwnershipType.Joint : OwnershipType.Individual,
                OwnershipPercentage = ownershipPercentages[holderIndex],
                IsPrimary = holderIndex == 0,
                SigningRule = request.IsSharedAccount ? signingRule : null,
                CreatedAt = createdAt
            };

            await _dbContext.AccountHolders.AddAsync(accountHolder, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountHolderResponse>> UpdateHoldersOfAccountAsync(
        Account account,
        UpdateAccountHoldersRequest request,
        CancellationToken cancellationToken)
    {
        var accountHolders = await _dbContext.AccountHolders
            .Where(holder => holder.AccountId == account.Id)
            .Include(holder => holder.Customer)
            .ToListAsync(cancellationToken);

        if (account.Status == AccountStatus.Closed ||
            accountHolders.Count != AccountConstants.RequiredJointAccountHolderCount ||
            accountHolders.Any(holder => holder.OwnershipType != OwnershipType.Joint))
        {
            throw new BusinessRuleException(MessageCode.AccountHolderUpdateNotAllowed);
        }

        ValidateHolderUpdateSelection(accountHolders, request.Holders);
        ValidateJointOwnershipPercentages(request.Holders.Select(holder => holder.OwnershipPercentage));
        var signingRule = NormalizeAndValidateSigningRule(request.SigningRule);
        var requestedHolders = request.Holders.ToDictionary(holder => holder.AccountHolderId);
        var oldHolders = accountHolders.Select(ToResponse).ToList();

        foreach (var accountHolder in accountHolders)
        {
            var requestedHolder = requestedHolders[accountHolder.Id];
            accountHolder.OwnershipPercentage = requestedHolder.OwnershipPercentage;
            accountHolder.IsPrimary = requestedHolder.IsPrimary;
            accountHolder.SigningRule = signingRule;
        }

        var updatedAt = DateTime.UtcNow;
        account.UpdatedAt = updatedAt;
        var newHolders = accountHolders.Select(ToResponse).ToList();
        await _auditLogService.RecordAccountHolderUpdateLogAsync(
            account.Id,
            oldHolders,
            newHolders,
            updatedAt,
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newHolders
            .OrderByDescending(holder => holder.IsPrimary)
            .ThenBy(holder => holder.Id)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Account> FindRequiredIndividualAccountAsync(
        AccountType requestedAccountType,
        Customer primaryHolder,
        CancellationToken cancellationToken)
    {
        if (!requestedAccountType.RequiredProductId.HasValue)
        {
            throw new BusinessRuleException(MessageCode.RequiredPayoutAccountNotConfigured);
        }

        var requiredAccount = await _dbContext.AccountHolders
            .AsNoTracking()
            .Where(holder =>
                holder.CustomerId == primaryHolder.Id &&
                holder.OwnershipType == OwnershipType.Individual &&
                holder.Account != null &&
                holder.Account.Status == AccountStatus.Active &&
                holder.Account.AccountTypeId == requestedAccountType.RequiredProductId.Value)
            .OrderBy(holder => holder.AccountId)
            .Select(holder => holder.Account!)
            .FirstOrDefaultAsync(cancellationToken);

        if (requiredAccount is null)
        {
            throw new NotFoundException(MessageCode.RequiredPayoutAccountNotFound);
        }

        return requiredAccount;
    }

    // Ensures a holder update describes exactly the two relationships owned by the account.
    private static void ValidateHolderUpdateSelection(
        ICollection<AccountHolder> existingHolders,
        IReadOnlyList<UpdateAccountHolderItem>? requestedHolders)
    {
        if (requestedHolders is null ||
            requestedHolders.Count != AccountConstants.RequiredJointAccountHolderCount ||
            requestedHolders.Select(holder => holder.AccountHolderId).Distinct().Count() !=
                AccountConstants.RequiredJointAccountHolderCount)
        {
            throw new ValidationException(MessageCode.AccountHolderSelectionInvalid);
        }

        if (requestedHolders.Count(holder => holder.IsPrimary) != 1)
        {
            throw new ValidationException(MessageCode.SharedAccountRequiresExactlyOnePrimaryHolder);
        }

        var existingIds = existingHolders.Select(holder => holder.Id).OrderBy(id => id);
        var requestedIds = requestedHolders.Select(holder => holder.AccountHolderId).OrderBy(id => id);
        if (!existingIds.SequenceEqual(requestedIds))
        {
            throw new ValidationException(MessageCode.AccountHolderSelectionInvalid);
        }
    }

    // Enforces valid joint ownership ranges and a total of exactly 100 percent.
    private static void ValidateJointOwnershipPercentages(IEnumerable<decimal> ownershipPercentages)
    {
        var percentages = ownershipPercentages.ToList();
        if (percentages.Any(percentage =>
                percentage <= AccountConstants.MinimumOwnershipPercentage ||
                percentage > AccountConstants.FullOwnershipPercentage))
        {
            throw new ValidationException(MessageCode.OwnershipPercentageOutOfRange);
        }

        if (percentages.Sum() != AccountConstants.FullOwnershipPercentage)
        {
            throw new ValidationException(MessageCode.SharedAccountOwnershipPercentagesMustSumTo100);
        }
    }

    // Normalizes a shared signing rule and rejects values that cannot be persisted.
    private static string? NormalizeAndValidateSigningRule(string? signingRule)
    {
        var normalizedSigningRule = string.IsNullOrWhiteSpace(signingRule)
            ? null
            : signingRule.Trim();

        if (normalizedSigningRule?.Length > AccountConstants.AccountHolderSigningRuleMaximumLength)
        {
            throw new ValidationException(MessageCode.AccountHolderSigningRuleTooLong);
        }

        return normalizedSigningRule;
    }

    // Prevents duplicate active individual accounts of the same account type.
    private async Task<Customer> ValidateIndividualAccountHolderAsync(
        string? holderNrc,
        long requestedAccountTypeId,
        CancellationToken cancellationToken)
    {
        var customer = await GetCustomerByNrcAsync(holderNrc, cancellationToken);
        var hasMatchingAccount = await _dbContext.AccountHolders
            .AsNoTracking()
            .AnyAsync(holder =>
                holder.CustomerId == customer.Id &&
                holder.OwnershipType == OwnershipType.Individual &&
                holder.Account != null &&
                holder.Account.Status == AccountStatus.Active &&
                holder.Account.AccountTypeId == requestedAccountTypeId,
                cancellationToken);

        if (hasMatchingAccount)
        {
            throw new ValidationException(MessageCode.HolderAlreadyHasActiveAccount);
        }

        return customer;
    }

    // Gets a registered customer by NRC.
    private async Task<Customer> GetCustomerByNrcAsync(
        string? nrcNumber,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(existingCustomer => existingCustomer.NrcNumber == nrcNumber, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException(MessageCode.CustomerNotFound);
        }

        return customer;
    }

    // Maps a holder relationship to its API response contract.
    private static AccountHolderResponse ToResponse(AccountHolder holder)
    {
        return new AccountHolderResponse(
            holder.Id,
            holder.AccountId,
            holder.CustomerId,
            holder.Customer?.NrcNumber ?? string.Empty,
            holder.OwnershipType,
            holder.OwnershipPercentage,
            holder.IsPrimary,
            holder.SigningRule,
            holder.Status,
            holder.CreatedAt);
    }
}
