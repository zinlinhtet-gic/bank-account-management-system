using bams.server.Constants;
using bams.server.Data;
using bams.server.Models.Transactions;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Groups repair components with their closed source journal for checks, preserving financial posting dates.</summary>
internal static class ReconciliationLedgerQueries
{
    // Only linked journal repairs inherit a reconciliation date; ordinary adjustments use their actual posting date.
    public static IQueryable<ReconciliationLedgerAmount> Amounts(ApplicationDbContext db) =>
        from entry in db.TransactionEntries.AsNoTracking()
        join original in db.Transactions on entry.Transaction!.AccountingRootTransactionId equals (long?)original.Id into originals
        from original in originals.DefaultIfEmpty()
        select new ReconciliationLedgerAmount
        {
            Id = entry.Id,
            RootTransactionId = entry.Transaction!.AccountingRootTransactionId ?? entry.TransactionId,
            ReconciliationDate = original != null && original.BusinessDate.HasValue &&
                db.BusinessDates.Any(day => day.Date == original.BusinessDate.Value && day.Status == OperationsConstants.BusinessDateClosed)
                ? original.BusinessDate.Value : entry.PostingDate,
            Direction = entry.EntryType,
            Amount = entry.Amount
        };
}

/// <summary>Financial amounts attributed to the journal's date solely for reconciliation validation.</summary>
internal sealed class ReconciliationLedgerAmount
{
    public long Id { get; init; }
    public long RootTransactionId { get; init; }
    public DateOnly ReconciliationDate { get; init; }
    public EntryType Direction { get; init; }
    public decimal Amount { get; init; }
}
