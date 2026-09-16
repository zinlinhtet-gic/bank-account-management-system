using bams.server.Models.Accounts;

namespace bams.server.Models.Transactions;

public sealed class AccountTransaction
{
    public long Id { get; set; }

    public long TransactionId { get; set; }

    public Transaction? Transaction { get; set; }

    public long AccountId { get; set; }

    public Account? Account { get; set; }

    public EntryType EntryType { get; set; }

    public decimal Amount { get; set; }

    public decimal LedgerBalanceBefore { get; set; }

    public decimal LedgerBalanceAfter { get; set; }

    public decimal AvailableBalanceBefore { get; set; }

    public decimal AvailableBalanceAfter { get; set; }

    public DateOnly ValueDate { get; set; }

    public DateOnly PostingDate { get; set; }

    public string? Description { get; set; }

    public string? ReferenceNo { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
