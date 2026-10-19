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
    private sealed record MovementEffect(long SessionId, decimal Change, long? SourceMovementId = null,
        long? DestinationSessionId = null, long? TransactionId = null);
    private sealed record CorrectionPlan(decimal Amount, IReadOnlyList<MovementEffect> Effects);

    // Each discrepancy has its own planner; only the immutable movement append operation is shared.
    private Task<CorrectionPlan> PlanCorrectionAsync(ReconciliationException exception, CashPositionSession session,
        CashCorrectionInputs input, CancellationToken ct) => input.Kind switch
    {
        CashDiscrepancyKind.CountEntryError => PlanCountEntryErrorAsync(session, input, ct),
        CashDiscrepancyKind.UnrecordedDeposit => PlanUnrecordedDepositAsync(exception, session, input, ct),
        CashDiscrepancyKind.UnrecordedWithdrawal => PlanUnrecordedWithdrawalAsync(exception, session, input, ct),
        CashDiscrepancyKind.MissingMovement => PlanMissingMovementAsync(exception, session, input, ct),
        CashDiscrepancyKind.DuplicateMovement => PlanDuplicateMovementAsync(session, input, ct),
        CashDiscrepancyKind.IncorrectMovementAmount => PlanIncorrectMovementAmountAsync(session, input, ct),
        CashDiscrepancyKind.WrongCashPosition => PlanWrongCashPositionAsync(session, input, ct),
        CashDiscrepancyKind.TransferMismatch => PlanTransferMismatchAsync(session, input, ct),
        CashDiscrepancyKind.HandoffMismatch => PlanHandoffMismatchAsync(session, input, ct),
        CashDiscrepancyKind.UnexplainedOverShort => PlanUnexplainedOverShortAsync(exception, session, input, ct),
        CashDiscrepancyKind.BankExpenseNotRecorded => PlanBankExpenseAsync(exception, session, input, ct),
        _ => throw new ValidationException(MessageCode.InvalidRequest)
    };

    private static Task<CorrectionPlan> PlanCountEntryErrorAsync(CashPositionSession session,
        CashCorrectionInputs input, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        decimal amount = RequireAmount(input.Amount, allowZero: true);
        return Task.FromResult(new CorrectionPlan(amount, [new(session.Id, 0m)]));
    }

    private async Task<CorrectionPlan> PlanUnrecordedDepositAsync(ReconciliationException exception,
        CashPositionSession session, CashCorrectionInputs input, CancellationToken ct)
    {
        decimal amount = RequireAmount(input.Amount ?? Math.Abs(exception.Difference));
        await ValidateCustomerCashAsync(input.AccountId, amount, ct);
        return await ValidateEffectsAsync(new(amount, [new(session.Id, amount)]), ct);
    }

    private async Task<CorrectionPlan> PlanUnrecordedWithdrawalAsync(ReconciliationException exception,
        CashPositionSession session, CashCorrectionInputs input, CancellationToken ct)
    {
        decimal amount = RequireAmount(input.Amount ?? Math.Abs(exception.Difference));
        await ValidateCustomerCashAsync(input.AccountId, -amount, ct);
        return await ValidateEffectsAsync(new(amount, [new(session.Id, -amount)]), ct);
    }

    private async Task ValidateCustomerCashAsync(long? accountId, decimal change, CancellationToken ct)
    {
        Account? account = accountId.HasValue
            ? await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId, ct) : null;
        if (account is null || account.Status != AccountStatus.Active)
            throw new ValidationException(MessageCode.InvalidRequest);
        if (account.LedgerBalance + change < 0m || account.AvailableBalance + change < 0m)
            throw new BusinessRuleException(MessageCode.InsufficientBalance);
    }

    private async Task<CorrectionPlan> PlanMissingMovementAsync(ReconciliationException exception,
        CashPositionSession session, CashCorrectionInputs input, CancellationToken ct)
    {
        if (!input.TransactionId.HasValue ||
            exception.RelatedTransactionId.HasValue && exception.RelatedTransactionId != input.TransactionId ||
            await db.CashMovements.AnyAsync(x => x.TransactionId == input.TransactionId, ct))
            throw new ValidationException(MessageCode.InvalidRequest);
        decimal change = await RequireCashJournalAsync(input.TransactionId.Value, session.BusinessDate, ct);
        return await ValidateEffectsAsync(new(Math.Abs(change), [new(session.Id, change, TransactionId: input.TransactionId)]), ct);
    }

    private async Task<CorrectionPlan> PlanDuplicateMovementAsync(CashPositionSession session,
        CashCorrectionInputs input, CancellationToken ct)
    {
        CashMovement duplicate = await RequireSourceMovementAsync(session, input.MovementId, ct);
        CashMovement retained = await RequireSourceMovementAsync(session, input.CounterpartMovementId, ct);
        // The investigator identifies the retained record; approval never guesses a duplicate from timestamps.
        if (duplicate.Id == retained.Id || duplicate.Type != retained.Type || duplicate.Amount != retained.Amount ||
            duplicate.TransactionId != retained.TransactionId || duplicate.DestinationSessionId != retained.DestinationSessionId ||
            !duplicate.TransactionId.HasValue && !IsCustodyMovement(duplicate.Type))
            throw new ValidationException(MessageCode.InvalidRequest);
        if (duplicate.TransactionId.HasValue &&
            await RequireCashJournalAsync(duplicate.TransactionId.Value, session.BusinessDate, ct) != SignedMovement(retained))
            throw new ValidationException(MessageCode.InvalidRequest);
        return await ValidateEffectsAsync(new(duplicate.Amount,
            [new(session.Id, -SignedMovement(duplicate), duplicate.Id)]), ct);
    }

    private async Task<CorrectionPlan> PlanIncorrectMovementAmountAsync(CashPositionSession session,
        CashCorrectionInputs input, CancellationToken ct)
    {
        CashMovement movement = await RequireSourceMovementAsync(session, input.MovementId, ct);
        decimal amount = RequireAmount(input.Amount, allowZero: true);
        if (IsCustodyMovement(movement.Type) || !movement.TransactionId.HasValue)
            throw new ValidationException(MessageCode.InvalidRequest);
        decimal correct = SignedMovement(movement) > 0m ? amount : -amount;
        // Correct a movement against an already balanced journal; journal errors belong to account/GL correction.
        if (await RequireCashJournalAsync(movement.TransactionId.Value, session.BusinessDate, ct) != correct)
            throw new ValidationException(MessageCode.InvalidRequest);
        return await ValidateEffectsAsync(new(amount, [new(session.Id, correct - SignedMovement(movement), movement.Id)]), ct);
    }

    private async Task<CorrectionPlan> PlanWrongCashPositionAsync(CashPositionSession session,
        CashCorrectionInputs input, CancellationToken ct)
    {
        CashMovement movement = await RequireSourceMovementAsync(session, input.MovementId, ct);
        CashPositionSession destination = await RequireDestinationAsync(session, input.DestinationSessionId, ct);
        if (IsCustodyMovement(movement.Type) || !movement.TransactionId.HasValue)
            throw new ValidationException(MessageCode.InvalidRequest);
        decimal original = SignedMovement(movement);
        if (await RequireCashJournalAsync(movement.TransactionId.Value, session.BusinessDate, ct) != original)
            throw new ValidationException(MessageCode.InvalidRequest);
        return await ValidateEffectsAsync(new(movement.Amount,
            [new(session.Id, -original, movement.Id, destination.Id),
             new(destination.Id, original, movement.Id, session.Id)]), ct);
    }

    private async Task<CorrectionPlan> PlanTransferMismatchAsync(CashPositionSession session,
        CashCorrectionInputs input, CancellationToken ct)
    {
        CashMovement movement = await RequireSourceMovementAsync(session, input.MovementId, ct);
        CashPositionSession destination = await RequireDestinationAsync(session, input.DestinationSessionId, ct);
        CashMovement counterpart = await RequireSourceMovementAsync(destination, input.CounterpartMovementId, ct);
        bool supportedPair = (movement.Type, counterpart.Type) is
            ("TransferOut", "TransferIn") or ("TransferIn", "TransferOut") or
            ("VaultIssue", "CashReceipt") or ("CashReceipt", "VaultIssue");
        if (!supportedPair) throw new ValidationException(MessageCode.InvalidRequest);
        return await PlanCustodyPairAsync(session, destination, movement, counterpart, input, ct);
    }

    private async Task<CorrectionPlan> PlanHandoffMismatchAsync(CashPositionSession session,
        CashCorrectionInputs input, CancellationToken ct)
    {
        CashMovement movement = await RequireSourceMovementAsync(session, input.MovementId, ct);
        CashPositionSession destination = await RequireDestinationAsync(session, input.DestinationSessionId, ct);
        CashMovement counterpart = await RequireSourceMovementAsync(destination, input.CounterpartMovementId, ct);
        CashHandoff? handoff = input.HandoffId.HasValue
            ? await db.CashHandoffs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.HandoffId, ct) : null;
        CashMovement outgoing = movement.Type == "VaultReturnOut" ? movement : counterpart;
        CashMovement incoming = movement.Type == "VaultReturnIn" ? movement : counterpart;
        if (handoff is null || handoff.Status != OperationsConstants.CashHandoffAccepted || !handoff.AcceptedAtUtc.HasValue ||
            outgoing.Type != "VaultReturnOut" || incoming.Type != "VaultReturnIn" ||
            handoff.SessionId != outgoing.SessionId || handoff.DestinationSessionId != incoming.SessionId ||
            handoff.BusinessDate != session.BusinessDate || handoff.Amount != outgoing.Amount || handoff.Amount != incoming.Amount ||
            !await db.CashCounts.AnyAsync(x => x.Id == handoff.CashCountId && x.SessionId == outgoing.SessionId, ct))
            throw new ValidationException(MessageCode.InvalidRequest);
        return await PlanCustodyPairAsync(session, destination, movement, counterpart, input, ct);
    }

    private async Task<CorrectionPlan> PlanCustodyPairAsync(CashPositionSession source, CashPositionSession destination,
        CashMovement movement, CashMovement counterpart, CashCorrectionInputs input, CancellationToken ct)
    {
        if (movement.Id == counterpart.Id || movement.DestinationSessionId != destination.Id ||
            counterpart.DestinationSessionId != source.Id || movement.TransactionId.HasValue || counterpart.TransactionId.HasValue)
            throw new ValidationException(MessageCode.InvalidRequest);
        decimal amount = RequireAmount(input.Amount, allowZero: true);
        decimal correctSource = SignedMovement(movement) > 0m ? amount : -amount;
        return await ValidateEffectsAsync(new(amount,
            [new(source.Id, correctSource - SignedMovement(movement), movement.Id, destination.Id),
             new(destination.Id, -correctSource - SignedMovement(counterpart), counterpart.Id, source.Id)]), ct);
    }

    private async Task<CorrectionPlan> PlanUnexplainedOverShortAsync(ReconciliationException exception,
        CashPositionSession session, CashCorrectionInputs input, CancellationToken ct)
    {
        decimal amount = RequireAmount(input.Amount ?? Math.Abs(exception.Difference));
        if (amount != Math.Abs(exception.Difference)) throw new ValidationException(MessageCode.InvalidAmount);
        return await ValidateEffectsAsync(new(amount, [new(session.Id, exception.Difference)]), ct);
    }

    private async Task<CorrectionPlan> PlanBankExpenseAsync(ReconciliationException exception,
        CashPositionSession session, CashCorrectionInputs input, CancellationToken ct)
    {
        decimal amount = RequireAmount(input.Amount ?? Math.Abs(exception.Difference));
        BankExpenseService.Validate(new(session.Id, amount, input.Payee ?? "", input.Purpose ?? "",
            input.ReceiptFileName, input.ReceiptContentType, input.ReceiptBase64, input.MissingReceiptReason));
        return await ValidateEffectsAsync(new(amount, [new(session.Id, -amount)]), ct);
    }

    private async Task<CashMovement> RequireSourceMovementAsync(CashPositionSession session, long? id, CancellationToken ct)
    {
        CashMovement? movement = id.HasValue
            ? await db.CashMovements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SessionId == session.Id &&
                x.Status == OperationsConstants.CashMovementApproved, ct) : null;
        if (movement is null || movement.CorrectsMovementId.HasValue || movement.CashCorrectionProposalId.HasValue ||
            await db.CashMovements.AnyAsync(x => x.CorrectsMovementId == movement.Id, ct))
            throw new ValidationException(MessageCode.InvalidRequest);
        _ = SignedMovement(movement);
        return movement;
    }

    private async Task<CashPositionSession> RequireDestinationAsync(CashPositionSession source, long? id, CancellationToken ct)
    {
        CashPositionSession? destination = id.HasValue
            ? await db.CashPositionSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) : null;
        if (destination is null || destination.Id == source.Id || destination.BusinessDate != source.BusinessDate)
            throw new ValidationException(MessageCode.InvalidRequest);
        return destination;
    }

    private async Task<decimal> RequireCashJournalAsync(long transactionId, DateOnly date, CancellationToken ct)
    {
        Transaction? transaction = await db.Transactions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == transactionId, ct);
        if (transaction is null || transaction.TransactionStatus != TransactionStatus.Completed || transaction.BusinessDate != date)
            throw new ValidationException(MessageCode.InvalidRequest);
        await ledger.ValidateTransactionAccountingEntriesAsync(transactionId, ct);
        List<TransactionEntry> entries = await db.TransactionEntries.AsNoTracking().Where(x => x.TransactionId == transactionId &&
            x.PostingDate == date && x.GlAccount!.Code == AccountingConstants.CashOnHandGlCode).ToListAsync(ct);
        decimal change = entries.Sum(x => x.EntryType == EntryType.Debit ? x.Amount : -x.Amount);
        if (change == 0m) throw new ValidationException(MessageCode.InvalidRequest);
        return change;
    }

    private async Task<CorrectionPlan> ValidateEffectsAsync(CorrectionPlan plan, CancellationToken ct)
    {
        if (plan.Effects.All(effect => effect.Change == 0m)) throw new ValidationException(MessageCode.InvalidAmount);
        foreach (MovementEffect effect in plan.Effects)
        {
            CashPositionSession session = await db.CashPositionSessions.AsNoTracking().SingleAsync(x => x.Id == effect.SessionId, ct);
            if (session.ExpectedClosingCash + effect.Change < session.ReservedCash)
                throw new BusinessRuleException(MessageCode.InsufficientCashPositionBalance);
        }
        return plan;
    }

    private static decimal RequireAmount(decimal? value, bool allowZero = false)
    {
        if (!value.HasValue || value < 0m || !allowZero && value == 0m || decimal.Round(value.Value, 2) != value.Value)
            throw new ValidationException(MessageCode.InvalidAmount);
        return value.Value;
    }

    private static bool IsCustodyMovement(string type) => type is "TransferIn" or "TransferOut" or
        "VaultReturnIn" or "VaultReturnOut" or "VaultIssue" or "CashReceipt";

    private static decimal SignedMovement(CashMovement movement) => movement.Type switch
    {
        "Deposit" or "CorrectionIn" or "TransferIn" or "VaultReturnIn" or "CashReceipt" => movement.Amount,
        "Withdrawal" or "BankExpense" or "CorrectionOut" or "TransferOut" or "VaultReturnOut" or "VaultIssue" => -movement.Amount,
        _ => throw new ValidationException(MessageCode.InvalidRequest)
    };
}
