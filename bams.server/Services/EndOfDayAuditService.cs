using bams.server.Data;
using bams.server.DTO.Audit;
using bams.server.Services.Interfaces;
using bams.server.Models.Transactions;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;
public sealed class EndOfDayAuditService : IEndOfDayAuditService
{
    private readonly ApplicationDbContext _dbContext;
    public EndOfDayAuditService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    /// <summary>
    /// Runs the end-of-day accounting audit for the specified accounting date.
    /// </summary>
    public async Task<EndOfDayAuditResult> RunEndOfDayAuditAsync(DateOnly auditDate, CancellationToken cancellationToken)
    {
        var transactions = await GetTransactionsForAuditDateAsync(auditDate, cancellationToken);
        var includedTransactions = transactions.Where(IsValidPostedTransaction).ToList();
        var excludedTransactionCount = transactions.Count - includedTransactions.Count;
        return new EndOfDayAuditResult(
            auditDate,
            transactions.Count,
            includedTransactions.Count,
            excludedTransactionCount,
            0m,
            0m,
            false,
            false
        );
    }
    // Retrieves transactions that were posted on the requested accounting date.
    private async Task<IReadOnlyList<Transaction>> GetTransactionsForAuditDateAsync(DateOnly auditDate, CancellationToken cancellationToken)
    {
        var startUtc = auditDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = auditDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.TransactionAt >= startUtc &&
                transaction.TransactionAt < endUtc
            ).ToListAsync(cancellationToken);
    }
    private static bool IsValidPostedTransaction(Transaction transaction)
    {
        return transaction.PostedAt.HasValue && transaction.TransactionStatus is 
        TransactionStatus.Posted or TransactionStatus.Completed;
    }
}