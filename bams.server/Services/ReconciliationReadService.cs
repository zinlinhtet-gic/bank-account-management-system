using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Models.Accounting;
using Microsoft.EntityFrameworkCore;
namespace bams.server.Services;

/// <summary>Read-only evidence and metadata for investigation and manager review.</summary>
public sealed class ReconciliationReadService(ApplicationDbContext db)
{
    public static IReadOnlyList<DiscrepancyDefinition> Definitions { get; } =
    [
        new(DiscrepancyKind.MissingGlPosting, "Missing GL posting", "OffsetGlAccountId"),
        new(DiscrepancyKind.MissingAccountPosting, "Missing customer posting", ""),
        new(DiscrepancyKind.IncorrectAmount, "Incorrect amount", "AccountEntryId,GlEntryId,OffsetGlAccountId,CorrectAmount"),
        new(DiscrepancyKind.WrongCustomerAccount, "Wrong customer account", "AccountEntryId,GlEntryId,CorrectAccountId"),
        new(DiscrepancyKind.WrongGlAccount, "Wrong GL account", "GlEntryId,CorrectGlAccountId,CorrectAccountId"),
        new(DiscrepancyKind.DuplicatePosting, "Duplicate posting", "AccountEntryId,GlEntryId"),
        new(DiscrepancyKind.WrongDirection, "Wrong debit/credit direction", "AccountEntryId,GlEntryId"),
        new(DiscrepancyKind.PostingDateMismatch, "Posting date mismatch", "AccountEntryId,CorrectPostingDate"),
        new(DiscrepancyKind.IncompleteJournal, "Incomplete journal", "CorrectGlAccountId,CorrectAccountId"),
        new(DiscrepancyKind.BalanceSnapshot, "Incorrect balance snapshot", ""),
        new(DiscrepancyKind.OpeningBalance, "Incorrect opening posting", "AccountEntryId,GlEntryId,OffsetGlAccountId,CorrectAmount")
    ];
    public async Task<object> EvidenceAsync(long id, CancellationToken ct) => new
    {
        AccountEntries = await db.AccountTransactions.AsNoTracking().Where(x => x.TransactionId == id)
            .Select(x => new { x.Id, x.AccountId, x.EntryType, x.Amount, x.PostingDate, x.LedgerBalanceBefore, x.LedgerBalanceAfter }).ToListAsync(ct),
        JournalEntries = await db.TransactionEntries.AsNoTracking().Where(x => x.TransactionId == id ||
            db.Transactions.Any(t => t.Id == x.TransactionId && t.AccountingRootTransactionId == id))
            .Select(x => new { x.Id, x.GlAccountId, x.CustomerAccountId, x.EntryType, x.Amount, x.PostingDate }).ToListAsync(ct)
    };
    public async Task<IReadOnlyList<CorrectionGlOption>> GlOptionsAsync(CancellationToken ct) =>
        await db.GlAccounts.AsNoTracking().Where(x => x.Status == AccountingConstants.ActiveGlAccountStatus)
            .Select(x => new CorrectionGlOption(x.Id, x.Code, x.Name)).ToListAsync(ct);
    public async Task<IReadOnlyList<SummaryRevisionResponse>> SummariesAsync(DateOnly date, CancellationToken ct) =>
        await db.DailySummaryRevisions.AsNoTracking().Where(x => x.BusinessDate == date).OrderByDescending(x => x.Revision)
            .Select(x => new SummaryRevisionResponse(x.Id, x.BusinessDate, x.Revision, x.IsFinal, x.IsStale, x.LinesJson, x.ExceptionsJson, x.GeneratedAtUtc)).ToListAsync(ct);
}

