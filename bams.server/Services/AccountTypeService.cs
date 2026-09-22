using bams.server.Data;
using bams.server.DTO.Products;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Products;
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
                accountType.IsFixedDeposit))
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
}
