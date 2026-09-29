using bams.server.Models.Transactions;

namespace bams.server.Services.Interfaces;

public interface IScheduledTransactionService
{
    Task<Transaction> CreateAsync(long actorId, TransactionType type, decimal amount, string description,
        DateTime timestamp, DateOnly effectiveDate, long accountId, bool accrued, CancellationToken cancellationToken);
}
