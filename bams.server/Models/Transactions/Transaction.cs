using bams.server.Models.Security;

namespace bams.server.Models.Transactions;

public sealed class Transaction
{
    public long Id { get; set; }

    public string TransactionNo { get; set; } = string.Empty;

    public TransactionType TransactionType { get; set; }

    public TransactionStatus TransactionStatus { get; set; } = TransactionStatus.Pending;

    public long InitiatedBy { get; set; }

    public User? InitiatedByUser { get; set; }

    public long? AuthorizedBy { get; set; }

    public User? AuthorizedByUser { get; set; }

    public DateTime? AuthorizedAt { get; set; }

    public long? PostedBy { get; set; }

    public User? PostedByUser { get; set; }

    public decimal Amount { get; set; }

    public decimal FeeAmount { get; set; }

    public DateTime TransactionAt { get; set; }

    public DateTime? PostedAt { get; set; }

    public string? Description { get; set; }

    public string? ReferenceNo { get; set; }

    // Client-supplied key that makes a retried request return the original transaction instead of posting twice.
    public string? IdempotencyKey { get; set; }

    public long? ReversalOfTransactionId { get; set; }

    public Transaction? ReversalOfTransaction { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
