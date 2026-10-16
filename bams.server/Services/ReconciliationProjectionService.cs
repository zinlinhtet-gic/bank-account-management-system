using System.Text.Json;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Rebuilds derived history and retains provisional daily summary versions.</summary>
public sealed class ReconciliationProjectionService(ApplicationDbContext db)
{
    /// <summary>Appends recalculated balances; financial source snapshots are never rewritten.</summary>
    public async Task RebuildAccountAsync(long accountId, long proposalId, CancellationToken ct, string proposalSource = "Account")
    {
        var entries = await db.AccountTransactions.Where(x => x.AccountId == accountId)
            .OrderBy(x => x.PostingDate).ThenBy(x => x.Id).ToListAsync(ct);
        if (entries.Count == 0) return;
        decimal balance = entries.OrderBy(x => x.Id).First().LedgerBalanceBefore;
        foreach (var entry in entries)
        {
            var before = balance;
            balance += entry.EntryType == EntryType.Credit ? entry.Amount : -entry.Amount;
            db.EffectiveAccountBalances.Add(new EffectiveAccountBalance
            {
                AccountId = accountId, AccountTransactionId = entry.Id, ProposalId = proposalId, ProposalSource = proposalSource,
                BalanceBefore = before, BalanceAfter = balance, CalculatedAtUtc = DateTime.UtcNow
            });
        }
        var account = await db.Accounts.SingleAsync(x => x.Id == accountId, ct);
        var delta = balance - account.LedgerBalance;
        if (balance < 0 || account.AvailableBalance + delta < 0)
            throw new BusinessRuleException(MessageCode.InsufficientBalance);
        account.AvailableBalance += delta;
        account.LedgerBalance = balance;
        account.Version++;
        account.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Serializes summary revisions with business-date switches and correction execution.</summary>
    public async Task GenerateProvisionalAsync(DateOnly from, CancellationToken ct)
    {
        var owns = db.Database.CurrentTransaction is null;
        await using var transaction = owns ? await db.Database.BeginTransactionAsync(ct) : null;
        var dates = await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Date >= {from} AND Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(ct);
        foreach (var date in dates) await GenerateDateAsync(date.Date, false, ct);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    /// <summary>Creates a new immutable snapshot of posted GL amounts and outstanding investigations.</summary>
    public async Task GenerateDateAsync(DateOnly date, bool final, CancellationToken ct)
    {
        var prior = await db.DailySummaryRevisions.Where(x => x.BusinessDate == date).ToListAsync(ct);
        foreach (var version in prior.Where(x => !x.IsFinal)) version.IsStale = true;
        var entries = await db.TransactionEntries.AsNoTracking().Where(x => x.PostingDate <= date).ToListAsync(ct);
        var gl = await db.GlAccounts.AsNoTracking().ToListAsync(ct);
        var lines = gl.Select(account =>
        {
            var accountEntries = entries.Where(x => x.GlAccountId == account.Id).ToList();
            int sign = account.AccountClass is GlAccountClass.Asset or GlAccountClass.Expense ? 1 : -1;
            decimal opening = accountEntries.Where(x => x.PostingDate < date).Sum(x => x.EntryType == EntryType.Debit ? x.Amount : -x.Amount) * sign;
            decimal debit = accountEntries.Where(x => x.PostingDate == date && x.EntryType == EntryType.Debit).Sum(x => x.Amount);
            decimal credit = accountEntries.Where(x => x.PostingDate == date && x.EntryType == EntryType.Credit).Sum(x => x.Amount);
            return new { GlAccountId = account.Id, account.Code, account.Name, OpeningBalance = opening, TotalDebit = debit, TotalCredit = credit, ClosingBalance = opening + (debit - credit) * sign };
        }).ToList();
        var exceptions = await db.ReconciliationExceptions.AsNoTracking().Where(x => x.BusinessDate <= date && x.Status != OperationsConstants.ExceptionResolved)
            .Select(x => new { x.Id, x.BusinessDate, x.Type, x.Difference, x.Status }).ToListAsync(ct);
        db.DailySummaryRevisions.Add(new DailySummaryRevision
        {
            BusinessDate = date, Revision = prior.Count == 0 ? 1 : prior.Max(x => x.Revision) + 1,
            IsFinal = final, LinesJson = JsonSerializer.Serialize(lines), ExceptionsJson = JsonSerializer.Serialize(exceptions),
            GeneratedAtUtc = DateTime.UtcNow
        });
    }
}

