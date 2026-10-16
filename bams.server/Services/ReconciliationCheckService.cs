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
    ICurrentUserService user, ReconciliationProjectionService projections)
{
    public async Task<AccountReconciliationRunResponse> RunAsync(AccountReconciliationRequest request, CancellationToken ct)
    {
        var result = await accounts.ReconcileAccountsAsync(request, ct);
        var transactions = await db.Transactions.AsNoTracking().Where(x => x.PostedAt != null &&
            (x.BusinessDate >= request.FromDate && x.BusinessDate <= request.ToDate ||
             db.TransactionEntries.Any(e => e.TransactionId == x.Id && e.PostingDate >= request.FromDate && e.PostingDate <= request.ToDate)))
            .ToListAsync(ct);
        foreach (var transaction in transactions.Where(x => x.AccountingRootTransactionId == null &&
            x.TransactionType != TransactionType.AccountPostingRepair))
        {
            var lines = await db.TransactionEntries.AsNoTracking().Where(x => x.TransactionId == transaction.Id ||
                db.Transactions.Any(t => t.Id == x.TransactionId && t.AccountingRootTransactionId == transaction.Id)).ToListAsync(ct);
            var debit = lines.Where(x => x.EntryType == EntryType.Debit).Sum(x => x.Amount);
            var credit = lines.Where(x => x.EntryType == EntryType.Credit).Sum(x => x.Amount);
            var date = transaction.BusinessDate ?? request.FromDate;
            await UpsertAsync("JournalIntegrity", date, transaction.Id, debit, credit, lines.Count == 0 || debit != credit, ct);
        }
        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            var lines = await db.TransactionEntries.AsNoTracking().Where(x => x.PostingDate == date).ToListAsync(ct);
            var debit = lines.Where(x => x.EntryType == EntryType.Debit).Sum(x => x.Amount);
            var credit = lines.Where(x => x.EntryType == EntryType.Credit).Sum(x => x.Amount);
            await UpsertAsync(OperationsConstants.ExceptionLedgerBalance, date, null, debit, credit, debit != credit, ct);
        }
        await db.SaveChangesAsync(ct);
        await projections.GenerateProvisionalAsync(request.FromDate, ct);
        return result;
    }

    private async Task UpsertAsync(string type, DateOnly date, long? transactionId, decimal debit, decimal credit, bool failed, CancellationToken ct)
    {
        var existing = await db.ReconciliationExceptions.Where(x => x.Type == type && x.BusinessDate == date &&
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
        var old = existing.Status;
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

