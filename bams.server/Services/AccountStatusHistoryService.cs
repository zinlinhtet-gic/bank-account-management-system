using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Queries account status history without exposing persistence entities.</summary>
public sealed class AccountStatusHistoryService : IAccountStatusHistoryService
{
    private readonly ApplicationDbContext _dbContext;

    public AccountStatusHistoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountStatusHistoryResponse>> GetAccountStatusHistoryAsync(
        long accountId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Accounts.AsNoTracking().AnyAsync(account => account.Id == accountId, cancellationToken))
        {
            throw new NotFoundException(MessageCode.AccountNotFound);
        }

        return await _dbContext.AccountStatusHistories.AsNoTracking()
            .Where(history => history.AccountId == accountId)
            .OrderByDescending(history => history.ChangedAt)
            .Select(history => new AccountStatusHistoryResponse(
                history.Id,
                history.OldStatus,
                history.NewStatus,
                history.Reason,
                history.ChangedByUser == null ? string.Empty : history.ChangedByUser.FullName,
                history.ChangedAt))
            .ToListAsync(cancellationToken);
    }
}
