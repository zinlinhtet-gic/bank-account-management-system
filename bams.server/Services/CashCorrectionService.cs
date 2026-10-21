using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Accounts;
using bams.server.Models.Security;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Plans and executes the operation required by each cash discrepancy.</summary>
public sealed partial class CashCorrectionService(ApplicationDbContext db, ICurrentUserService currentUser,
    BankExpenseService expenses, ReconciliationProjectionService projections,
    UnifiedReconciliationService checks, LedgerPostingService ledger, IBusinessDateService businessDates,
    Notifications.ReconciliationNotifier notifier)
{
    public static readonly IReadOnlyList<CashDiscrepancyDefinition> Definitions =
    [
        new(CashDiscrepancyKind.CountEntryError, "Incorrect physical count", "Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.UnrecordedDeposit, "Unrecorded deposit", "AccountId,Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.UnrecordedWithdrawal, "Unrecorded withdrawal", "AccountId,Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.MissingMovement, "Missing cash movement", "TransactionId,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.DuplicateMovement, "Duplicate cash movement", "MovementId,CounterpartMovementId,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.IncorrectMovementAmount, "Incorrect cash movement amount", "MovementId,Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.WrongCashPosition, "Wrong cash position", "MovementId,DestinationSessionId,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.TransferMismatch, "Transfer mismatch", "MovementId,CounterpartMovementId,DestinationSessionId,Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.HandoffMismatch, "Handoff mismatch", "MovementId,CounterpartMovementId,HandoffId,DestinationSessionId,Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.UnexplainedOverShort, "Cash over or short", "Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.BankExpenseNotRecorded, "Bank expense not recorded", "Amount,Payee,Purpose,Receipt,Findings,EvidenceReference")
    ];

    public async Task<IReadOnlyList<CashCorrectionProposalResponse>> PendingAsync(CancellationToken ct)
    {
        List<CashCorrectionProposal> proposals = await db.CashCorrectionProposals.AsNoTracking()
            .Include(proposal => proposal.Exception)
            .Where(x => x.Status == ProposalStatus.PendingApproval).OrderByDescending(x => x.Id).ToListAsync(ct);
        List<BusinessDate> openDates = await db.BusinessDates.AsNoTracking()
            .Where(day => day.Status == OperationsConstants.BusinessDateOpen).ToListAsync(ct);
        DateOnly? activeDate = openDates.Where(day => day.IsActive && day.Date <= bams.server.Utils.BusinessTime.Today)
            .Select(day => (DateOnly?)day.Date).SingleOrDefault();
        return proposals.Select(proposal => ToResponse(proposal) with
        {
            SourceBusinessDate = proposal.Exception!.BusinessDate,
            PostingDate = !CreatesFinancialTransaction(proposal.Kind) || openDates.Any(day => day.Date == proposal.Exception.BusinessDate)
                ? proposal.Exception.BusinessDate : activeDate
        }).ToList();
    }

    public async Task<CashCorrectionPreview> PreviewAsync(long exceptionId, CashCorrectionInputs input, CancellationToken ct)
    {
        if (!Enum.IsDefined(input.Kind) || string.IsNullOrWhiteSpace(input.Findings) || input.Findings.Length > 2000 ||
            string.IsNullOrWhiteSpace(input.EvidenceReference) || input.EvidenceReference.Length > 500)
            throw new ValidationException(MessageCode.CorrectionEvidenceRequired);
        ReconciliationException exception = await db.ReconciliationExceptions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == exceptionId, ct)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (!exception.PositionSessionId.HasValue || exception.Status is OperationsConstants.ExceptionResolved or OperationsConstants.ExceptionForcePassed)
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        CashPositionSession session = await db.CashPositionSessions.AsNoTracking().SingleAsync(x => x.Id == exception.PositionSessionId.Value, ct);
        DateOnly postingDate = CreatesFinancialTransaction(input.Kind)
            ? await businessDates.GetCorrectionPostingDateAsync(session.BusinessDate, ct) : session.BusinessDate;
        CashCount? count = await db.CashCounts.AsNoTracking().Where(x => x.SessionId == session.Id).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        ValidateCaseInputs(input);
        CorrectionPlan plan = await PlanCorrectionAsync(exception, session, input, ct);
        decimal amount = plan.Amount;
        decimal cashChange = plan.Effects.Single(effect => effect.SessionId == session.Id).Change;
        CashPositionSession? destinationSnapshot = input.DestinationSessionId.HasValue
            ? await db.CashPositionSessions.AsNoTracking().SingleAsync(x => x.Id == input.DestinationSessionId, ct) : null;
        CashMovement? movementSnapshot = input.MovementId.HasValue
            ? await db.CashMovements.AsNoTracking().SingleAsync(x => x.Id == input.MovementId, ct) : null;
        AccountSourceSnapshot? accountSnapshot = input.AccountId.HasValue
            ? await db.Accounts.AsNoTracking().Where(x => x.Id == input.AccountId.Value)
                .Select(x => new AccountSourceSnapshot(x.Id, x.Status, x.LedgerBalance, x.AvailableBalance, x.Version)).SingleOrDefaultAsync(ct)
            : null;
        TransactionSourceSnapshot? transactionSnapshot = input.TransactionId.HasValue
            ? await db.Transactions.AsNoTracking().Where(x => x.Id == input.TransactionId.Value)
                .Select(x => new TransactionSourceSnapshot(x.Id, x.TransactionType, x.TransactionStatus, x.Amount, x.FeeAmount, x.BusinessDate, x.PostedAt, x.UpdatedAt))
                .SingleOrDefaultAsync(ct)
            : null;
        List<TransactionEntry> transactionEntries = input.Kind == CashDiscrepancyKind.MissingMovement && input.TransactionId.HasValue
            ? await db.TransactionEntries.AsNoTracking().Where(x => x.TransactionId == input.TransactionId.Value &&
                x.PostingDate == session.BusinessDate && x.GlAccount!.Code == AccountingConstants.CashOnHandGlCode)
                .OrderBy(x => x.Id).ToListAsync(ct)
            : [];
        string source = SerializeSourceSnapshot(exception, session, count, destinationSnapshot, movementSnapshot,
            accountSnapshot, transactionSnapshot, transactionEntries);
        CashMovement? counterpart = input.CounterpartMovementId.HasValue
            ? await db.CashMovements.AsNoTracking().SingleAsync(x => x.Id == input.CounterpartMovementId, ct) : null;
        CashHandoff? handoff = input.HandoffId.HasValue
            ? await db.CashHandoffs.AsNoTracking().SingleAsync(x => x.Id == input.HandoffId, ct) : null;
        CashCount? destinationCount = input.DestinationSessionId.HasValue
            ? await db.CashCounts.AsNoTracking().Where(x => x.SessionId == input.DestinationSessionId)
                .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct) : null;
        List<long> sourceIds = plan.Effects.Where(effect => effect.SourceMovementId.HasValue)
            .Select(effect => effect.SourceMovementId!.Value).ToList();
        List<long> previousCorrections = await db.CashMovements.AsNoTracking()
            .Where(x => x.CorrectsMovementId.HasValue && sourceIds.Contains(x.CorrectsMovementId.Value))
            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        string sourceHash = Hash(JsonSerializer.Serialize(new
        {
            Source = source,
            DestinationCountId = destinationCount?.Id,
            DestinationCountAmount = NormalizeAmount(destinationCount?.ActualAmount),
            Counterpart = counterpart == null ? null : new MovementSourceSnapshot(counterpart.Id, counterpart.SessionId,
                counterpart.DestinationSessionId, counterpart.Type, counterpart.Status, NormalizeAmount(counterpart.Amount), counterpart.TransactionId),
            Handoff = handoff == null ? null : new HandoffSourceSnapshot(handoff.Id, handoff.SessionId,
                handoff.DestinationSessionId, handoff.CashCountId, handoff.Status, NormalizeAmount(handoff.Amount),
                handoff.Version, handoff.AcceptedAtUtc),
            PreviousCorrections = previousCorrections
        }));
        string previewHash = Hash(JsonSerializer.Serialize(new
        {
            SourceHash = sourceHash,
            PostingDate = postingDate,
            Input = new
            {
                input.Kind,
                input.Findings,
                input.EvidenceReference,
                Amount = NormalizeAmount(input.Amount),
                input.AccountId,
                input.TransactionId,
                input.MovementId,
                input.DestinationSessionId,
                input.Payee,
                input.Purpose,
                input.ReceiptFileName,
                input.ReceiptContentType,
                input.ReceiptBase64,
                input.MissingReceiptReason,
                input.CounterpartMovementId,
                input.HandoffId
            }
        }));
        return new(input.Kind, session.BusinessDate, amount, cashChange,
            input.Kind == CashDiscrepancyKind.CountEntryError ? "Record an approved recount; no financial posting." :
            input.Kind == CashDiscrepancyKind.BankExpenseNotRecorded ? "Debit Bank Operating Expense and credit Cash on Hand." :
            input.Kind == CashDiscrepancyKind.UnexplainedOverShort ? "Post the cash difference to Cash Over/Short." :
            "Append the case-specific account, GL, or cash position correction.", sourceHash, previewHash)
        {
            PostingDate = postingDate,
            PositionEffects = await PositionEffectsAsync(plan, input, ct),
            JournalLines = PlannedJournal(input, plan, cashChange),
            AccountEffects = accountSnapshot is null ? [] : [new(accountSnapshot.Id, accountSnapshot.LedgerBalance,
                cashChange, accountSnapshot.LedgerBalance + cashChange, accountSnapshot.AvailableBalance, accountSnapshot.AvailableBalance + cashChange)]
        };
    }

    public async Task<CashCorrectionProposalResponse> SubmitAsync(long exceptionId, CashCorrectionInputs input,
        string expectedPreviewHash, CancellationToken ct)
    {
        long actor = currentUser.GetCurrentUserId();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(ct);
        ReconciliationException exception = (await db.ReconciliationExceptions.FromSql($"SELECT * FROM ReconciliationExceptions WHERE Id = {exceptionId} FOR UPDATE").ToListAsync(ct)).Single();
        await LockCorrectionSourcesAsync(exception.PositionSessionId!.Value, input, ct);
        CashCorrectionPreview preview = await PreviewAsync(exceptionId, input, ct);
        if (preview.PreviewHash != expectedPreviewHash) throw new ConflictException(MessageCode.CorrectionProposalStale);
        if (exception.AssignedTo != actor || exception.Status is OperationsConstants.ExceptionResolved or OperationsConstants.ExceptionForcePassed ||
            await db.CashCorrectionProposals.AnyAsync(x => x.ExceptionId == exceptionId && x.Status == ProposalStatus.PendingApproval, ct))
            throw new ConflictException(MessageCode.Conflict);
        CashCorrectionProposal proposal = new() { ExceptionId = exceptionId, Kind = input.Kind,
            Status = ProposalStatus.PendingApproval, Verification = VerificationStatus.Pending,
            ProposerId = actor, InputsJson = JsonSerializer.Serialize(input), SourceHash = preview.SourceHash,
            CreatedAtUtc = DateTime.UtcNow };
        db.CashCorrectionProposals.Add(proposal);
        SetStatus(exception, OperationsConstants.ExceptionAdjustmentRequired, "Cash correction proposal submitted.", actor);
        await db.SaveChangesAsync(ct);
        // The proposal id exists only after the first save; both saves share the transaction.
        await notifier.ProposalSubmittedAsync(exception, proposal.Id, isCash: true, actor, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(proposal);
    }

    public async Task<CashCorrectionProposalResponse> ReviewAsync(long id, ReviewCorrectionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000)
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        long actor = currentUser.GetCurrentUserId();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize the posting date with date switches and EOD before taking financial source locks.
        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(ct);
        CashCorrectionProposal proposal = (await db.CashCorrectionProposals.FromSql($"SELECT * FROM CashCorrectionProposals WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);

        List<long> managers = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
            join staff in db.Users on ur.UserId equals staff.Id
            where role.Code == SecurityConstants.ManagerRole && staff.Status == UserStatus.Active select staff.Id).Distinct().ToListAsync(ct);
        if (!managers.Contains(actor))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        if (proposal.Status == ProposalStatus.Executed)
        {
            if (!request.Approve) throw new ConflictException(MessageCode.Conflict);
            DateOnly sourceDate = await db.ReconciliationExceptions.AsNoTracking().Where(x => x.Id == proposal.ExceptionId).Select(x => x.BusinessDate).SingleAsync(ct);
            DateOnly? postedDate = proposal.ExecutionTransactionId.HasValue
                ? await db.Transactions.AsNoTracking().Where(x => x.Id == proposal.ExecutionTransactionId).Select(x => x.BusinessDate).SingleAsync(ct) : sourceDate;
            return ToResponse(proposal) with { SourceBusinessDate = sourceDate, PostingDate = postedDate };
        }
        if (actor == proposal.ProposerId && (managers.Count != 1 || managers[0] != actor))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        if (proposal.Status != ProposalStatus.PendingApproval) throw new ConflictException(MessageCode.Conflict);
        ReconciliationException exception = (await db.ReconciliationExceptions.FromSql($"SELECT * FROM ReconciliationExceptions WHERE Id = {proposal.ExceptionId} FOR UPDATE").ToListAsync(ct)).Single();
        if (exception.Status == OperationsConstants.ExceptionForcePassed) throw new ConflictException(MessageCode.Conflict);
        proposal.ReviewerId = actor;
        proposal.ReviewedAtUtc = DateTime.UtcNow;
        proposal.ReviewNote = request.Note.Trim();
        bool proposalBecameStale = false;
        if (!request.Approve)
        {
            proposal.Status = ProposalStatus.Rejected;
            SetStatus(exception, OperationsConstants.ExceptionUnderInvestigation, "Cash proposal rejected: " + request.Note.Trim(), actor);
        }
        else
        {
            CashCorrectionInputs input = JsonSerializer.Deserialize<CashCorrectionInputs>(proposal.InputsJson)!;
            await LockCorrectionSourcesAsync(exception.PositionSessionId!.Value, input, ct);
            CashPositionSession session = await db.CashPositionSessions.SingleAsync(x => x.Id == exception.PositionSessionId, ct);
            CashCorrectionPreview? preview = null;
            string staleReason = "Cash proposal superseded because its source records or required evidence changed before manager approval.";
            if (exception.Status != OperationsConstants.ExceptionResolved)
            {
                try
                {
                    preview = await PreviewAsync(exception.Id, input, ct);
                }
                catch (ValidationException failure)
                {
                    // Earlier proposals may lack the newly required pair evidence, or another correction consumed it.
                    staleReason = $"Cash proposal requires a new investigation preview ({failure.Code}).";
                }
            }
            if (preview is null || preview.SourceHash != proposal.SourceHash)
            {
                proposal.Status = ProposalStatus.Superseded;
                proposalBecameStale = true;
                SetStatus(exception, OperationsConstants.ExceptionUnderInvestigation, staleReason, actor);
            }
            else
            {
                await ExecuteAsync(proposal, exception, session, input, preview, actor, ct);
                proposal.Status = ProposalStatus.Executed;
                proposal.Verification = VerificationStatus.Pending;
                SetStatus(exception, OperationsConstants.ExceptionAdjustmentRequired,
                    $"Cash correction posted on {preview.PostingDate:yyyy-MM-dd} for source date {session.BusinessDate:yyyy-MM-dd}; verification pending.", actor);
            }
        }
        Notifications.ProposalReviewOutcome outcome = !request.Approve ? Notifications.ProposalReviewOutcome.Rejected
            : proposalBecameStale ? Notifications.ProposalReviewOutcome.Superseded : Notifications.ProposalReviewOutcome.Approved;
        await notifier.ProposalReviewedAsync(exception, proposal.Id, proposal.ProposerId, isCash: true, outcome,
            request.Note.Trim(), actor, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (proposalBecameStale) throw new ConflictException(MessageCode.CorrectionProposalStale);
        if (request.Approve)
        {
            // Release the committed posting transaction before verification opens its own transaction.
            await tx.DisposeAsync();
            return await VerifyAsync(id, ct);
        }
        return ToResponse(proposal);
    }

    public async Task<CashCorrectionProposalResponse> VerifyAsync(long id, CancellationToken ct)
    {
        CashCorrectionProposal proposal = await db.CashCorrectionProposals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (proposal.Status != ProposalStatus.Executed) throw new ConflictException(MessageCode.Conflict);
        ReconciliationException source = await db.ReconciliationExceptions.AsNoTracking().SingleAsync(x => x.Id == proposal.ExceptionId, ct);
        await checks.RequireVerificationAccessAsync(source, ct);
        if (proposal.Verification == VerificationStatus.Passed) return ToResponse(proposal);
        await checks.RunAsync(new(source.BusinessDate, source.BusinessDate), ct);
        DateOnly? financialDate = proposal.ExecutionTransactionId.HasValue
            ? await db.Transactions.AsNoTracking().Where(transaction => transaction.Id == proposal.ExecutionTransactionId.Value)
                .Select(transaction => transaction.BusinessDate).SingleAsync(ct)
            : null;
        if (financialDate.HasValue && financialDate.Value != source.BusinessDate)
            await checks.RunAsync(new(financialDate.Value, financialDate.Value), ct);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(ct);
        CashCorrectionProposal locked = (await db.CashCorrectionProposals.FromSql($"SELECT * FROM CashCorrectionProposals WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).Single();
        ReconciliationException exception = (await db.ReconciliationExceptions
            .FromSql($"SELECT * FROM ReconciliationExceptions WHERE Id = {locked.ExceptionId} FOR UPDATE").ToListAsync(ct)).Single();
        await checks.RequireVerificationAccessAsync(exception, ct);
        if (locked.Verification == VerificationStatus.Passed) return ToResponse(locked);
        CashCorrectionInputs input = JsonSerializer.Deserialize<CashCorrectionInputs>(locked.InputsJson)!;
        await LockCorrectionSourcesAsync(exception.PositionSessionId!.Value, input, ct);
        bool matched;
        string failureNote = "A cash position or its correction evidence does not match; investigate again.";
        try
        {
            matched = await VerifyCorrectionAsync(locked, exception, input, ct);
        }
        catch (BusinessRuleException failure)
        {
            // A posted correction remains posted even when subsequent journal verification fails.
            matched = false;
            failureNote = $"Correction verification failed ({failure.Code}): {failure.Message}";
        }
        locked.Verification = matched ? VerificationStatus.Passed : VerificationStatus.Failed;
        locked.VerifiedAtUtc = DateTime.UtcNow;
        locked.VerificationNote = matched ? "Correction evidence and all required cash positions match." : failureNote;
        SetStatus(exception, matched ? OperationsConstants.ExceptionResolved : OperationsConstants.ExceptionUnderInvestigation,
            locked.VerificationNote, currentUser.GetCurrentUserId());
        if (!matched)
            await notifier.VerificationFailedAsync(exception, locked.Id, locked.ProposerId, locked.VerificationNote,
                currentUser.GetCurrentUserId(), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(locked) with { SourceBusinessDate = source.BusinessDate, PostingDate = financialDate ?? source.BusinessDate };
    }

    private async Task ExecuteAsync(CashCorrectionProposal proposal, ReconciliationException exception,
        CashPositionSession session, CashCorrectionInputs input, CashCorrectionPreview preview, long approver, CancellationToken ct)
    {
        CorrectionPlan plan = await PlanCorrectionAsync(exception, session, input, ct);
        switch (input.Kind)
        {
            case CashDiscrepancyKind.CountEntryError:
                await ExecuteCountEntryErrorAsync(proposal, session, input, preview, ct); break;
            case CashDiscrepancyKind.UnrecordedDeposit:
                await ExecuteUnrecordedDepositAsync(proposal, session, input, preview, approver, ct); break;
            case CashDiscrepancyKind.UnrecordedWithdrawal:
                await ExecuteUnrecordedWithdrawalAsync(proposal, session, input, preview, approver, ct); break;
            case CashDiscrepancyKind.MissingMovement:
                await ExecuteMissingMovementAsync(proposal, plan, input, approver, ct); break;
            case CashDiscrepancyKind.DuplicateMovement:
                await ExecuteDuplicateMovementAsync(proposal, plan, input, approver, ct); break;
            case CashDiscrepancyKind.IncorrectMovementAmount:
                await ExecuteIncorrectMovementAmountAsync(proposal, plan, input, approver, ct); break;
            case CashDiscrepancyKind.WrongCashPosition:
                await ExecuteWrongCashPositionAsync(proposal, plan, input, approver, ct); break;
            case CashDiscrepancyKind.TransferMismatch:
                await ExecuteTransferMismatchAsync(proposal, plan, input, approver, ct); break;
            case CashDiscrepancyKind.HandoffMismatch:
                await ExecuteHandoffMismatchAsync(proposal, plan, input, approver, ct); break;
            case CashDiscrepancyKind.UnexplainedOverShort:
                await ExecuteUnexplainedOverShortAsync(proposal, session, preview, approver, ct); break;
            case CashDiscrepancyKind.BankExpenseNotRecorded:
                await ExecuteBankExpenseAsync(proposal, exception, session, input, preview, approver, ct); break;
            default: throw new ValidationException(MessageCode.InvalidRequest);
        }
        if (proposal.ExecutionTransactionId.HasValue)
        {
            exception.CorrectionTransactionId = proposal.ExecutionTransactionId;
            await ledger.ValidateTransactionAccountingEntriesAsync(proposal.ExecutionTransactionId.Value, ct);
        }
        await projections.GenerateProvisionalAsync(preview.PostingDate, ct);
    }

    private async Task PostMissingCustomerCashAsync(CashCorrectionProposal proposal, CashPositionSession session,
        CashCorrectionInputs input, CashCorrectionPreview preview, long approver, CancellationToken ct)
    {
        Account account = (await db.Accounts.FromSql($"SELECT * FROM Accounts WHERE Id = {input.AccountId} FOR UPDATE").ToListAsync(ct)).Single();
        decimal change = input.Kind == CashDiscrepancyKind.UnrecordedDeposit ? preview.SuggestedAmount : -preview.SuggestedAmount;
        if (account.LedgerBalance + change < 0m || account.AvailableBalance + change < 0m ||
            session.ExpectedClosingCash + preview.CashChange < session.ReservedCash)
            throw new BusinessRuleException(MessageCode.InsufficientBalance);
        DateTime now = DateTime.UtcNow;
        Transaction transaction = LedgerPostingService.CreateTransaction(change > 0m ? TransactionType.CashDeposit : TransactionType.CashWithdrawal,
            TransactionStatus.Completed, preview.SuggestedAmount, input.Findings, $"CASH-EX-{proposal.ExceptionId}",
            $"CASH-EX-{proposal.Id}", proposal.ProposerId, now);
        transaction.BusinessDate = preview.PostingDate;
        transaction.AuthorizedBy = approver;
        transaction.AuthorizedAt = now;
        db.Transactions.Add(transaction);
        long customerGl = await db.GlAccounts.Where(x => x.Code == AccountingConstants.CustomerDepositsGlCode).Select(x => x.Id).SingleAsync(ct);
        long cashGl = await db.GlAccounts.Where(x => x.Code == AccountingConstants.CashOnHandGlCode).Select(x => x.Id).SingleAsync(ct);
        EntryType customerDirection = change > 0m ? EntryType.Credit : EntryType.Debit;
        EntryType cashDirection = change > 0m ? EntryType.Debit : EntryType.Credit;
        db.TransactionEntries.AddRange(
            new TransactionEntry { Transaction = transaction, GlAccountId = customerGl, CustomerAccountId = account.Id,
                EntryType = customerDirection, Amount = preview.SuggestedAmount, PostingDate = preview.PostingDate, CreatedAt = now },
            new TransactionEntry { Transaction = transaction, GlAccountId = cashGl, EntryType = cashDirection,
                Amount = preview.SuggestedAmount, PostingDate = preview.PostingDate, CreatedAt = now });
        db.AccountTransactions.Add(new AccountTransaction { Transaction = transaction, AccountId = account.Id,
            EntryType = customerDirection, Amount = preview.SuggestedAmount, LedgerBalanceBefore = account.LedgerBalance,
            LedgerBalanceAfter = account.LedgerBalance + change, AvailableBalanceBefore = account.AvailableBalance,
            AvailableBalanceAfter = account.AvailableBalance + change, ValueDate = preview.PostingDate,
            PostingDate = preview.PostingDate, Description = input.Findings, ReferenceNo = transaction.ReferenceNo,
            Status = TransactionConstants.CompletedStatus, CreatedAt = now });
        account.LedgerBalance += change;
        account.AvailableBalance += change;
        account.Version++;
        account.UpdatedAt = now;
        session.ExpectedClosingCash += preview.CashChange;
        session.Version++;
        db.CashMovements.Add(new CashMovement { Session = session, Transaction = transaction,
            CashCorrectionProposalId = proposal.Id,
            Type = change > 0m ? "Deposit" : "Withdrawal", Amount = preview.SuggestedAmount,
            ActorId = approver, ApprovedBy = approver, ApprovedAtUtc = now, CreatedAtUtc = now,
            Note = $"Cash reconciliation proposal {proposal.Id}" });
        await db.SaveChangesAsync(ct);
        proposal.ExecutionTransactionId = transaction.Id;
        await projections.RebuildAccountAsync(account.Id, proposal.Id, ct, "Cash");
    }

    private async Task PostOverShortAsync(CashCorrectionProposal proposal, CashPositionSession session,
        CashCorrectionPreview preview, long approver, CancellationToken ct)
    {
        decimal change = preview.CashChange;
        if (change == 0m || session.ExpectedClosingCash + change < session.ReservedCash)
            throw new BusinessRuleException(MessageCode.InsufficientCashPositionBalance);
        DateTime now = DateTime.UtcNow;
        Transaction transaction = LedgerPostingService.CreateTransaction(TransactionType.CashOverShort,
            TransactionStatus.Completed, Math.Abs(change), "Cash over/short reconciliation", $"CASH-EX-{proposal.ExceptionId}",
            $"CASH-EX-{proposal.Id}", proposal.ProposerId, now);
        transaction.BusinessDate = preview.PostingDate;
        transaction.AuthorizedBy = approver;
        transaction.AuthorizedAt = now;
        long cashGl = await db.GlAccounts.Where(x => x.Code == AccountingConstants.CashOnHandGlCode).Select(x => x.Id).SingleAsync(ct);
        long differenceGl = await db.GlAccounts.Where(x => x.Code == AccountingConstants.CashOverShortGlCode).Select(x => x.Id).SingleAsync(ct);
        db.Transactions.Add(transaction);
        db.TransactionEntries.AddRange(
            new TransactionEntry { Transaction = transaction, GlAccountId = cashGl,
                EntryType = change > 0m ? EntryType.Debit : EntryType.Credit,
                Amount = Math.Abs(change), PostingDate = preview.PostingDate, CreatedAt = now },
            new TransactionEntry { Transaction = transaction, GlAccountId = differenceGl,
                EntryType = change > 0m ? EntryType.Credit : EntryType.Debit,
                Amount = Math.Abs(change), PostingDate = preview.PostingDate, CreatedAt = now });
        db.CashMovements.Add(new CashMovement { Session = session, Transaction = transaction,
            CashCorrectionProposalId = proposal.Id,
            Type = change > 0m ? "CorrectionIn" : "CorrectionOut", Amount = Math.Abs(change),
            ActorId = approver, ApprovedBy = approver, ApprovedAtUtc = now, CreatedAtUtc = now,
            Note = $"Cash over/short proposal {proposal.Id}" });
        session.ExpectedClosingCash += change;
        session.Version++;
        await db.SaveChangesAsync(ct);
        proposal.ExecutionTransactionId = transaction.Id;
    }

    private static string SerializeSourceSnapshot(ReconciliationException exception, CashPositionSession session,
        CashCount? count, CashPositionSession? destination, CashMovement? movement, AccountSourceSnapshot? account,
        TransactionSourceSnapshot? transaction, IReadOnlyCollection<TransactionEntry> transactionEntries) => JsonSerializer.Serialize(new
        {
            SessionId = session.Id,
            SessionVersion = session.Version,
            session.BusinessDate,
            session.PositionType,
            session.Status,
            ExpectedClosingCash = NormalizeAmount(session.ExpectedClosingCash),
            ReservedCash = NormalizeAmount(session.ReservedCash),
            CountId = count?.Id,
            CountAmount = NormalizeAmount(count?.ActualAmount),
            ExceptionId = exception.Id,
            exception.Type,
            exception.PositionSessionId,
            exception.RelatedTransactionId,
            Difference = NormalizeAmount(exception.Difference),
            DestinationSessionId = destination?.Id,
            DestinationVersion = destination?.Version,
            DestinationExpectedCash = NormalizeAmount(destination?.ExpectedClosingCash),
            DestinationReservedCash = NormalizeAmount(destination?.ReservedCash),
            MovementId = movement?.Id,
            MovementSessionId = movement?.SessionId,
            MovementAmount = NormalizeAmount(movement?.Amount),
            MovementType = movement?.Type,
            MovementStatus = movement?.Status,
            MovementDestinationSessionId = movement?.DestinationSessionId,
            Account = account,
            Transaction = transaction,
            TransactionEntries = transactionEntries.Select(entry => new
            {
                entry.Id,
                entry.GlAccountId,
                entry.EntryType,
                Amount = NormalizeAmount(entry.Amount),
                entry.PostingDate
            }).ToList()
        });

    private static string NormalizeAmount(decimal? amount) => amount.HasValue
        ? amount.Value.ToString("0.00", CultureInfo.InvariantCulture)
        : "<null>";

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private void SetStatus(ReconciliationException exception, string status, string note, long actor)
    {
        db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory { Exception = exception,
            OldStatus = exception.Status, NewStatus = status, Note = note, ActorId = actor, CreatedAtUtc = DateTime.UtcNow });
        exception.Status = status;
        exception.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static CashCorrectionProposalResponse ToResponse(CashCorrectionProposal proposal) =>
        new(proposal.Id, proposal.ExceptionId, proposal.Kind, proposal.Status, proposal.Verification,
            proposal.ProposerId, proposal.ReviewerId, JsonSerializer.Deserialize<CashCorrectionInputs>(proposal.InputsJson)!,
            proposal.ExecutionTransactionId, proposal.ReviewNote, proposal.VerificationNote, proposal.CreatedAtUtc);
}
