using bams.server.Models.Accounting;
using bams.server.Models.Accounts;

namespace bams.server.Models.Transactions;

public sealed class TransactionEntry
{
    public long Id { get; set; }

    public long TransactionId { get; set; }

    public Transaction? Transaction { get; set; }

    public long GlAccountId { get; set; }

    public GlAccount? GlAccount { get; set; }

    public long? CustomerAccountId { get; set; }

    public Account? CustomerAccount { get; set; }

    public EntryType EntryType { get; set; }

    public decimal Amount { get; set; }

    public DateOnly PostingDate { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
