using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Builds deterministic correction previews. No financial records are changed here.</summary>
public sealed class CorrectionPlanningService(ApplicationDbContext db, IBusinessDateService businessDates)
{
    public const string CustomerLedger = "Customer";
    public const string GeneralLedger = "GL";
    private sealed record Context(ReconciliationException Exception, Transaction Original,
        List<AccountTransaction> Accounts, List<TransactionEntry> Journal, long DepositGlId, CorrectionInputs Input);

    /// <summary>Validates evidence and delegates calculation to the diagnosed discrepancy handler.</summary>
    public async Task<CorrectionPreview> PreviewAsync(long exceptionId, CorrectionInputs input, CancellationToken ct)
    {
        if (!Enum.IsDefined(input.Kind) || string.IsNullOrWhiteSpace(input.Findings) ||
            string.IsNullOrWhiteSpace(input.EvidenceReference) || input.Findings.Length > 2000 || input.EvidenceReference.Length > 500)
            throw new ValidationException(MessageCode.CorrectionEvidenceRequired);
        var exception = await db.ReconciliationExceptions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == exceptionId, ct)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (exception.PositionSessionId.HasValue || exception.Status is OperationsConstants.ExceptionResolved or OperationsConstants.ExceptionForcePassed)
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        var original = await db.Transactions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.TransactionId && x.PostedAt != null, ct)
            ?? throw new NotFoundException(MessageCode.TransactionNotFound);
        if (original.AccountingRootTransactionId.HasValue || original.TransactionType is TransactionType.ReconciliationAdjustment or TransactionType.AccountPostingRepair)
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        var accounts = await db.AccountTransactions.AsNoTracking().Where(x => x.TransactionId == original.Id).OrderBy(x => x.Id).ToListAsync(ct);
        var journal = await db.TransactionEntries.AsNoTracking().Where(x => x.TransactionId == original.Id ||
            db.Transactions.Any(t => t.Id == x.TransactionId && t.AccountingRootTransactionId == original.Id)).OrderBy(x => x.Id).ToListAsync(ct);
        if (exception.AccountId.HasValue && !accounts.Any(x => x.AccountId == exception.AccountId) &&
            !journal.Any(x => x.CustomerAccountId == exception.AccountId))
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        if (exception.RelatedTransactionId.HasValue && exception.RelatedTransactionId != original.Id)
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        if (!exception.AccountId.HasValue && !exception.RelatedTransactionId.HasValue &&
            !journal.Any(x => x.PostingDate == exception.BusinessDate) && original.BusinessDate != exception.BusinessDate)
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        var deposit = await db.GlAccounts.Where(x => x.Code == AccountingConstants.CustomerDepositsGlCode).Select(x => x.Id).SingleAsync(ct);
        // Prevent applying a correction to the same source components twice, even if live balances changed.
        var executed = await db.CorrectionProposals.AsNoTracking().Where(x => x.Status == ProposalStatus.Executed && x.Kind == input.Kind).Select(x => x.InputsJson).ToListAsync(ct);
        if (input.Kind != DiscrepancyKind.BalanceSnapshot && executed.Select(x => JsonSerializer.Deserialize<CorrectionInputs>(x)!).Any(x =>
            x.TransactionId == input.TransactionId && x.AccountEntryId == input.AccountEntryId && x.GlEntryId == input.GlEntryId))
            throw new ConflictException(MessageCode.CorrectionAlreadyApplied);
        var context = new Context(exception, original, accounts, journal, deposit, input);
        var lines = input.Kind switch
        {
            DiscrepancyKind.MissingGlPosting => CorrectMissingGlPosting(context),
            DiscrepancyKind.MissingAccountPosting => CorrectMissingAccountPosting(context),
            DiscrepancyKind.IncorrectAmount => CorrectPostingAmount(context),
            DiscrepancyKind.WrongCustomerAccount => CorrectCustomerAccount(context),
            DiscrepancyKind.WrongGlAccount => ReclassifyGlPosting(context),
            DiscrepancyKind.DuplicatePosting => CorrectDuplicatePosting(context),
            DiscrepancyKind.WrongDirection => CorrectPostingDirection(context),
            DiscrepancyKind.PostingDateMismatch => CorrectPostingDateMismatch(context),
            DiscrepancyKind.IncompleteJournal => RepairIncompleteJournal(context),
            DiscrepancyKind.BalanceSnapshot => RepairAccountBalanceSnapshot(context),
            DiscrepancyKind.OpeningBalance => CorrectOpeningBalance(context),
            _ => throw new ValidationException(MessageCode.InvalidRequest)
        };
        var journalRepair = input.Kind is DiscrepancyKind.MissingGlPosting or DiscrepancyKind.IncompleteJournal;
        var balanceRepair = input.Kind == DiscrepancyKind.BalanceSnapshot;
        DateOnly postingDate = await businessDates.GetCorrectionPostingDateAsync(exception.BusinessDate, ct);
        // A forced closed period is immutable; the correction keeps its source links and posts on the active date.
        if (postingDate != exception.BusinessDate)
            lines = lines.Select(line => line with { PostingDate = postingDate }).ToList();
        else
        {
            foreach (DateOnly lineDate in lines.Select(line => line.PostingDate).Distinct().ToList())
            {
                DateOnly effectiveDate = await businessDates.GetCorrectionPostingDateAsync(lineDate, ct);
                if (effectiveDate != lineDate)
                    lines = lines.Select(line => line.PostingDate == lineDate ? line with { PostingDate = effectiveDate } : line).ToList();
            }
        }
        await ValidateLinesAsync(context, lines, journalRepair, balanceRepair, postingDate, ct);
        var ids = lines.Where(x => x.Ledger == CustomerLedger).Select(x => x.AccountId)
            .Concat(balanceRepair ? new[] { exception.AccountId ?? throw new ValidationException(MessageCode.InvalidRequest) } : []).Distinct().Order().ToList();
        var balances = await db.Accounts.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
        var impacts = new List<CorrectionBalanceImpact>();
        foreach (var account in balances)
        {
            decimal change = lines.Where(x => x.Ledger == CustomerLedger && x.AccountId == account.Id).Sum(CustomerEffect);
            if (balanceRepair)
            {
                var entries = await db.AccountTransactions.AsNoTracking().Where(x => x.AccountId == account.Id).OrderBy(x => x.PostingDate).ThenBy(x => x.Id).ToListAsync(ct);
                change = entries.Count == 0 ? 0 : entries.OrderBy(x => x.Id).First().LedgerBalanceBefore +
                    entries.Sum(x => x.EntryType == EntryType.Credit ? x.Amount : -x.Amount) - account.LedgerBalance;
            }
            if (account.LedgerBalance + change < 0 || account.AvailableBalance + change < 0)
                throw new BusinessRuleException(MessageCode.InsufficientBalance);
            impacts.Add(new(account.Id, account.LedgerBalance, change, account.LedgerBalance + change));
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            original.Id, original.TransactionStatus, original.Amount, original.UpdatedAt,
            Accounts = accounts.Select(x => new { x.Id, x.AccountId, x.EntryType, x.Amount, x.PostingDate, x.LedgerBalanceBefore }),
            Journal = journal.Select(x => new { x.Id, x.GlAccountId, x.CustomerAccountId, x.EntryType, x.Amount, x.PostingDate }),
            Balances = balances.Select(x => new { x.Id, x.Version, x.LedgerBalance, x.AvailableBalance }), Input = input
        }))));
        var amount = lines.Count == 0 ? impacts.Sum(x => Math.Abs(x.Change)) : lines.Max(x => x.Amount);
        return new(input.Kind, postingDate, amount,
            balanceRepair ? "Rebuild derived balances from verified account movements; original snapshots remain intact." :
            journalRepair ? "Append the missing journal components and validate them together with the original journal." :
            "Apply only the displayed differences. Customer and GL effects are shown separately.", hash, lines, impacts, journalRepair, balanceRepair);
    }

    // A wholly absent journal is reconstructed from the known customer postings and the evidenced offset account.
    private static List<CorrectionLine> CorrectMissingGlPosting(Context c)
    {
        if (c.Journal.Count != 0 || c.Accounts.Count == 0) throw new BusinessRuleException(MessageCode.InvalidRequest);
        var lines = c.Accounts.Select(x => new CorrectionLine(GeneralLedger, c.DepositGlId, x.AccountId, x.EntryType, x.Amount, c.Exception.BusinessDate)).ToList();
        var net = lines.Sum(GlEffect);
        if (net != 0) lines.Add(Gl(c.Input.OffsetGlAccountId, net > 0 ? EntryType.Credit : EntryType.Debit, Math.Abs(net), c));
        return lines;
    }

    // Use the customer-linked deposit liability as evidence; do not repeat its existing GL effect.
    private static List<CorrectionLine> CorrectMissingAccountPosting(Context c)
    {
        var lines = new List<CorrectionLine>();
        foreach (var group in c.Journal.Where(x => x.GlAccountId == c.DepositGlId && x.CustomerAccountId.HasValue).GroupBy(x => x.CustomerAccountId!.Value))
        {
            decimal delta = group.Sum(x => x.EntryType == EntryType.Credit ? x.Amount : -x.Amount) -
                c.Accounts.Where(x => x.AccountId == group.Key).Sum(x => x.EntryType == EntryType.Credit ? x.Amount : -x.Amount);
            AddCustomerDelta(lines, group.Key, delta, c.Exception.BusinessDate);
        }
        return lines;
    }

    // Selected records define which side is wrong; an unselected side is never posted again.
    private static List<CorrectionLine> CorrectPostingAmount(Context c)
    {
        if (c.Input.CorrectAmount is not { } amount || amount < 0 || decimal.Round(amount, 2) != amount)
            throw new ValidationException(MessageCode.InvalidAmount);
        var lines = new List<CorrectionLine>();
        if (c.Input.AccountEntryId.HasValue)
        {
            var entry = AccountEntry(c);
            AddCustomerDelta(lines, entry.AccountId, (amount - entry.Amount) * (entry.EntryType == EntryType.Credit ? 1 : -1), c.Exception.BusinessDate);
        }
        if (c.Input.GlEntryId.HasValue)
        {
            var entry = JournalEntry(c);
            var delta = amount - entry.Amount;
            if (delta != 0)
            {
                var direction = delta > 0 ? entry.EntryType : Opposite(entry.EntryType);
                lines.Add(new(GeneralLedger, entry.GlAccountId, entry.CustomerAccountId, direction, Math.Abs(delta), c.Exception.BusinessDate));
                lines.Add(Gl(c.Input.OffsetGlAccountId, Opposite(direction), Math.Abs(delta), c));
            }
        }
        return lines;
    }

    // Move only selected erroneous attributions; both sides may be selected when both are wrong.
    private static List<CorrectionLine> CorrectCustomerAccount(Context c)
    {
        var target = c.Input.CorrectAccountId ?? throw new ValidationException(MessageCode.RequiredFieldMissing);
        var lines = new List<CorrectionLine>();
        if (c.Input.AccountEntryId.HasValue)
        {
            var entry = AccountEntry(c);
            if (entry.AccountId == target) throw new ValidationException(MessageCode.InvalidRequest);
            lines.Add(new(CustomerLedger, entry.AccountId, null, Opposite(entry.EntryType), entry.Amount, c.Exception.BusinessDate));
            lines.Add(new(CustomerLedger, target, null, entry.EntryType, entry.Amount, c.Exception.BusinessDate));
        }
        if (c.Input.GlEntryId.HasValue)
        {
            var entry = JournalEntry(c);
            if (entry.CustomerAccountId == target || entry.GlAccountId != c.DepositGlId) throw new ValidationException(MessageCode.InvalidRequest);
            lines.Add(new(GeneralLedger, entry.GlAccountId, entry.CustomerAccountId, Opposite(entry.EntryType), entry.Amount, c.Exception.BusinessDate));
            lines.Add(new(GeneralLedger, entry.GlAccountId, target, entry.EntryType, entry.Amount, c.Exception.BusinessDate));
        }
        return lines;
    }

    // A reclassification is a balanced journal and has no customer balance effect.
    private static List<CorrectionLine> ReclassifyGlPosting(Context c)
    {
        var entry = JournalEntry(c);
        var target = c.Input.CorrectGlAccountId ?? throw new ValidationException(MessageCode.RequiredFieldMissing);
        if (target == entry.GlAccountId) throw new ValidationException(MessageCode.InvalidRequest);
        return [new(GeneralLedger, entry.GlAccountId, entry.CustomerAccountId, Opposite(entry.EntryType), entry.Amount, c.Exception.BusinessDate),
            new(GeneralLedger, target, target == c.DepositGlId ? c.Input.CorrectAccountId ?? entry.CustomerAccountId : null,
                entry.EntryType, entry.Amount, c.Exception.BusinessDate)];
    }

    // With no selected component, offset the complete duplicate transaction.
    private static List<CorrectionLine> CorrectDuplicatePosting(Context c)
    {
        var lines = new List<CorrectionLine>();
        foreach (var entry in c.Accounts.Where(x => c.Input.AccountEntryId.HasValue ? x.Id == c.Input.AccountEntryId : !c.Input.GlEntryId.HasValue))
            lines.Add(new(CustomerLedger, entry.AccountId, null, Opposite(entry.EntryType), entry.Amount, c.Exception.BusinessDate));
        foreach (var entry in c.Journal.Where(x => c.Input.GlEntryId.HasValue ? x.Id == c.Input.GlEntryId : !c.Input.AccountEntryId.HasValue))
            lines.Add(new(GeneralLedger, entry.GlAccountId, entry.CustomerAccountId, Opposite(entry.EntryType), entry.Amount, c.Exception.BusinessDate));
        return lines;
    }

    // Offset and replace the selected direction: the corrective effect is twice the original amount.
    private static List<CorrectionLine> CorrectPostingDirection(Context c) =>
        CorrectDuplicatePosting(c).Select(x => x with { Amount = x.Amount * 2 }).ToList();

    // A date correction offsets selected effects on their original date and reinstates them on the evidenced date.
    private static List<CorrectionLine> CorrectPostingDateMismatch(Context c)
    {
        var target = c.Input.CorrectPostingDate ?? throw new ValidationException(MessageCode.RequiredFieldMissing);
        var lines = new List<CorrectionLine>();
        foreach (var entry in c.Accounts.Where(x => c.Input.AccountEntryId.HasValue && x.Id == c.Input.AccountEntryId))
        {
            if (target == entry.PostingDate) throw new ValidationException(MessageCode.InvalidRequest);
            lines.Add(new(CustomerLedger, entry.AccountId, null, Opposite(entry.EntryType), entry.Amount, entry.PostingDate));
            lines.Add(new(CustomerLedger, entry.AccountId, null, entry.EntryType, entry.Amount, target));
        }
        foreach (var entry in c.Journal.Where(x => !c.Input.AccountEntryId.HasValue))
        {
            if (target == entry.PostingDate) throw new ValidationException(MessageCode.InvalidRequest);
            lines.Add(new(GeneralLedger, entry.GlAccountId, entry.CustomerAccountId, Opposite(entry.EntryType), entry.Amount, entry.PostingDate));
            lines.Add(new(GeneralLedger, entry.GlAccountId, entry.CustomerAccountId, entry.EntryType, entry.Amount, target));
        }
        return lines;
    }

    // Repair the proven missing side; validate the effective journal rather than pretending the component is balanced.
    private static List<CorrectionLine> RepairIncompleteJournal(Context c)
    {
        decimal net = c.Journal.Sum(x => x.EntryType == EntryType.Debit ? x.Amount : -x.Amount);
        if (net == 0) throw new BusinessRuleException(MessageCode.InvalidRequest);
        return [new(GeneralLedger, c.Input.CorrectGlAccountId ?? throw new ValidationException(MessageCode.RequiredFieldMissing),
            c.Input.CorrectAccountId, net > 0 ? EntryType.Credit : EntryType.Debit, Math.Abs(net), c.Exception.BusinessDate)];
    }

    // Snapshot repair produces calculation versions, not monetary journal lines.
    private static List<CorrectionLine> RepairAccountBalanceSnapshot(Context c)
    {
        if (!c.Exception.AccountId.HasValue) throw new ValidationException(MessageCode.InvalidRequest);
        return [];
    }

    // Opening errors use the same explicit amount correction, restricted to the account's first posting.
    private List<CorrectionLine> CorrectOpeningBalance(Context c)
    {
        var entry = AccountEntry(c);
        if (db.AccountTransactions.Any(x => x.AccountId == entry.AccountId &&
            (x.PostingDate < entry.PostingDate || (x.PostingDate == entry.PostingDate && x.Id < entry.Id))))
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        return CorrectPostingAmount(c);
    }

    // Reject unsafe or incomplete posting plans before either submission or approval.
    private async Task ValidateLinesAsync(Context c, List<CorrectionLine> lines, bool repair, bool balance,
        DateOnly postingDate, CancellationToken ct)
    {
        if ((!balance && lines.Count == 0) || lines.Any(x => x.Amount <= 0 || decimal.Round(x.Amount, 2) != x.Amount))
            throw new ValidationException(MessageCode.InvalidAmount);
        foreach (DateOnly date in lines.Select(x => x.PostingDate).Append(postingDate).Distinct())
            if (!await db.BusinessDates.AnyAsync(x => x.Date == date && x.Status == OperationsConstants.BusinessDateOpen, ct))
                throw new BusinessRuleException(MessageCode.BusinessDateClosed);
        foreach (var id in lines.Where(x => x.Ledger == GeneralLedger).Select(x => x.AccountId).Distinct())
            if (!await db.GlAccounts.AnyAsync(x => x.Id == id && x.Status == AccountingConstants.ActiveGlAccountStatus, ct))
                throw new ValidationException(MessageCode.InvalidRequest);
        foreach (var id in lines.Where(x => x.Ledger == CustomerLedger).Select(x => x.AccountId)
            .Concat(lines.Where(x => x.CustomerAccountId.HasValue).Select(x => x.CustomerAccountId!.Value)).Distinct())
            if (!await db.Accounts.AnyAsync(x => x.Id == id && x.ClosedAt == null, ct))
                throw new BusinessRuleException(MessageCode.InvalidRequest);
        if (lines.Any(x => x.Ledger == GeneralLedger && x.AccountId == c.DepositGlId && x.CustomerAccountId == null))
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        decimal net = lines.Where(x => x.Ledger == GeneralLedger).Sum(GlEffect);
        if (repair) net += c.Journal.Sum(x => x.EntryType == EntryType.Debit ? x.Amount : -x.Amount);
        if (net != 0) throw new BusinessRuleException(MessageCode.TransactionEntriesUnbalanced);
        if (!repair && c.Journal.Count > 0 && c.Journal.Sum(x => x.EntryType == EntryType.Debit ? x.Amount : -x.Amount) != 0)
            throw new BusinessRuleException(MessageCode.TransactionEntriesUnbalanced);
        if (c.Original.TransactionType is TransactionType.InterbankTransfer or TransactionType.NrcTransfer &&
            c.Original.TransactionStatus == TransactionStatus.Completed && string.IsNullOrWhiteSpace(c.Input.ExternalRecoveryReference))
            throw new ValidationException(MessageCode.RequiredFieldMissing);
    }

    private static AccountTransaction AccountEntry(Context c) => c.Accounts.SingleOrDefault(x => x.Id == c.Input.AccountEntryId)
        ?? throw new ValidationException(MessageCode.RequiredFieldMissing);
    private static TransactionEntry JournalEntry(Context c) => c.Journal.SingleOrDefault(x => x.Id == c.Input.GlEntryId)
        ?? throw new ValidationException(MessageCode.RequiredFieldMissing);
    private static CorrectionLine Gl(long? id, EntryType direction, decimal amount, Context c) =>
        new(GeneralLedger, id ?? throw new ValidationException(MessageCode.RequiredFieldMissing), null, direction, amount, c.Exception.BusinessDate);
    private static void AddCustomerDelta(List<CorrectionLine> lines, long id, decimal delta, DateOnly date)
    {
        if (delta != 0) lines.Add(new(CustomerLedger, id, null, delta > 0 ? EntryType.Credit : EntryType.Debit, Math.Abs(delta), date));
    }
    public static EntryType Opposite(EntryType type) => type == EntryType.Debit ? EntryType.Credit : EntryType.Debit;
    public static decimal CustomerEffect(CorrectionLine line) => line.Direction == EntryType.Credit ? line.Amount : -line.Amount;
    public static decimal GlEffect(CorrectionLine line) => line.Direction == EntryType.Debit ? line.Amount : -line.Amount;
}

