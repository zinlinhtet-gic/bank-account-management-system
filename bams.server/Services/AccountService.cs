using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models;
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
                account.AccountNumber,
                account.Name,
                account.Type,
                account.Status,
                account.Balance))
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
        ValidateCreateAccountRequest(request);

        var account = new Account
        {
            AccountNumber = GenerateAccountNumber(),
            Name = request.Name.Trim(),
            Type = request.Type,
            Status = AccountStatus.Active,
            Balance = request.OpeningBalance,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.Accounts.AddAsync(account, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return account.ToResponse();
    }

    // Enforces request and business validation before an account entity is created.
    private static void ValidateCreateAccountRequest(CreateAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(MessageCode.AccountNameRequired);
        }

        if (!Enum.IsDefined(request.Type))
        {
            throw new ValidationException(MessageCode.AccountTypeInvalid);
        }

        if (request.OpeningBalance < AccountConstants.MinimumOpeningBalance)
        {
            throw new ValidationException(MessageCode.OpeningBalanceInvalid);
        }
    }

    // Generates a readable account number without relying on client-provided identifiers.
    private static string GenerateAccountNumber()
    {
        return string.Concat(
            AccountConstants.AccountNumberPrefix,
            DateTime.UtcNow.ToString(AccountConstants.AccountNumberTimestampFormat));
    }
}
