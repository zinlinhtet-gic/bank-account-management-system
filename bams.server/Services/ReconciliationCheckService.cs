using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace bams.server.Services;

/// <summary>Persists journal exceptions as well as account mismatches; repairs are grouped with their source journal.</summary>
public sealed class ReconciliationCheckService(ApplicationDbContext db, IAccountReconciliationService accounts,
    ICurrentUserService user, ReconciliationProjectionService projections, ReconciliationExclusionService exclusions)
{
    public async Task<AccountReconciliationRunResponse> RunAsync(AccountReconciliationRequest request, CancellationToken ct)
    {
        await exclusions.ValidateAsync(request.FromDate, request.ToDate, ct);
        AccountReconciliationRunResponse result = await accounts.ReconcileAccountsAsync(request, ct);
        List<Transaction> transactions = await db.Transactions.AsNoTracking().Where(x => x.PostedAt != null &&
            (x.BusinessDate >= request.FromDate && x.BusinessDate <= request.ToDate ||
             db.TransactionEntries.Any(e => (e.TransactionId == x.Id || e.Transaction!.AccountingRootTransactionId == x.Id) &&
                e.PostingDate >= request.FromDate && e.PostingDate <= request.ToDate)))
            .ToListAsync(ct);
        foreach (Transaction transaction in transactions.Where(x => x.AccountingRootTransactionId == null &&
            x.TransactionType != TransactionType.AccountPostingRepair))
        {
            DateOnly sourceDate = transaction.BusinessDate ?? request.FromDate;
            HashSet<long> waivedTransactions = await exclusions.ActiveSourceIdsAsync(sourceDate, "Transaction", ct);
            HashSet<long> waivedLines = await exclusions.ActiveSourceIdsAsync(sourceDate, "GL", ct);
            if (waivedTransactions.Contains(transaction.Id)) continue;
            List<TransactionEntry> lines = await db.TransactionEntries.AsNoTracking().Where(x => x.TransactionId == transaction.Id ||
                db.Transactions.Any(t => t.Id == x.TransactionId && t.AccountingRootTransactionId == transaction.Id)).ToListAsync(ct);
            bool journalMissing = lines.Count == 0;
            lines = lines.Where(line => !waivedLines.Contains(line.Id)).ToList();
            decimal debit = lines.Where(x => x.EntryType == EntryType.Debit).Sum(x => x.Amount);
            decimal credit = lines.Where(x => x.EntryType == EntryType.Credit).Sum(x => x.Amount);
            DateOnly date = transaction.BusinessDate ?? request.FromDate;
            await UpsertAsync("JournalIntegrity", date, transaction.Id, debit, credit, journalMissing || debit != credit, ct);
        }
        for (DateOnly date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            HashSet<long> waivedTransactions = await exclusions.ActiveSourceIdsAsync(date, "Transaction", ct);
            HashSet<long> waivedLines = await exclusions.ActiveSourceIdsAsync(date, "GL", ct);
            List<ReconciliationLedgerAmount> lines = await ReconciliationLedgerQueries.Amounts(db)
                .Where(line => line.ReconciliationDate == date && !waivedTransactions.Contains(line.RootTransactionId) && !waivedLines.Contains(line.Id)).ToListAsync(ct);
            decimal debit = lines.Where(line => line.Direction == EntryType.Debit).Sum(line => line.Amount);
            decimal credit = lines.Where(line => line.Direction == EntryType.Credit).Sum(line => line.Amount);
            await UpsertAsync(OperationsConstants.ExceptionLedgerBalance, date, null, debit, credit, debit != credit, ct);
        }
        await db.SaveChangesAsync(ct);
        await projections.GenerateProvisionalAsync(request.FromDate, ct);
        return result;
    }

    private async Task UpsertAsync(string type, DateOnly date, long? transactionId, decimal debit, decimal credit, bool failed, CancellationToken ct)
    {
        ReconciliationException? existing = await db.ReconciliationExceptions.Where(x => x.Type == type && x.BusinessDate == date &&
            x.RelatedTransactionId == transactionId && x.Status != OperationsConstants.ExceptionResolved).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (existing == null && !failed) return;
        if (existing?.Status == OperationsConstants.ExceptionForcePassed) return;
        if (existing == null)
        {
            existing = new ReconciliationException { Type = type, Source = "ReconciliationChecks", BusinessDate = date,
                RelatedTransactionId = transactionId, Severity = "Critical", Status = OperationsConstants.ExceptionOpen,
                CreatedBy = user.GetCurrentUserId(), CreatedAtUtc = DateTime.UtcNow };
            db.ReconciliationExceptions.Add(existing);
        }
        string old = existing.Status;
        existing.ExpectedAmount = debit; existing.ActualAmount = credit; existing.Difference = debit - credit;
        if (!failed && !await db.CorrectionProposals.AnyAsync(x => x.ExceptionId == existing.Id &&
            (x.Status == ProposalStatus.PendingApproval || x.Status == ProposalStatus.Executed && x.Verification != VerificationStatus.Passed), ct))
            existing.Status = OperationsConstants.ExceptionResolved;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory { Exception = existing,
            OldStatus = old, NewStatus = existing.Status, Note = failed ? "Journal check failed." : "Journal check passed.",
            ActorId = user.GetCurrentUserId(), CreatedAtUtc = DateTime.UtcNow });
    }
}

