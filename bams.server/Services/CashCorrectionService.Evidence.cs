using System.Text.Json;
using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Transactions;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed partial class CashCorrectionService
{
    private const int SourceOptionLimit = 500;

    // Catalogs are scoped to the assigned exception and its source date; IDs never authorize a correction.
    public async Task<CashSourceCatalog> SourcesAsync(long exceptionId, string? search, CancellationToken ct)
    {
        await checks.DetailAsync(exceptionId, ct);
        ReconciliationException exception = await db.ReconciliationExceptions.AsNoTracking().SingleAsync(x => x.Id == exceptionId, ct);
        if (!exception.PositionSessionId.HasValue) throw new ValidationException(MessageCode.InvalidRequest);
        string term = search?.Trim() ?? "";
        long.TryParse(term, out long selectedId);
        List<Account> accounts = await db.Accounts.AsNoTracking().Where(x => x.Status == AccountStatus.Active &&
            (term == "" || x.AccountNo.Contains(term) || x.Id == selectedId)).OrderBy(x => x.AccountNo).Take(SourceOptionLimit).ToListAsync(ct);
        List<CashPositionSession> sessions = await db.CashPositionSessions.AsNoTracking().Where(x => x.BusinessDate == exception.BusinessDate &&
            (term == "" || x.Id == exception.PositionSessionId || x.Id == selectedId || x.PositionType.Contains(term) ||
             db.CashMovements.Any(m => m.Id == selectedId && (m.SessionId == x.Id || m.DestinationSessionId == x.Id)) ||
             db.CashHandoffs.Any(h => h.Id == selectedId && (h.SessionId == x.Id || h.DestinationSessionId == x.Id))))
            .OrderByDescending(x => x.Id == exception.PositionSessionId).ThenBy(x => x.Id).Take(SourceOptionLimit).ToListAsync(ct);
        List<long> sessionIds = sessions.Select(x => x.Id).ToList();
        List<CashMovement> movements = await db.CashMovements.AsNoTracking().Where(x => sessionIds.Contains(x.SessionId) &&
            x.Status == OperationsConstants.CashMovementApproved && !x.CorrectsMovementId.HasValue && !x.CashCorrectionProposalId.HasValue &&
            !db.CashMovements.Any(correction => correction.CorrectsMovementId == x.Id) &&
            (term == "" || x.Id == selectedId || x.SessionId == selectedId || x.TransactionId == selectedId || x.Type.Contains(term)))
            .OrderByDescending(x => x.Id).Take(SourceOptionLimit).ToListAsync(ct);
        List<Transaction> transactions = await db.Transactions.AsNoTracking().Where(x => x.BusinessDate == exception.BusinessDate &&
            x.TransactionStatus == TransactionStatus.Completed && !db.CashMovements.Any(m => m.TransactionId == x.Id) &&
            db.TransactionEntries.Any(e => e.TransactionId == x.Id && e.GlAccount!.Code == AccountingConstants.CashOnHandGlCode) &&
            (term == "" || x.Id == selectedId || x.TransactionNo.Contains(term)))
            .OrderByDescending(x => x.Id).Take(SourceOptionLimit).ToListAsync(ct);
        List<CashHandoff> handoffs = await db.CashHandoffs.AsNoTracking().Where(x => x.BusinessDate == exception.BusinessDate &&
            x.Status == OperationsConstants.CashHandoffAccepted && (term == "" || x.Id == selectedId || x.SessionId == selectedId))
            .OrderByDescending(x => x.Id).Take(SourceOptionLimit).ToListAsync(ct);
        List<long> transactionIds = transactions.Select(x => x.Id).Concat(movements.Where(x => x.TransactionId.HasValue).Select(x => x.TransactionId!.Value)).Distinct().ToList();
        List<CashSourceJournalLine> journal = await db.TransactionEntries.AsNoTracking().Where(x => transactionIds.Contains(x.TransactionId))
            .OrderBy(x => x.TransactionId).ThenBy(x => x.Id)
            .Select(x => new CashSourceJournalLine(x.TransactionId, x.GlAccount!.Code, x.EntryType.ToString(), x.Amount)).ToListAsync(ct);
        return new(exceptionId, exception.PositionSessionId.Value,
            accounts.Select(x => new CashSourceOption(x.Id, $"{x.AccountNo} · account #{x.Id} · balance {x.LedgerBalance:N2}", Amount: x.LedgerBalance)).ToList(),
            transactions.Select(x => new CashSourceOption(x.Id, $"{x.TransactionNo} · {x.TransactionType} · {x.Amount:N2}", Amount: x.Amount)).ToList(),
            movements.Select(x => new CashSourceOption(x.Id, $"#{x.Id} · {x.Type} · {x.Amount:N2} · session #{x.SessionId} → #{x.DestinationSessionId}", x.SessionId, x.Amount, x.TransactionId, x.DestinationSessionId, x.Type)).ToList(),
            sessions.Select(x => new CashSourceOption(x.Id, $"#{x.Id} · {x.PositionType} · {x.Status} · expected {x.ExpectedClosingCash:N2}", x.Id, x.ExpectedClosingCash)).ToList(),
            handoffs.Select(x => new CashSourceOption(x.Id, $"#{x.Id} · accepted {x.Amount:N2} · session #{x.SessionId} → #{x.DestinationSessionId}", x.SessionId, x.Amount, DestinationSessionId: x.DestinationSessionId)).ToList()) { JournalLines = journal };
    }

    public async Task<CashProposalEvidence> ProposalEvidenceAsync(long proposalId, CancellationToken ct)
    {
        CashCorrectionProposal proposal = await db.CashCorrectionProposals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == proposalId, ct)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        CashCorrectionInputs input = JsonSerializer.Deserialize<CashCorrectionInputs>(proposal.InputsJson)!;
        CashCorrectionPreview? preview = null;
        string? warning = null;
        try { preview = await PreviewAsync(proposal.ExceptionId, input, ct); }
        catch (AppException failure) { warning = failure.Message; }
        bool unchanged = preview?.SourceHash == proposal.SourceHash;
        long? sourceTransactionId = input.TransactionId;
        if (input.MovementId.HasValue)
            sourceTransactionId = await db.CashMovements.AsNoTracking().Where(x => x.Id == input.MovementId).Select(x => x.TransactionId).SingleOrDefaultAsync(ct);
        List<CashSourceJournalLine> original = sourceTransactionId.HasValue
            ? await db.TransactionEntries.AsNoTracking().Where(x => x.TransactionId == sourceTransactionId).OrderBy(x => x.Id)
                .Select(x => new CashSourceJournalLine(x.TransactionId, x.GlAccount!.Code, x.EntryType.ToString(), x.Amount)).ToListAsync(ct) : [];
        return new(ToResponse(proposal), preview, unchanged,
            warning ?? (unchanged ? null : MessageCatalog.GetMessage(MessageCode.CorrectionProposalStale))) { OriginalJournal = original };
    }

    private static IReadOnlyList<CashPlannedJournalLine> PlannedJournal(CashCorrectionInputs input, CorrectionPlan plan, decimal change)
    {
        if (!CreatesFinancialTransaction(input.Kind)) return [];
        string offset = input.Kind switch
        {
            CashDiscrepancyKind.BankExpenseNotRecorded => AccountingConstants.BankOperatingExpenseGlCode,
            CashDiscrepancyKind.UnexplainedOverShort => AccountingConstants.CashOverShortGlCode,
            _ => AccountingConstants.CustomerDepositsGlCode
        };
        return [new(AccountingConstants.CashOnHandGlCode, change > 0m ? "Debit" : "Credit", plan.Amount),
            new(offset, change > 0m ? "Credit" : "Debit", plan.Amount, input.AccountId)];
    }

    private async Task<IReadOnlyList<CashPositionCorrectionEffect>> PositionEffectsAsync(CorrectionPlan plan,
        CashCorrectionInputs input, CancellationToken ct)
    {
        List<CashPositionCorrectionEffect> effects = [];
        foreach (MovementEffect effect in plan.Effects)
        {
            CashPositionSession session = await db.CashPositionSessions.AsNoTracking().SingleAsync(x => x.Id == effect.SessionId, ct);
            CashCount? count = await db.CashCounts.AsNoTracking().Where(x => x.SessionId == session.Id).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            decimal? beforeCounted = count is null ? null : await checks.CountedCashAfterHandoffsAsync(session.Id, count, ct);
            decimal? afterCounted = beforeCounted;
            if (input.Kind == CashDiscrepancyKind.CountEntryError) afterCounted = plan.Amount;
            else if (count is not null && effect.SourceMovementId.HasValue)
            {
                CashMovement source = await db.CashMovements.AsNoTracking().SingleAsync(x => x.Id == effect.SourceMovementId, ct);
                if (IsCustodyMovement(source.Type) && source.CreatedAtUtc > count.CountedAtUtc) afterCounted += effect.Change;
            }
            effects.Add(new(session.Id, effect.Change) { BeforeExpected = session.ExpectedClosingCash,
                AfterExpected = session.ExpectedClosingCash + effect.Change, BeforeCounted = beforeCounted, AfterCounted = afterCounted });
        }
        return effects;
    }
}
