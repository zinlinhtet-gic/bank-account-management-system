using bams.server.Models.Transactions;

namespace bams.server.Services.Interfaces;

public interface IGeneralLedgerPostingService
{
    Task AddAccrualEntriesAsync(Transaction transaction, long accountId, TransactionType type, decimal amount,
        string description, DateOnly postingDate, DateTime createdAt, CancellationToken cancellationToken);
    Task AddPostingEntriesAsync(Transaction transaction, long accountId, TransactionType type, decimal amount,
        string description, DateOnly postingDate, DateTime createdAt, CancellationToken cancellationToken);
}
