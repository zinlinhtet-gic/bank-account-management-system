using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class AccountTransactionService : IAccountTransactionService
{
    private readonly ApplicationDbContext _dbContext;

    public AccountTransactionService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountTransactionDetailResponse>> GetAccountTransactionsAsync(
        long accountId,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Accounts.AsNoTracking().AnyAsync(account => account.Id == accountId, cancellationToken))
        {
            throw new NotFoundException(MessageCode.AccountNotFound);
        }

        return await _dbContext.AccountTransactions.AsNoTracking()
            .Where(entry => entry.AccountId == accountId)
            .OrderByDescending(entry => entry.CreatedAt)
            .Select(entry => new AccountTransactionDetailResponse(
                entry.Id,
                entry.Transaction!.TransactionNo,
                entry.Transaction.TransactionType,
                entry.Transaction.TransactionStatus,
                entry.EntryType,
                entry.Amount,
                entry.LedgerBalanceAfter,
                entry.AvailableBalanceAfter,
                entry.ValueDate,
                entry.PostingDate,
                entry.Description,
                entry.ReferenceNo,
                entry.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RecordAccountOpeningTransactionAsync(
        Account account,
        decimal openingBalance,
        DateTime currentDateTime,
        CancellationToken cancellationToken)
    {
        // Create a new account transaction for the account opening
    }
}
