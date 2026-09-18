using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class AccountService : IAccountService
{
    private readonly ApplicationDbContext _dbContext;

    public AccountService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
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
        var accountType = await _dbContext.AccountTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(type => type.Id == request.AccountTypeId, cancellationToken);

        if (accountType is null)
        {
            throw new NotFoundException(MessageCode.AccountTypeNotFound);
        }

        if (request.OpeningBalance < accountType.MinimumOpeningBalance)
        {
            throw new ValidationException(MessageCode.OpeningBalanceInvalid);
        }

        var now = DateTime.UtcNow;
        var account = new Account
        {
            AccountNo = GenerateAccountNumber(),
            AccountTypeId = request.AccountTypeId,
            Status = AccountStatus.Active,
            OpenedAt = now,
            AvailableBalance = request.OpeningBalance,
            LedgerBalance = request.OpeningBalance,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _dbContext.Accounts.AddAsync(account, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        account.AccountType = accountType;

        return account.ToResponse();
    }

    // Generates a readable account number without relying on client-provided identifiers.
    private static string GenerateAccountNumber()
    {
        return string.Concat(
            AccountConstants.AccountNumberPrefix,
            DateTime.UtcNow.ToString(AccountConstants.AccountNumberTimestampFormat));
    }
}
