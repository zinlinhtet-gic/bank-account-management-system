using System.Globalization;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Customers;
using bams.server.Models.Products;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class AccountService : IAccountService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAccountDocumentService _accountDocumentService;

    public AccountService(
        ApplicationDbContext dbContext,
        IAccountDocumentService accountDocumentService)
    {
        _dbContext = dbContext;
        _accountDocumentService = accountDocumentService;
    }

    /// <summary>
    /// Gets all account summaries using a read-only database query.
    /// </summary>
    public async Task<IReadOnlyList<AccountSummaryResponse>> GetAccountsAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.Accounts
            .AsNoTracking()
            .OrderBy(account => account.Id)
            .Select(account => new AccountSummaryResponse(
                account.Id,
                account.AccountNo,
                account.AccountType!.Code,
                account.Status,
                account.AvailableBalance))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a single account by its unique identifier.
    /// </summary>
    public async Task<AccountResponse> GetAccountByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .Include(account => account.AccountType)
            .FirstOrDefaultAsync(account => account.Id == id, cancellationToken);

        if (account is null)
        {
            throw new NotFoundException(MessageCode.AccountNotFound);
        }

        return account.ToResponse();
    }

    /// <summary>
    /// Creates an active account after validating request and business rules.
    /// </summary>
    public async Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var accountType = await GetAccountTypeByIdAsync(request.AccountTypeId, cancellationToken);
        ValidateOpeningBalance(request.OpeningBalance, accountType);
        var ownershipPercentages = ValidateOwnershipPercentages(request);
        var customers = await ValidateHolderNRCAsync(request, cancellationToken);
        var documents = request.Documents ?? [];
        await _accountDocumentService.ValidateRequiredDocumentsAsync(
            request.AccountTypeId,
            documents,
            cancellationToken);
        var now = DateTime.UtcNow;
        IReadOnlyList<string> storedFileReferences = [];
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var accountNumber = await GenerateAccountNumberAsync(
                request.AccountTypeId,
                now,
                cancellationToken);
            var account = await InsertAccountAsync(
                request,
                accountNumber,
                now,
                cancellationToken);
            await InsertAccountHolderAsync(
                account,
                customers,
                ownershipPercentages,
                request,
                now,
                cancellationToken);

            // Persist the account first so its database identifier can organize private files.
            await _dbContext.SaveChangesAsync(cancellationToken);
            storedFileReferences = await _accountDocumentService.StoreAccountDocumentsAsync(
                account,
                documents,
                now,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            account.AccountType = accountType;
            return account.ToResponse();
        }
        catch
        {
            // File storage cannot participate in the database transaction, so compensate on failure.
            _accountDocumentService.DeleteStoredFiles(storedFileReferences);
            throw;
        }
    }

    // Gets an account type by identifier and rejects unknown account types.
    private async Task<AccountType> GetAccountTypeByIdAsync(
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

    // Enforces the configured minimum opening balance for the account type.
    private void ValidateOpeningBalance(
        decimal openingBalance,
        AccountType accountType)
    {
        if (openingBalance < accountType.MinimumOpeningBalance)
        {
            throw new ValidationException(MessageCode.OpeningBalanceInvalid);
        }
    }

    // Normalizes individual ownership and validates joint ownership percentages.
    private IReadOnlyList<decimal> ValidateOwnershipPercentages(
        CreateAccountRequest request)
    {
        if (!request.IsSharedAccount)
        {
            return [AccountConstants.FullOwnershipPercentage];
        }

        if (!request.OwnershipPercentage1.HasValue || !request.OwnershipPercentage2.HasValue)
        {
            throw new ValidationException(MessageCode.SharedAccountRequiresTwoOwnershipPercentages);
        }

        var firstOwnershipPercentage = request.OwnershipPercentage1.Value;
        var secondOwnershipPercentage = request.OwnershipPercentage2.Value;

        if (!IsValidOwnershipPercentage(firstOwnershipPercentage) ||
            !IsValidOwnershipPercentage(secondOwnershipPercentage))
        {
            throw new ValidationException(MessageCode.OwnershipPercentageOutOfRange);
        }

        if (firstOwnershipPercentage + secondOwnershipPercentage !=
            AccountConstants.FullOwnershipPercentage)
        {
            throw new ValidationException(MessageCode.SharedAccountOwnershipPercentagesMustSumTo100);
        }

        return [firstOwnershipPercentage, secondOwnershipPercentage];
    }

    // Determines whether a holder percentage is within the supported ownership range.
    private static bool IsValidOwnershipPercentage(decimal ownershipPercentage)
    {
        return ownershipPercentage > AccountConstants.MinimumOwnershipPercentage &&
               ownershipPercentage <= AccountConstants.FullOwnershipPercentage;
    }

    // Resolves and validates the customers who will hold the requested account.
    private async Task<List<Customer>> ValidateHolderNRCAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var customers = new List<Customer>();

        if (!request.IsSharedAccount)
        {
            var customer = await ValidateIndividualAccountHolderAsync(
                request.HolderNRC1,
                request.AccountTypeId,
                cancellationToken);
            customers.Add(customer);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.HolderNRC1) || string.IsNullOrWhiteSpace(request.HolderNRC2))
            {
                throw new ValidationException(MessageCode.SharedAccountRequiresTwoHolders);
            }
            var customer1 = await GetCustomerByNRCAsync(request.HolderNRC1!, cancellationToken);
            var customer2 = await GetCustomerByNRCAsync(request.HolderNRC2!, cancellationToken);
            customers.Add(customer1);
            customers.Add(customer2);
        }
        return customers;
    }

    // Prevents a customer from holding duplicate active individual accounts of the same type.
    private async Task<Customer> ValidateIndividualAccountHolderAsync(
        string? holderNRC1,
        long requestedAccountTypeId,
        CancellationToken cancellationToken)
    {
        var customer = await GetCustomerByNRCAsync(holderNRC1!, cancellationToken);
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

    // Allocates the next sequence for the account type and UTC hour, then builds a 16-digit number.
    private async Task<string> GenerateAccountNumberAsync(
        long accountTypeId,
        DateTime currentDateTime,
        CancellationToken cancellationToken)
    {
        if (accountTypeId is < 1 or > AccountConstants.MaximumAccountTypeIdentifier)
        {
            throw new BusinessRuleException(MessageCode.AccountTypeIdentifierOutOfRange);
        }

        var generationPeriod = currentDateTime.ToString(
            AccountConstants.AccountNumberTimestampFormat,
            CultureInfo.InvariantCulture);

        var sequenceNumber = await GetSequenceNumberAsync(
            accountTypeId,
            generationPeriod,
            cancellationToken);

        if (sequenceNumber > AccountConstants.MaximumHourlyAccountNumberSequence)
        {
            throw new BusinessRuleException(MessageCode.AccountNumberSequenceExhausted);
        }

        var accountNumber = string.Concat(
            accountTypeId.ToString(
                $"D{AccountConstants.AccountTypeIdentifierWidth}",
                CultureInfo.InvariantCulture),
            generationPeriod,
            sequenceNumber.ToString(
                $"D{AccountConstants.AccountNumberSequenceWidth}",
                CultureInfo.InvariantCulture));

        if (accountNumber.Length != AccountConstants.AccountNumberRequiredLength)
        {
            throw new InvalidOperationException(
                "Generated account number does not have the required length.");
        }

        return accountNumber;
    }

    // Creates and tracks a new active account for the validated request.
    private async Task<Account> InsertAccountAsync(
        CreateAccountRequest request,
        string accountNumber,
        DateTime currentDateTime,
        CancellationToken cancellationToken)
    {
        var account = new Account
        {
            AccountNo = accountNumber,
            AccountTypeId = request.AccountTypeId,
            Status = AccountStatus.Active,
            OpenedAt = currentDateTime,
            AvailableBalance = request.OpeningBalance,
            LedgerBalance = request.OpeningBalance,
            CreatedAt = currentDateTime,
            UpdatedAt = currentDateTime
        };

        await _dbContext.Accounts.AddAsync(account, cancellationToken);
        return account;
    }

    // Creates and tracks account-holder relationships for all validated customers.
    private async Task InsertAccountHolderAsync(
        Account account,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<decimal> ownershipPercentages,
        CreateAccountRequest request,
        DateTime currentDateTime,
        CancellationToken cancellationToken)
    {
        for (var holderIndex = 0; holderIndex < customers.Count; holderIndex++)
        {
            var customer = customers[holderIndex];
            var accountHolder = new AccountHolder
            {
                Account = account,
                CustomerId = customer.Id,
                OwnershipType = request.IsSharedAccount ? OwnershipType.Joint : OwnershipType.Individual,
                OwnershipPercentage = ownershipPercentages[holderIndex],
                CreatedAt = currentDateTime
            };

            await _dbContext.AccountHolders.AddAsync(accountHolder, cancellationToken);
        }
    }

    // Atomically creates or increments the sequence for an account type and UTC hour.
    private async Task<int> GetSequenceNumberAsync(
        long accountTypeId,
        string generationPeriod,
        CancellationToken cancellationToken)
    {
        // The unique period key makes this MySQL insert-or-increment atomic for concurrent requests.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO `AccountNumberGenerations`
                (`AccountTypeId`, `GenerationPeriod`, `LastSequenceNumber`)
            VALUES
                ({accountTypeId}, {generationPeriod}, 1)
            ON DUPLICATE KEY UPDATE
                `LastSequenceNumber` = `LastSequenceNumber` + 1
            """,
            cancellationToken);

        // The transaction retains the row lock until the generated account is persisted.
        var sequenceNumber = await _dbContext.AccountNumberGenerations
            .AsNoTracking()
            .Where(generation =>
                generation.AccountTypeId == accountTypeId &&
                generation.GenerationPeriod == generationPeriod)
            .Select(generation => generation.LastSequenceNumber)
            .SingleAsync(cancellationToken);

        return sequenceNumber;
    }

    // Gets a customer by NRC and rejects NRCs that are not registered customers.
    private async Task<Customer> GetCustomerByNRCAsync(
        string nrcNumber,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.NrcNumber == nrcNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException(MessageCode.CustomerNotFound);
        }

        return customer;
    }
}
