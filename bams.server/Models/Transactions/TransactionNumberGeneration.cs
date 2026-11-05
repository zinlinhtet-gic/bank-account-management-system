namespace bams.server.Models.Transactions;

/// <summary>Persists the last reserved transaction sequence for one Myanmar business date.</summary>
public sealed class TransactionNumberGeneration
{
    public long Id { get; set; }

    public DateOnly BusinessDate { get; set; }

    public int LastSequenceNumber { get; set; }
}
