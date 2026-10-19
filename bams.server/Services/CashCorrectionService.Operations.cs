using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Transactions;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed partial class CashCorrectionService
{
    private sealed record AccountSourceSnapshot(long Id, AccountStatus Status, decimal LedgerBalance,
        decimal AvailableBalance, long Version);
    private sealed record TransactionSourceSnapshot(long Id, TransactionType Type, TransactionStatus Status,
        decimal Amount, decimal FeeAmount, DateOnly? BusinessDate, DateTime? PostedAt, DateTime UpdatedAt);
    private sealed record MovementSourceSnapshot(long Id, long SessionId, long? DestinationSessionId,
        string Type, string Status, string Amount, long? TransactionId);
    private sealed record HandoffSourceSnapshot(long Id, long SessionId, long? DestinationSessionId,
        long CountId, string Status, string Amount, long Version, DateTime? AcceptedAtUtc);

    private static bool CreatesFinancialTransaction(CashDiscrepancyKind kind) => kind is
        CashDiscrepancyKind.UnrecordedDeposit or CashDiscrepancyKind.UnrecordedWithdrawal or
        CashDiscrepancyKind.UnexplainedOverShort or CashDiscrepancyKind.BankExpenseNotRecorded;

    // Reject unrelated identifiers instead of silently moving money to a hidden destination.
    private static void ValidateCaseInputs(CashCorrectionInputs input)
    {
        bool needsAccount = input.Kind is CashDiscrepancyKind.UnrecordedDeposit or CashDiscrepancyKind.UnrecordedWithdrawal;
        bool needsTransaction = input.Kind == CashDiscrepancyKind.MissingMovement;
        bool needsMovement = input.Kind is CashDiscrepancyKind.DuplicateMovement or CashDiscrepancyKind.IncorrectMovementAmount or
            CashDiscrepancyKind.WrongCashPosition or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch;
        bool needsDestination = input.Kind is CashDiscrepancyKind.WrongCashPosition or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch;
        bool needsCounterpart = input.Kind is CashDiscrepancyKind.DuplicateMovement or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch;
        if (input.AccountId.HasValue != needsAccount || input.TransactionId.HasValue != needsTransaction ||
            input.MovementId.HasValue != needsMovement || input.DestinationSessionId.HasValue != needsDestination ||
            input.CounterpartMovementId.HasValue != needsCounterpart || input.HandoffId.HasValue != (input.Kind == CashDiscrepancyKind.HandoffMismatch))
            throw new ValidationException(MessageCode.InvalidRequest);
    }

    // Approval and submission lock all affected positions in the same order before validating the snapshot.
    private async Task LockCorrectionSourcesAsync(long sourceSessionId, CashCorrectionInputs input, CancellationToken ct)
    {
        long destinationId = input.DestinationSessionId ?? sourceSessionId;
        await db.CashPositionSessions.FromSql($"SELECT * FROM CashPositionSessions WHERE Id = {sourceSessionId} OR Id = {destinationId} ORDER BY Id FOR UPDATE").ToListAsync(ct);
        long movementId = input.MovementId ?? 0;
        long counterpartId = input.CounterpartMovementId ?? 0;
        await db.CashMovements.FromSql($"SELECT * FROM CashMovements WHERE Id = {movementId} OR Id = {counterpartId} ORDER BY Id FOR UPDATE").ToListAsync(ct);
        if (input.HandoffId.HasValue)
            await db.CashHandoffs.FromSql($"SELECT * FROM CashHandoffs WHERE Id = {input.HandoffId} FOR UPDATE").ToListAsync(ct);
        if (input.AccountId.HasValue)
            await db.Accounts.FromSql($"SELECT * FROM Accounts WHERE Id = {input.AccountId} FOR UPDATE").ToListAsync(ct);
        if (input.TransactionId.HasValue)
            await db.Transactions.FromSql($"SELECT * FROM Transactions WHERE Id = {input.TransactionId} FOR UPDATE").ToListAsync(ct);
    }

    private Task ExecuteCountEntryErrorAsync(CashCorrectionProposal proposal, CashPositionSession session,
        CashCorrectionInputs input, CashCorrectionPreview preview, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        db.CashCounts.Add(new CashCount { SessionId = session.Id, ExpectedAmount = session.ExpectedClosingCash,
            ActualAmount = preview.SuggestedAmount, Difference = preview.SuggestedAmount - session.ExpectedClosingCash,
            Notes = $"Approved recount proposal {proposal.Id}: {input.Findings}", CountedBy = proposal.ProposerId,
            CountedAtUtc = DateTime.UtcNow });
        return Task.CompletedTask;
    }

    private Task ExecuteUnrecordedDepositAsync(CashCorrectionProposal proposal, CashPositionSession session,
        CashCorrectionInputs input, CashCorrectionPreview preview, long approver, CancellationToken ct) =>
        PostMissingCustomerCashAsync(proposal, session, input, preview, approver, ct);

    private Task ExecuteUnrecordedWithdrawalAsync(CashCorrectionProposal proposal, CashPositionSession session,
        CashCorrectionInputs input, CashCorrectionPreview preview, long approver, CancellationToken ct) =>
        PostMissingCustomerCashAsync(proposal, session, input, preview, approver, ct);

    private Task ExecuteMissingMovementAsync(CashCorrectionProposal proposal, CorrectionPlan plan,
        CashCorrectionInputs input, long approver, CancellationToken ct) => AppendPlannedMovementsAsync(proposal, plan, input, approver, ct);

    private Task ExecuteDuplicateMovementAsync(CashCorrectionProposal proposal, CorrectionPlan plan,
        CashCorrectionInputs input, long approver, CancellationToken ct) => AppendPlannedMovementsAsync(proposal, plan, input, approver, ct);

    private Task ExecuteIncorrectMovementAmountAsync(CashCorrectionProposal proposal, CorrectionPlan plan,
        CashCorrectionInputs input, long approver, CancellationToken ct) => AppendPlannedMovementsAsync(proposal, plan, input, approver, ct);

    private Task ExecuteWrongCashPositionAsync(CashCorrectionProposal proposal, CorrectionPlan plan,
        CashCorrectionInputs input, long approver, CancellationToken ct) => AppendPlannedMovementsAsync(proposal, plan, input, approver, ct);

    private Task ExecuteTransferMismatchAsync(CashCorrectionProposal proposal, CorrectionPlan plan,
        CashCorrectionInputs input, long approver, CancellationToken ct) => AppendPlannedMovementsAsync(proposal, plan, input, approver, ct);

    private Task ExecuteHandoffMismatchAsync(CashCorrectionProposal proposal, CorrectionPlan plan,
        CashCorrectionInputs input, long approver, CancellationToken ct) => AppendPlannedMovementsAsync(proposal, plan, input, approver, ct);

    private Task ExecuteUnexplainedOverShortAsync(CashCorrectionProposal proposal, CashPositionSession session,
        CashCorrectionPreview preview, long approver, CancellationToken ct) => PostOverShortAsync(proposal, session, preview, approver, ct);

    private async Task ExecuteBankExpenseAsync(CashCorrectionProposal proposal, ReconciliationException exception,
        CashPositionSession session, CashCorrectionInputs input, CashCorrectionPreview preview, long approver, CancellationToken ct)
    {
        BankExpenseRequest expense = BankExpenseService.CreateRequest(new(session.Id, preview.SuggestedAmount,
            input.Payee!, input.Purpose!, input.ReceiptFileName, input.ReceiptContentType,
            input.ReceiptBase64, input.MissingReceiptReason), session.BusinessDate, proposal.ProposerId, exception.Id, proposal.Id);
        expense.Status = "Posted";
        expense.ReviewedBy = approver;
        expense.ReviewedAtUtc = DateTime.UtcNow;
        expense.ReviewNote = proposal.ReviewNote;
        db.BankExpenseRequests.Add(expense);
        await db.SaveChangesAsync(ct);
        await expenses.PostApprovedAsync(expense, session, approver, ct, preview.PostingDate);
        proposal.ExecutionTransactionId = expense.TransactionId;
    }

    // All changes are append-only and carry both their proposal and original movement references.
    private async Task AppendPlannedMovementsAsync(CashCorrectionProposal proposal, CorrectionPlan plan,
        CashCorrectionInputs input, long approver, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        foreach (MovementEffect effect in plan.Effects)
        {
            if (effect.Change == 0m) continue;
            CashPositionSession session = await db.CashPositionSessions.SingleAsync(x => x.Id == effect.SessionId, ct);
            if (session.ExpectedClosingCash + effect.Change < session.ReservedCash)
                throw new BusinessRuleException(MessageCode.InsufficientCashPositionBalance);
            db.CashMovements.Add(new CashMovement { SessionId = session.Id, DestinationSessionId = effect.DestinationSessionId,
                CashCorrectionProposalId = proposal.Id, CorrectsMovementId = effect.SourceMovementId,
                TransactionId = effect.TransactionId, Type = effect.Change > 0m ? "CorrectionIn" : "CorrectionOut",
                Amount = Math.Abs(effect.Change), ActorId = approver, ApprovedBy = approver,
                ApprovedAtUtc = now, CreatedAtUtc = now, Note = $"Proposal {proposal.Id}: {input.Findings}" });
            session.ExpectedClosingCash += effect.Change;
            session.Version++;
        }
    }

    private Task<bool> VerifyCorrectionAsync(CashCorrectionProposal proposal, ReconciliationException exception,
        CashCorrectionInputs input, CancellationToken ct) => proposal.Kind switch
    {
        CashDiscrepancyKind.CountEntryError => VerifyCountEntryErrorAsync(exception, ct),
        CashDiscrepancyKind.UnrecordedDeposit => VerifyUnrecordedDepositAsync(proposal, exception, ct),
        CashDiscrepancyKind.UnrecordedWithdrawal => VerifyUnrecordedWithdrawalAsync(proposal, exception, ct),
        CashDiscrepancyKind.MissingMovement => VerifyMissingMovementAsync(proposal, exception, input, ct),
        CashDiscrepancyKind.DuplicateMovement => VerifyDuplicateMovementAsync(proposal, exception, ct),
        CashDiscrepancyKind.IncorrectMovementAmount => VerifyIncorrectMovementAmountAsync(proposal, exception, ct),
        CashDiscrepancyKind.WrongCashPosition => VerifyWrongCashPositionAsync(proposal, exception, input, ct),
        CashDiscrepancyKind.TransferMismatch => VerifyTransferMismatchAsync(proposal, exception, input, ct),
        CashDiscrepancyKind.HandoffMismatch => VerifyHandoffMismatchAsync(proposal, exception, input, ct),
        CashDiscrepancyKind.UnexplainedOverShort => VerifyUnexplainedOverShortAsync(proposal, exception, ct),
        CashDiscrepancyKind.BankExpenseNotRecorded => VerifyBankExpenseAsync(proposal, exception, ct),
        _ => throw new ValidationException(MessageCode.InvalidRequest)
    };

    private Task<bool> VerifyCountEntryErrorAsync(ReconciliationException exception, CancellationToken ct) =>
        VerifyPositionAsync(exception.PositionSessionId!.Value, ct);

    private Task<bool> VerifyUnrecordedDepositAsync(CashCorrectionProposal proposal, ReconciliationException exception, CancellationToken ct) =>
        VerifyFinancialCorrectionAsync(proposal, exception, ct);
    private Task<bool> VerifyUnrecordedWithdrawalAsync(CashCorrectionProposal proposal, ReconciliationException exception, CancellationToken ct) =>
        VerifyFinancialCorrectionAsync(proposal, exception, ct);
    private Task<bool> VerifyUnexplainedOverShortAsync(CashCorrectionProposal proposal, ReconciliationException exception, CancellationToken ct) =>
        VerifyFinancialCorrectionAsync(proposal, exception, ct);
    private Task<bool> VerifyBankExpenseAsync(CashCorrectionProposal proposal, ReconciliationException exception, CancellationToken ct) =>
        VerifyFinancialCorrectionAsync(proposal, exception, ct);

    private async Task<bool> VerifyMissingMovementAsync(CashCorrectionProposal proposal, ReconciliationException exception,
        CashCorrectionInputs input, CancellationToken ct)
    {
        bool appended = await db.CashMovements.AnyAsync(x => x.CashCorrectionProposalId == proposal.Id &&
            x.SessionId == exception.PositionSessionId && x.TransactionId == input.TransactionId, ct);
        if (!appended) return false;
        await ledger.ValidateTransactionAccountingEntriesAsync(input.TransactionId!.Value, ct);
        return exception.Type == "CashMovementMissing" || await VerifyPositionAsync(exception.PositionSessionId!.Value, ct);
    }

    private Task<bool> VerifyDuplicateMovementAsync(CashCorrectionProposal proposal, ReconciliationException exception, CancellationToken ct) =>
        VerifyMovementCorrectionAsync(proposal, exception, null, ct);
    private Task<bool> VerifyIncorrectMovementAmountAsync(CashCorrectionProposal proposal, ReconciliationException exception, CancellationToken ct) =>
        VerifyMovementCorrectionAsync(proposal, exception, null, ct);
    private Task<bool> VerifyWrongCashPositionAsync(CashCorrectionProposal proposal, ReconciliationException exception,
        CashCorrectionInputs input, CancellationToken ct) => VerifyMovementCorrectionAsync(proposal, exception, input.DestinationSessionId, ct);
    private Task<bool> VerifyTransferMismatchAsync(CashCorrectionProposal proposal, ReconciliationException exception,
        CashCorrectionInputs input, CancellationToken ct) => VerifyMovementCorrectionAsync(proposal, exception, input.DestinationSessionId, ct);
    private Task<bool> VerifyHandoffMismatchAsync(CashCorrectionProposal proposal, ReconciliationException exception,
        CashCorrectionInputs input, CancellationToken ct) => VerifyMovementCorrectionAsync(proposal, exception, input.DestinationSessionId, ct);

    private async Task<bool> VerifyFinancialCorrectionAsync(CashCorrectionProposal proposal, ReconciliationException exception, CancellationToken ct)
    {
        if (!proposal.ExecutionTransactionId.HasValue) return false;
        await ledger.ValidateTransactionAccountingEntriesAsync(proposal.ExecutionTransactionId.Value, ct);
        return await db.CashMovements.AnyAsync(x => x.CashCorrectionProposalId == proposal.Id &&
            x.TransactionId == proposal.ExecutionTransactionId, ct) && await VerifyPositionAsync(exception.PositionSessionId!.Value, ct);
    }

    private async Task<bool> VerifyMovementCorrectionAsync(CashCorrectionProposal proposal, ReconciliationException exception,
        long? destinationId, CancellationToken ct)
    {
        if (!await db.CashMovements.AnyAsync(x => x.CashCorrectionProposalId == proposal.Id && x.CorrectsMovementId.HasValue, ct)) return false;
        bool sourceMatches = await VerifyPositionAsync(exception.PositionSessionId!.Value, ct);
        bool destinationMatches = !destinationId.HasValue || await VerifyPositionAsync(destinationId.Value, ct);
        return sourceMatches && destinationMatches;
    }

    private async Task<bool> VerifyPositionAsync(long sessionId, CancellationToken ct)
    {
        CashPositionSession session = await db.CashPositionSessions.SingleAsync(x => x.Id == sessionId, ct);
        CashCount? count = await db.CashCounts.AsNoTracking().Where(x => x.SessionId == sessionId).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        return count is not null && await checks.CountedCashAfterHandoffsAsync(sessionId, count, ct) == session.ExpectedClosingCash;
    }
}
