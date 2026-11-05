using bams.server.Data;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;

namespace bams.server.Services;

public sealed class ScheduledTransactionService(ApplicationDbContext dbContext,
    ITransactionNumberGenerator transactionNumbers) : IScheduledTransactionService
{
    public async Task<Transaction> CreateAsync(long actorId, TransactionType type, decimal amount, string description,
        DateTime timestamp, DateOnly effectiveDate, long accountId, bool accrued, CancellationToken cancellationToken)
    {
        var transactionNumber = await transactionNumbers.ReserveAsync(effectiveDate, isScheduled: true, cancellationToken);
        var transaction = new Transaction
        {
            TransactionNo = transactionNumber, TransactionType = type,
            TransactionStatus = accrued ? TransactionStatus.Accrued : TransactionStatus.Completed,
            InitiatedBy = actorId, PostedBy = actorId, Amount = amount, TransactionAt = timestamp,
            BusinessDate = effectiveDate,
            PostedAt = timestamp, CreatedAt = timestamp, UpdatedAt = timestamp, Description = description,
            ReferenceNo = $"JOB-{type}-{accountId}-{effectiveDate:yyyyMMdd}"
        };
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        return transaction;
    }
}
