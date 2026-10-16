using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
public sealed class CashCorrectionService(ApplicationDbContext db, ICurrentUserService currentUser,
    BankExpenseService expenses, ReconciliationProjectionService projections,
    UnifiedReconciliationService checks, LedgerPostingService ledger)
{
    public static readonly IReadOnlyList<CashDiscrepancyDefinition> Definitions =
    [
        new(CashDiscrepancyKind.CountEntryError, "Incorrect physical count", "Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.UnrecordedDeposit, "Unrecorded deposit", "AccountId,Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.UnrecordedWithdrawal, "Unrecorded withdrawal", "AccountId,Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.MissingMovement, "Missing cash movement", "TransactionId,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.DuplicateMovement, "Duplicate cash movement", "MovementId,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.IncorrectMovementAmount, "Incorrect cash movement amount", "MovementId,Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.WrongCashPosition, "Wrong cash position", "MovementId,DestinationSessionId,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.TransferMismatch, "Transfer mismatch", "MovementId,DestinationSessionId,Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.HandoffMismatch, "Handoff mismatch", "MovementId,DestinationSessionId,Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.UnexplainedOverShort, "Cash over or short", "Amount,Findings,EvidenceReference"),
        new(CashDiscrepancyKind.BankExpenseNotRecorded, "Bank expense not recorded", "Amount,Payee,Purpose,Receipt,Findings,EvidenceReference")
    ];

    public async Task<IReadOnlyList<CashCorrectionProposalResponse>> PendingAsync(CancellationToken ct)
    {
        List<CashCorrectionProposal> proposals = await db.CashCorrectionProposals.AsNoTracking()
            .Where(x => x.Status == ProposalStatus.PendingApproval).OrderByDescending(x => x.Id).ToListAsync(ct);
        return proposals.Select(ToResponse).ToList();
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
        CashCount? count = await db.CashCounts.AsNoTracking().Where(x => x.SessionId == session.Id).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        decimal amount = input.Amount ?? Math.Abs(exception.Difference);
        if ((input.Kind == CashDiscrepancyKind.CountEntryError && !input.Amount.HasValue) ||
            amount < 0m || (amount == 0m && input.Kind != CashDiscrepancyKind.CountEntryError) ||
            decimal.Round(amount, 2) != amount)
            throw new ValidationException(MessageCode.InvalidAmount);
        decimal cashChange = input.Kind switch
        {
            CashDiscrepancyKind.UnrecordedDeposit => amount,
            CashDiscrepancyKind.UnrecordedWithdrawal or CashDiscrepancyKind.BankExpenseNotRecorded => -amount,
            CashDiscrepancyKind.UnexplainedOverShort => exception.Difference,
            CashDiscrepancyKind.CountEntryError => 0m,
            _ => await MovementCorrectionAsync(session, input, ct)
        };
        if (input.Kind is CashDiscrepancyKind.UnrecordedDeposit or CashDiscrepancyKind.UnrecordedWithdrawal)
        {
            if (!input.AccountId.HasValue || !await db.Accounts.AnyAsync(x => x.Id == input.AccountId.Value, ct))
                throw new ValidationException(MessageCode.InvalidRequest);
        }
        if (input.Kind == CashDiscrepancyKind.BankExpenseNotRecorded)
            BankExpenseService.Validate(new(session.Id, amount, input.Payee ?? "", input.Purpose ?? "",
                input.ReceiptFileName, input.ReceiptContentType, input.ReceiptBase64, input.MissingReceiptReason));
        if (input.Kind is CashDiscrepancyKind.WrongCashPosition or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch)
        {
            if (!input.DestinationSessionId.HasValue || input.DestinationSessionId == session.Id ||
                !await db.CashPositionSessions.AnyAsync(x => x.Id == input.DestinationSessionId && x.BusinessDate == session.BusinessDate, ct))
                throw new ValidationException(MessageCode.InvalidRequest);
        }
        CashPositionSession? destinationSnapshot = input.DestinationSessionId.HasValue
            ? await db.CashPositionSessions.AsNoTracking().SingleAsync(x => x.Id == input.DestinationSessionId, ct) : null;
        CashMovement? movementSnapshot = input.MovementId.HasValue
            ? await db.CashMovements.AsNoTracking().SingleAsync(x => x.Id == input.MovementId, ct) : null;
        string source = JsonSerializer.Serialize(new { session.Id, session.Version, session.ExpectedClosingCash,
            CountId = count?.Id, CountAmount = count?.ActualAmount, ExceptionId = exception.Id, exception.Difference,
            CashChange = cashChange, DestinationVersion = destinationSnapshot?.Version,
            MovementAmount = movementSnapshot?.Amount, MovementType = movementSnapshot?.Type,
            Input = input });
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        return new(input.Kind, session.BusinessDate, amount, cashChange,
            input.Kind == CashDiscrepancyKind.CountEntryError ? "Record an approved recount; no financial posting." :
            input.Kind == CashDiscrepancyKind.BankExpenseNotRecorded ? "Debit Bank Operating Expense and credit Cash on Hand." :
            input.Kind == CashDiscrepancyKind.UnexplainedOverShort ? "Post the cash difference to Cash Over/Short." :
            "Append the case-specific account, GL, or cash position correction.", hash);
    }

    public async Task<CashCorrectionProposalResponse> SubmitAsync(long exceptionId, CashCorrectionInputs input,
        string expectedSourceHash, CancellationToken ct)
    {
        CashCorrectionPreview preview = await PreviewAsync(exceptionId, input, ct);
        if (preview.SourceHash != expectedSourceHash) throw new ConflictException(MessageCode.CorrectionProposalStale);
        long actor = currentUser.GetCurrentUserId();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        ReconciliationException exception = (await db.ReconciliationExceptions.FromSql($"SELECT * FROM ReconciliationExceptions WHERE Id = {exceptionId} FOR UPDATE").ToListAsync(ct)).Single();
        if (!await db.BusinessDates.AnyAsync(x => x.Date == exception.BusinessDate && x.Status == OperationsConstants.BusinessDateOpen, ct))
            throw new ConflictException(MessageCode.BusinessDateTransitionConflict);
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
        await tx.CommitAsync(ct);
        return ToResponse(proposal);
    }

    public async Task<CashCorrectionProposalResponse> ReviewAsync(long id, ReviewCorrectionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000)
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        long actor = currentUser.GetCurrentUserId();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        List<BusinessDate> openDates = await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(ct);
        CashCorrectionProposal proposal = (await db.CashCorrectionProposals.FromSql($"SELECT * FROM CashCorrectionProposals WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (proposal.Status != ProposalStatus.PendingApproval) throw new ConflictException(MessageCode.Conflict);
        List<long> managers = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
            join staff in db.Users on ur.UserId equals staff.Id
            where role.Code == SecurityConstants.ManagerRole && staff.Status == UserStatus.Active select staff.Id).Distinct().ToListAsync(ct);
        if (!managers.Contains(actor) || actor == proposal.ProposerId && (managers.Count != 1 || managers[0] != actor))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
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
            CashPositionSession session = (await db.CashPositionSessions.FromSql($"SELECT * FROM CashPositionSessions WHERE Id = {exception.PositionSessionId} FOR UPDATE").ToListAsync(ct)).Single();
            CashCorrectionPreview preview = await PreviewAsync(exception.Id, input, ct);
            if (preview.SourceHash != proposal.SourceHash)
            {
                proposal.Status = ProposalStatus.Superseded;
                proposalBecameStale = true;
                SetStatus(exception, OperationsConstants.ExceptionUnderInvestigation,
                    "Cash proposal superseded because its source records changed before manager approval.", actor);
            }
            else if (!openDates.Any(x => x.Date == session.BusinessDate))
                throw new ConflictException(MessageCode.BusinessDateClosed);
            else
            {
                await ExecuteAsync(proposal, exception, session, input, preview, actor, ct);
                proposal.Status = ProposalStatus.Executed;
                proposal.Verification = VerificationStatus.Pending;
                SetStatus(exception, OperationsConstants.ExceptionAdjustmentRequired, "Cash correction posted; verification pending.", actor);
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (proposalBecameStale) throw new ConflictException(MessageCode.CorrectionProposalStale);
        return request.Approve ? await VerifyAsync(id, ct) : ToResponse(proposal);
    }

    public async Task<CashCorrectionProposalResponse> VerifyAsync(long id, CancellationToken ct)
    {
        CashCorrectionProposal proposal = await db.CashCorrectionProposals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (proposal.Status != ProposalStatus.Executed) throw new ConflictException(MessageCode.Conflict);
        ReconciliationException source = await db.ReconciliationExceptions.AsNoTracking().SingleAsync(x => x.Id == proposal.ExceptionId, ct);
        await checks.RunAsync(new(source.BusinessDate, source.BusinessDate), ct);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        CashCorrectionProposal locked = (await db.CashCorrectionProposals.FromSql($"SELECT * FROM CashCorrectionProposals WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).Single();
        ReconciliationException exception = await db.ReconciliationExceptions.SingleAsync(x => x.Id == locked.ExceptionId, ct);
        CashPositionSession session = await db.CashPositionSessions.AsNoTracking().SingleAsync(x => x.Id == exception.PositionSessionId, ct);
        CashCount? latest = await db.CashCounts.AsNoTracking().Where(x => x.SessionId == session.Id).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        bool matched = latest is not null && await checks.CountedCashAfterHandoffsAsync(session.Id, latest, ct) == session.ExpectedClosingCash;
        if (exception.Type == "CashMovementMissing" && locked.Kind == CashDiscrepancyKind.MissingMovement)
            matched = await db.CashMovements.AnyAsync(x => x.TransactionId == exception.RelatedTransactionId, ct);
        locked.Verification = matched ? VerificationStatus.Passed : VerificationStatus.Failed;
        locked.VerifiedAtUtc = DateTime.UtcNow;
        locked.VerificationNote = matched ? "Cash position and latest physical count match." : "Cash mismatch remains; investigate again.";
        SetStatus(exception, matched ? OperationsConstants.ExceptionResolved : OperationsConstants.ExceptionUnderInvestigation,
            locked.VerificationNote, currentUser.GetCurrentUserId());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(locked);
    }

    private async Task ExecuteAsync(CashCorrectionProposal proposal, ReconciliationException exception,
        CashPositionSession session, CashCorrectionInputs input, CashCorrectionPreview preview, long approver, CancellationToken ct)
    {
        switch (input.Kind)
        {
            case CashDiscrepancyKind.CountEntryError:
                db.CashCounts.Add(new CashCount { SessionId = session.Id, ExpectedAmount = session.ExpectedClosingCash,
                    ActualAmount = preview.SuggestedAmount, Difference = preview.SuggestedAmount - session.ExpectedClosingCash,
                    Notes = "Approved recount: " + input.Findings, CountedBy = proposal.ProposerId, CountedAtUtc = DateTime.UtcNow });
                break;
            case CashDiscrepancyKind.UnrecordedDeposit:
            case CashDiscrepancyKind.UnrecordedWithdrawal:
                await PostMissingCustomerCashAsync(proposal, session, input, preview, approver, ct);
                break;
            case CashDiscrepancyKind.BankExpenseNotRecorded:
            {
                BankExpenseRequest expense = BankExpenseService.CreateRequest(new(session.Id, preview.SuggestedAmount,
                    input.Payee!, input.Purpose!, input.ReceiptFileName, input.ReceiptContentType,
                    input.ReceiptBase64, input.MissingReceiptReason), session.BusinessDate, proposal.ProposerId,
                    exception.Id, proposal.Id);
                expense.Status = "Posted";
                expense.ReviewedBy = approver;
                expense.ReviewedAtUtc = DateTime.UtcNow;
                expense.ReviewNote = proposal.ReviewNote;
                db.BankExpenseRequests.Add(expense);
                await db.SaveChangesAsync(ct);
                await expenses.PostApprovedAsync(expense, session, approver, ct);
                proposal.ExecutionTransactionId = expense.TransactionId;
                break;
            }
            case CashDiscrepancyKind.UnexplainedOverShort:
                await PostOverShortAsync(proposal, session, preview, approver, ct);
                break;
            default:
                await AppendMovementCorrectionAsync(session, input, preview.CashChange, approver, ct);
                break;
        }
        if (proposal.ExecutionTransactionId.HasValue)
        {
            exception.CorrectionTransactionId = proposal.ExecutionTransactionId;
            await ledger.ValidateTransactionAccountingEntriesAsync(proposal.ExecutionTransactionId.Value, ct);
        }
        await projections.GenerateProvisionalAsync(session.BusinessDate, ct);
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
        transaction.BusinessDate = session.BusinessDate;
        transaction.AuthorizedBy = approver;
        transaction.AuthorizedAt = now;
        db.Transactions.Add(transaction);
        long customerGl = await db.GlAccounts.Where(x => x.Code == AccountingConstants.CustomerDepositsGlCode).Select(x => x.Id).SingleAsync(ct);
        long cashGl = await db.GlAccounts.Where(x => x.Code == AccountingConstants.CashOnHandGlCode).Select(x => x.Id).SingleAsync(ct);
        EntryType customerDirection = change > 0m ? EntryType.Credit : EntryType.Debit;
        EntryType cashDirection = change > 0m ? EntryType.Debit : EntryType.Credit;
        db.TransactionEntries.AddRange(
            new TransactionEntry { Transaction = transaction, GlAccountId = customerGl, CustomerAccountId = account.Id,
                EntryType = customerDirection, Amount = preview.SuggestedAmount, PostingDate = session.BusinessDate, CreatedAt = now },
            new TransactionEntry { Transaction = transaction, GlAccountId = cashGl, EntryType = cashDirection,
                Amount = preview.SuggestedAmount, PostingDate = session.BusinessDate, CreatedAt = now });
        db.AccountTransactions.Add(new AccountTransaction { Transaction = transaction, AccountId = account.Id,
            EntryType = customerDirection, Amount = preview.SuggestedAmount, LedgerBalanceBefore = account.LedgerBalance,
            LedgerBalanceAfter = account.LedgerBalance + change, AvailableBalanceBefore = account.AvailableBalance,
            AvailableBalanceAfter = account.AvailableBalance + change, ValueDate = session.BusinessDate,
            PostingDate = session.BusinessDate, Description = input.Findings, ReferenceNo = transaction.ReferenceNo,
            Status = TransactionConstants.CompletedStatus, CreatedAt = now });
        account.LedgerBalance += change;
        account.AvailableBalance += change;
        account.Version++;
        account.UpdatedAt = now;
        session.ExpectedClosingCash += preview.CashChange;
        session.Version++;
        db.CashMovements.Add(new CashMovement { Session = session, Transaction = transaction,
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
        transaction.BusinessDate = session.BusinessDate;
        transaction.AuthorizedBy = approver;
        transaction.AuthorizedAt = now;
        long cashGl = await db.GlAccounts.Where(x => x.Code == AccountingConstants.CashOnHandGlCode).Select(x => x.Id).SingleAsync(ct);
        long differenceGl = await db.GlAccounts.Where(x => x.Code == AccountingConstants.CashOverShortGlCode).Select(x => x.Id).SingleAsync(ct);
        db.Transactions.Add(transaction);
        db.TransactionEntries.AddRange(
            new TransactionEntry { Transaction = transaction, GlAccountId = cashGl,
                EntryType = change > 0m ? EntryType.Debit : EntryType.Credit,
                Amount = Math.Abs(change), PostingDate = session.BusinessDate, CreatedAt = now },
            new TransactionEntry { Transaction = transaction, GlAccountId = differenceGl,
                EntryType = change > 0m ? EntryType.Credit : EntryType.Debit,
                Amount = Math.Abs(change), PostingDate = session.BusinessDate, CreatedAt = now });
        db.CashMovements.Add(new CashMovement { Session = session, Transaction = transaction,
            Type = change > 0m ? "CorrectionIn" : "CorrectionOut", Amount = Math.Abs(change),
            ActorId = approver, ApprovedBy = approver, ApprovedAtUtc = now, CreatedAtUtc = now,
            Note = $"Cash over/short proposal {proposal.Id}" });
        session.ExpectedClosingCash += change;
        session.Version++;
        await db.SaveChangesAsync(ct);
        proposal.ExecutionTransactionId = transaction.Id;
    }

    private async Task AppendMovementCorrectionAsync(CashPositionSession session, CashCorrectionInputs input,
        decimal change, long approver, CancellationToken ct)
    {
        if (change == 0m || session.ExpectedClosingCash + change < session.ReservedCash)
            throw new BusinessRuleException(MessageCode.InsufficientCashPositionBalance);
        CashPositionSession? destination = null;
        if (input.DestinationSessionId.HasValue)
            destination = (await db.CashPositionSessions.FromSql($"SELECT * FROM CashPositionSessions WHERE Id = {input.DestinationSessionId} FOR UPDATE").ToListAsync(ct)).Single();
        DateTime now = DateTime.UtcNow;
        db.CashMovements.Add(new CashMovement { Session = session, DestinationSession = destination,
            Type = change > 0m ? "CorrectionIn" : "CorrectionOut", Amount = Math.Abs(change),
            TransactionId = input.Kind == CashDiscrepancyKind.MissingMovement ? input.TransactionId : null,
            CorrectionTransactionId = input.Kind == CashDiscrepancyKind.MissingMovement ? null : input.TransactionId,
            ActorId = approver, ApprovedBy = approver,
            ApprovedAtUtc = now, CreatedAtUtc = now, Note = input.Findings });
        session.ExpectedClosingCash += change;
        session.Version++;
        if (destination is not null)
        {
            decimal destinationChange = -change;
            if (destination.ExpectedClosingCash + destinationChange < destination.ReservedCash)
                throw new BusinessRuleException(MessageCode.InsufficientCashPositionBalance);
            destination.ExpectedClosingCash += destinationChange;
            destination.Version++;
            db.CashMovements.Add(new CashMovement { Session = destination, DestinationSession = session,
                Type = destinationChange > 0m ? "CorrectionIn" : "CorrectionOut", Amount = Math.Abs(destinationChange),
                ActorId = approver, ApprovedBy = approver, ApprovedAtUtc = now, CreatedAtUtc = now,
                Note = input.Findings });
        }
    }

    private async Task<decimal> MovementCorrectionAsync(CashPositionSession session, CashCorrectionInputs input, CancellationToken ct)
    {
        if (input.Kind == CashDiscrepancyKind.MissingMovement)
        {
            if (!input.TransactionId.HasValue || await db.CashMovements.AnyAsync(x => x.TransactionId == input.TransactionId, ct))
                throw new ValidationException(MessageCode.InvalidRequest);
            List<TransactionEntry> entries = await db.TransactionEntries.AsNoTracking().Where(x => x.TransactionId == input.TransactionId &&
                x.PostingDate == session.BusinessDate && x.GlAccount!.Code == AccountingConstants.CashOnHandGlCode).ToListAsync(ct);
            if (entries.Count == 0) throw new ValidationException(MessageCode.InvalidRequest);
            return entries.Sum(x => x.EntryType == EntryType.Debit ? x.Amount : -x.Amount);
        }
        if (!input.MovementId.HasValue) throw new ValidationException(MessageCode.InvalidRequest);
        CashMovement movement = await db.CashMovements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.MovementId &&
            x.SessionId == session.Id && x.Status == OperationsConstants.CashMovementApproved, ct)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        decimal original = movement.Type switch
        {
            "Deposit" or "CorrectionIn" or "TransferIn" or "VaultReturnIn" => movement.Amount,
            "Withdrawal" or "BankExpense" or "CorrectionOut" or "TransferOut" or "VaultReturnOut" or "VaultIssue" => -movement.Amount,
            _ => throw new ValidationException(MessageCode.InvalidRequest)
        };
        if (input.Kind == CashDiscrepancyKind.TransferMismatch && movement.Type is not ("TransferIn" or "TransferOut") ||
            input.Kind == CashDiscrepancyKind.HandoffMismatch && movement.Type is not ("VaultReturnIn" or "VaultReturnOut"))
            throw new ValidationException(MessageCode.InvalidRequest);
        if (input.Kind is CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch &&
            movement.DestinationSessionId != input.DestinationSessionId)
            throw new ValidationException(MessageCode.InvalidRequest);
        return input.Kind switch
        {
            CashDiscrepancyKind.DuplicateMovement or CashDiscrepancyKind.WrongCashPosition => -original,
            CashDiscrepancyKind.IncorrectMovementAmount or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch =>
                (original >= 0m ? input.Amount ?? 0m : -(input.Amount ?? 0m)) - original,
            _ => throw new ValidationException(MessageCode.InvalidRequest)
        };
    }

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
