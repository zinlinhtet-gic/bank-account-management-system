using bams.server.Models.Accounts;

namespace bams.server.Models.Accounting;

/// <summary>Snapshot of operational and ledger balances for an account at a business-date cutoff.</summary>
public sealed class AccountReconciliationResult
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public AccountReconciliationRun? Run { get; set; }
    public long AccountId { get; set; }
    public Account? Account { get; set; }
    public DateOnly BusinessDate { get; set; }
    public decimal OperationalBalance { get; set; }
    public decimal LedgerBalance { get; set; }
    public decimal Difference { get; set; }
    public string Status { get; set; } = "Matched";
}
