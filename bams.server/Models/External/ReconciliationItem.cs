using bams.server.Models.Transactions;

namespace bams.server.Models.External;

public sealed class ReconciliationItem
{
    public long Id { get; set; }

    public long ReconciliationBatchId { get; set; }

    public ReconciliationBatch? ReconciliationBatch { get; set; }

    public long TransactionId { get; set; }

    public Transaction? Transaction { get; set; }

    public long TransactionEntryId { get; set; }

    public TransactionEntry? TransactionEntry { get; set; }

    public decimal ExpectedAmount { get; set; }

    public decimal ActualAmount { get; set; }

    public decimal Difference { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }
}
