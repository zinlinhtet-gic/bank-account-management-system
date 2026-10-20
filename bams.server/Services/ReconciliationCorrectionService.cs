using System.Text.Json;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Security;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Owns proposal review transactions and dispatches case-specific correction operations.</summary>
public sealed class ReconciliationCorrectionService(ApplicationDbContext db, ICurrentUserService user,
    CorrectionPlanningService planner, ReconciliationProjectionService projections, ReconciliationCheckService checks,
    UnifiedReconciliationService unified)
{
    /// <summary>Returns proposal history without exposing persistence entities.</summary>
    public async Task<IReadOnlyList<CorrectionProposalResponse>> ListAsync(long? exceptionId, CancellationToken ct)
    {
        IQueryable<CorrectionProposal> query = db.CorrectionProposals.AsNoTracking().Include(proposal => proposal.Exception);
        if (exceptionId.HasValue) query = query.Where(x => x.ExceptionId == exceptionId);
        List<CorrectionProposal> proposals = await query.OrderByDescending(proposal => proposal.Id).Take(500).ToListAsync(ct);
        List<BusinessDate> dates = await db.BusinessDates.AsNoTracking().ToListAsync(ct);
        DateOnly? activeDate = dates.Where(day => day.IsActive && day.Status == OperationsConstants.BusinessDateOpen &&
                day.Date <= bams.server.Utils.BusinessTime.Today).Select(day => (DateOnly?)day.Date).SingleOrDefault();
        List<CorrectionProposalResponse> responses = [];
        foreach (CorrectionProposal proposal in proposals)
        {
            CorrectionProposalResponse response = ToResponse(proposal);
            if (proposal.Status == ProposalStatus.PendingApproval && response.Preview is not null &&
                dates.Any(day => day.Date == proposal.Exception!.BusinessDate && day.Status == OperationsConstants.BusinessDateClosed))
            {
                response = response with
                {
                    Preview = activeDate.HasValue
                        ? response.Preview with
                        {
                            PostingDate = activeDate.Value,
                            Lines = response.Preview.Lines.Select(line => line with { PostingDate = activeDate.Value }).ToList(),
                            Explanation = response.Preview.Explanation + $" Source date {proposal.Exception!.BusinessDate:yyyy-MM-dd} is closed; posting uses the active date."
                        }
                        : response.Preview with { Explanation = response.Preview.Explanation + " Start an active business date before approval." }
                };
            }
            responses.Add(response);
        }
        return responses;
    }

    public async Task<CorrectionProposalResponse> GetAsync(long id, CancellationToken ct) =>
        ToResponse(await db.CorrectionProposals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound));

    /// <summary>Records assignment and diagnosis separately from financial correction approval.</summary>
    public async Task AssignAsync(long exceptionId, InvestigationRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var exception = await LockExceptionAsync(exceptionId, ct);
        if (exception.PositionSessionId.HasValue || exception.Status == OperationsConstants.ExceptionResolved ||
            request.Findings.Length > 2000 || !await db.Users.AnyAsync(x => x.Id == request.InvestigatorId && x.Status == UserStatus.Active, ct))
            throw new ValidationException(MessageCode.InvalidRequest);
        exception.AssignedTo = request.InvestigatorId;
        exception.Notes = request.Findings;
        exception.DiscrepancyKind = request.Kind;
        exception.ReviewDueDate = request.ReviewDueDate;
        AddHistory(exception, OperationsConstants.ExceptionUnderInvestigation, "Investigator assignment/findings updated.");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Saves a draft or an immutable submitted proposal under the exception lock.</summary>
    public async Task<CorrectionProposalResponse> SaveAsync(long exceptionId, long? draftId, SaveCorrectionRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var exception = await LockExceptionAsync(exceptionId, ct);
        var actor = user.GetCurrentUserId();
        if (exception.AssignedTo != actor || exception.Status is OperationsConstants.ExceptionResolved or OperationsConstants.ExceptionForcePassed || exception.PositionSessionId.HasValue)
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        if (await db.CorrectionProposals.AnyAsync(x => x.ExceptionId == exceptionId && x.Status == ProposalStatus.PendingApproval && x.Id != draftId, ct))
            throw new ConflictException(MessageCode.Conflict);
        CorrectionPreview? preview = request.Submit ? await planner.PreviewAsync(exceptionId, request.Inputs, ct) : null;
        if (request.Submit && preview!.SourceHash != request.ExpectedSourceHash)
            throw new ConflictException(MessageCode.CorrectionProposalStale);
        CorrectionProposal proposal;
        if (draftId.HasValue)
        {
            proposal = await db.CorrectionProposals.SingleOrDefaultAsync(x => x.Id == draftId && x.ExceptionId == exceptionId, ct)
                ?? throw new NotFoundException(MessageCode.ResourceNotFound);
            if (proposal.Status != ProposalStatus.Draft || proposal.ProposerId != actor) throw new ConflictException(MessageCode.Conflict);
        }
        else
        {
            proposal = new CorrectionProposal
            {
                ExceptionId = exceptionId, ProposerId = actor, CreatedAtUtc = DateTime.UtcNow,
                Revision = (await db.CorrectionProposals.Where(x => x.ExceptionId == exceptionId).MaxAsync(x => (int?)x.Revision, ct) ?? 0) + 1
            };
            db.CorrectionProposals.Add(proposal);
        }
        proposal.Kind = request.Inputs.Kind;
        proposal.InputsJson = JsonSerializer.Serialize(request.Inputs);
        proposal.PreviewJson = preview is null ? "" : JsonSerializer.Serialize(preview);
        proposal.SourceHash = preview?.SourceHash ?? "";
        proposal.Status = request.Submit ? ProposalStatus.PendingApproval : ProposalStatus.Draft;
        proposal.Version++;
        exception.DiscrepancyKind = request.Inputs.Kind;
        if (request.Submit) AddHistory(exception, OperationsConstants.ExceptionAdjustmentRequired, $"Correction proposal revision {proposal.Revision} submitted.");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(proposal);
    }

    /// <summary>Approves and executes once; approval is rolled back if any financial operation fails.</summary>
    public async Task<CorrectionProposalResponse> ReviewAsync(long id, ReviewCorrectionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000)
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Date locks precede account locks, matching normal posting and EOD ordering.
        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(ct);
        var proposal = (await db.CorrectionProposals.FromSql($"SELECT * FROM CorrectionProposals WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (proposal.Status == ProposalStatus.Executed) return ToResponse(proposal);
        if (proposal.Status != ProposalStatus.PendingApproval) throw new ConflictException(MessageCode.Conflict);
        var actor = user.GetCurrentUserId();
        var managers = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
            join staff in db.Users on ur.UserId equals staff.Id
            where role.Code == SecurityConstants.ManagerRole && staff.Status == UserStatus.Active select staff.Id).Distinct().ToListAsync(ct);
        if (!managers.Contains(actor) || (actor == proposal.ProposerId && (managers.Count != 1 || managers[0] != actor)))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        var exception = await LockExceptionAsync(proposal.ExceptionId, ct);
        if (exception.Status is OperationsConstants.ExceptionResolved or OperationsConstants.ExceptionForcePassed) throw new ConflictException(MessageCode.Conflict);
        var input = JsonSerializer.Deserialize<CorrectionInputs>(proposal.InputsJson)!;
        proposal.ReviewerId = actor;
        proposal.ReviewNote = request.Note;
        proposal.ReviewedAtUtc = DateTime.UtcNow;
        proposal.SoleManagerApproval = actor == proposal.ProposerId;
        proposal.Version++;
        if (!request.Approve)
        {
            proposal.Status = ProposalStatus.Rejected;
            AddHistory(exception, OperationsConstants.ExceptionUnderInvestigation, $"Proposal {id} rejected: {request.Note}");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(input.ExternalRecoveryReference) && string.IsNullOrWhiteSpace(request.RecoveryAttestation))
                throw new ValidationException(MessageCode.RequiredFieldMissing);
            await db.Transactions.FromSql($"SELECT * FROM Transactions WHERE Id = {input.TransactionId} FOR UPDATE").ToListAsync(ct);
            var savedPreview = JsonSerializer.Deserialize<CorrectionPreview>(proposal.PreviewJson)!;
            foreach (var accountId in savedPreview.Balances.Select(x => x.AccountId).Distinct().Order())
                await db.Accounts.FromSql($"SELECT * FROM Accounts WHERE Id = {accountId} FOR UPDATE").ToListAsync(ct);
            var preview = await planner.PreviewAsync(exception.Id, input, ct);
            if (preview.SourceHash != proposal.SourceHash) throw new ConflictException(MessageCode.CorrectionProposalStale);
            if (await db.CorrectionProposals.AnyAsync(x => x.Id != id && x.Status == ProposalStatus.Executed && x.SourceHash == proposal.SourceHash, ct))
                throw new ConflictException(MessageCode.Conflict);
            // Persist the actual execution plan if force close redirected the original proposal to the active date.
            proposal.PreviewJson = JsonSerializer.Serialize(preview);
            await ExecuteDiscrepancyAsync(proposal, input, preview, ct);
            if (proposal.ExecutionTransactionId.HasValue)
                exception.CorrectionTransactionId = proposal.ExecutionTransactionId;
            proposal.Status = ProposalStatus.Executed;
            proposal.Verification = VerificationStatus.Pending;
            proposal.ReviewNote += string.IsNullOrWhiteSpace(request.RecoveryAttestation) ? "" : "\nRecovery: " + request.RecoveryAttestation;
            AddHistory(exception, OperationsConstants.ExceptionAdjustmentRequired,
                $"Proposal {id} executed on {preview.PostingDate:yyyy-MM-dd} for source date {exception.BusinessDate:yyyy-MM-dd}; verification pending. Sole-manager approval: {proposal.SoleManagerApproval}.");
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (request.Approve)
        {
            await tx.DisposeAsync();
            return await VerifyAsync(id, ct);
        }
        return ToResponse(proposal);
    }

    /// <summary>Dispatch is explicit so each discrepancy has its own operational workflow.</summary>
    private Task ExecuteDiscrepancyAsync(CorrectionProposal p, CorrectionInputs input, CorrectionPreview preview, CancellationToken ct) => p.Kind switch
    {
        DiscrepancyKind.MissingGlPosting => CorrectMissingGlPostingAsync(p, input, preview, ct),
        DiscrepancyKind.MissingAccountPosting => CorrectMissingAccountPostingAsync(p, input, preview, ct),
        DiscrepancyKind.IncorrectAmount => CorrectPostingAmountAsync(p, input, preview, ct),
        DiscrepancyKind.WrongCustomerAccount => CorrectCustomerAccountAsync(p, input, preview, ct),
        DiscrepancyKind.WrongGlAccount => ReclassifyGlPostingAsync(p, input, preview, ct),
        DiscrepancyKind.DuplicatePosting => CorrectDuplicatePostingAsync(p, input, preview, ct),
        DiscrepancyKind.WrongDirection => CorrectPostingDirectionAsync(p, input, preview, ct),
        DiscrepancyKind.PostingDateMismatch => CorrectPostingDateMismatchAsync(p, input, preview, ct),
        DiscrepancyKind.IncompleteJournal => RepairIncompleteJournalAsync(p, input, preview, ct),
        DiscrepancyKind.BalanceSnapshot => RepairAccountBalanceSnapshotAsync(p, input, preview, ct),
        DiscrepancyKind.OpeningBalance => CorrectOpeningBalanceAsync(p, input, preview, ct),
        _ => throw new ValidationException(MessageCode.InvalidRequest)
    };

    private Task CorrectMissingGlPostingAsync(CorrectionProposal p, CorrectionInputs i, CorrectionPreview v, CancellationToken ct) => PostPlanAsync(p, i, v, ct);
    private Task CorrectMissingAccountPostingAsync(CorrectionProposal p, CorrectionInputs i, CorrectionPreview v, CancellationToken ct) => PostPlanAsync(p, i, v, ct);
    private Task CorrectPostingAmountAsync(CorrectionProposal p, CorrectionInputs i, CorrectionPreview v, CancellationToken ct) => PostPlanAsync(p, i, v, ct);
    private Task CorrectCustomerAccountAsync(CorrectionProposal p, CorrectionInputs i, CorrectionPreview v, CancellationToken ct) => PostPlanAsync(p, i, v, ct);
    private Task ReclassifyGlPostingAsync(CorrectionProposal p, CorrectionInputs i, CorrectionPreview v, CancellationToken ct) => PostPlanAsync(p, i, v, ct);
    private Task CorrectDuplicatePostingAsync(CorrectionProposal p, CorrectionInputs i, CorrectionPreview v, CancellationToken ct) => PostPlanAsync(p, i, v, ct);
    private Task CorrectPostingDirectionAsync(CorrectionProposal p, CorrectionInputs i, CorrectionPreview v, CancellationToken ct) => PostPlanAsync(p, i, v, ct);
    private Task CorrectPostingDateMismatchAsync(CorrectionProposal p, CorrectionInputs i, CorrectionPreview v, CancellationToken ct) => PostPlanAsync(p, i, v, ct);
    private Task RepairIncompleteJournalAsync(CorrectionProposal p, CorrectionInputs i, CorrectionPreview v, CancellationToken ct) => PostPlanAsync(p, i, v, ct);
    private Task CorrectOpeningBalanceAsync(CorrectionProposal p, CorrectionInputs i, CorrectionPreview v, CancellationToken ct) => PostPlanAsync(p, i, v, ct);

    // Calculation repairs have no synthetic transaction and no accounting entry.
    private async Task RepairAccountBalanceSnapshotAsync(CorrectionProposal proposal, CorrectionInputs input, CorrectionPreview preview, CancellationToken ct)
    {
        foreach (var balance in preview.Balances) await projections.RebuildAccountAsync(balance.AccountId, proposal.Id, ct);
        await db.SaveChangesAsync(ct);
        await projections.GenerateProvisionalAsync(preview.PostingDate, ct);
    }

    // Write the server-calculated plan once, preserving all original records.
    private async Task PostPlanAsync(CorrectionProposal proposal, CorrectionInputs input, CorrectionPreview preview, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var type = preview.IsJournalRepair ? TransactionType.ReconciliationRepair :
            preview.Lines.All(x => x.Ledger == CorrectionPlanningService.CustomerLedger) ? TransactionType.AccountPostingRepair : TransactionType.ReconciliationAdjustment;
        var transaction = LedgerPostingService.CreateTransaction(type, TransactionStatus.Completed, preview.SuggestedAmount,
            $"Reconciliation proposal {proposal.Id}: {proposal.Kind}", $"EX-{proposal.ExceptionId}", $"RECON-{proposal.Id}", user.GetCurrentUserId(), now);
        transaction.BusinessDate = preview.PostingDate;
        transaction.AuthorizedBy = user.GetCurrentUserId();
        transaction.AuthorizedAt = now;
        if (preview.IsJournalRepair) transaction.AccountingRootTransactionId = input.TransactionId;
        if (proposal.Kind == DiscrepancyKind.DuplicatePosting && !input.GlEntryId.HasValue && !input.AccountEntryId.HasValue)
            transaction.ReversalOfTransactionId = input.TransactionId;
        db.Transactions.Add(transaction);
        foreach (var line in preview.Lines)
        {
            if (line.Ledger == CorrectionPlanningService.GeneralLedger)
                db.TransactionEntries.Add(new TransactionEntry { Transaction = transaction, GlAccountId = line.AccountId,
                    CustomerAccountId = line.CustomerAccountId, EntryType = line.Direction, Amount = line.Amount,
                    PostingDate = line.PostingDate, Description = transaction.Description, CreatedAt = now });
            else await AppendAccountEntryAsync(transaction, line, now, ct);
        }
        await db.SaveChangesAsync(ct);
        proposal.ExecutionTransactionId = transaction.Id;
        foreach (var balance in preview.Balances)
        {
            await projections.RebuildAccountAsync(balance.AccountId, proposal.Id, ct);
            if ((await db.Accounts.SingleAsync(x => x.Id == balance.AccountId, ct)).LedgerBalance != balance.After)
                throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete);
        }
        await db.SaveChangesAsync(ct);
        await projections.GenerateProvisionalAsync(preview.Lines.Select(x => x.PostingDate).Append(preview.PostingDate).Min(), ct);
    }

    // Original snapshots remain immutable; this new entry records the live balance effect.
    private async Task AppendAccountEntryAsync(Transaction transaction, CorrectionLine line, DateTime now, CancellationToken ct)
    {
        var account = await db.Accounts.SingleAsync(x => x.Id == line.AccountId, ct);
        var before = account.LedgerBalance;
        var available = account.AvailableBalance;
        var delta = CorrectionPlanningService.CustomerEffect(line);
        account.LedgerBalance += delta;
        account.AvailableBalance += delta;
        account.Version++;
        account.UpdatedAt = now;
        db.AccountTransactions.Add(new AccountTransaction
        {
            Transaction = transaction, AccountId = account.Id, EntryType = line.Direction, Amount = line.Amount,
            LedgerBalanceBefore = before, LedgerBalanceAfter = account.LedgerBalance,
            AvailableBalanceBefore = available, AvailableBalanceAfter = account.AvailableBalance,
            PostingDate = line.PostingDate, ValueDate = line.PostingDate, CreatedAt = now,
            Description = transaction.Description, ReferenceNo = transaction.ReferenceNo, Status = TransactionConstants.CompletedStatus
        });
    }

    /// <summary>Retries checks without replaying financial execution.</summary>
    public async Task<CorrectionProposalResponse> VerifyAsync(long id, CancellationToken ct)
    {
        await using var verificationTransaction = await db.Database.BeginTransactionAsync(ct);
        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(ct);
        var proposal = (await db.CorrectionProposals.FromSql($"SELECT * FROM CorrectionProposals WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (proposal.Status != ProposalStatus.Executed) throw new BusinessRuleException(MessageCode.InvalidRequest);
        ReconciliationException exception = await LockExceptionAsync(proposal.ExceptionId, ct);
        await unified.RequireVerificationAccessAsync(exception, ct);
        if (proposal.Verification == VerificationStatus.Passed) return ToResponse(proposal);
        var preview = JsonSerializer.Deserialize<CorrectionPreview>(proposal.PreviewJson)!;
        var end = await db.BusinessDates.MaxAsync(x => x.Date, ct);
        var start = preview.Lines.Select(x => x.PostingDate).Append(exception.BusinessDate).Min();
        bool sourceIsClosed = await db.BusinessDates.AnyAsync(day => day.Date == exception.BusinessDate &&
            day.Status == OperationsConstants.BusinessDateClosed, ct);
        // Historical snapshots retain the original mismatch; current-date evidence verifies the later correction.
        if (sourceIsClosed)
            start = preview.PostingDate;
        var passed = true;
        var scope = preview.Balances.Select(x => x.AccountId).Concat(preview.Lines.Where(x => x.CustomerAccountId.HasValue).Select(x => x.CustomerAccountId!.Value)).ToHashSet();
        if (exception.AccountId.HasValue) scope.Add(exception.AccountId.Value);
        // A bounded range keeps the existing reconciliation limit while permitting long investigations.
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var run = await checks.RunAsync(new(date, date, null), ct);
            passed &= run.Results.Where(x => scope.Contains(x.AccountId)).All(x => x.Difference == 0);
        }
        bool journalPassed = await ReconciliationLedgerQueries.Amounts(db)
            .Where(line => line.ReconciliationDate >= start && line.ReconciliationDate <= end)
            .GroupBy(line => line.ReconciliationDate)
            .Select(group => group.Sum(line => line.Direction == EntryType.Debit ? line.Amount : -line.Amount))
            .AllAsync(total => total == 0m, ct);
        passed &= journalPassed;
        if (sourceIsClosed && exception.Type == OperationsConstants.ExceptionLedgerBalance)
        {
            decimal sourceDifference = await ReconciliationLedgerQueries.Amounts(db)
                .Where(line => line.ReconciliationDate == exception.BusinessDate)
                .SumAsync(line => line.Direction == EntryType.Debit ? line.Amount : -line.Amount, ct);
            passed &= sourceDifference == 0m;
        }
        var originalId = JsonSerializer.Deserialize<CorrectionInputs>(proposal.InputsJson)!.TransactionId;
        var effectiveJournal = await db.TransactionEntries.Where(x => x.TransactionId == originalId || x.Transaction!.AccountingRootTransactionId == originalId).ToListAsync(ct);
        passed &= effectiveJournal.Count > 0 && effectiveJournal.Sum(x => x.EntryType == EntryType.Debit ? x.Amount : -x.Amount) == 0;
        proposal.Verification = passed ? VerificationStatus.Passed : VerificationStatus.Failed;
        proposal.VerifiedAtUtc = DateTime.UtcNow;
        proposal.VerificationNote = passed
            ? sourceIsClosed
                ? $"Current-date account and journal checks passed for correction posted on {preview.PostingDate:yyyy-MM-dd}; source date {exception.BusinessDate:yyyy-MM-dd} remains closed."
                : "Account and journal checks passed through the latest business date."
            : "A discrepancy remains; investigate the check results before proposing further corrections.";
        AddHistory(exception, passed ? OperationsConstants.ExceptionResolved : OperationsConstants.ExceptionUnderInvestigation, proposal.VerificationNote);
        await db.SaveChangesAsync(ct);
        if (sourceIsClosed && passed)
            await checks.RunAsync(new(exception.BusinessDate, exception.BusinessDate, null), ct);
        await projections.GenerateProvisionalAsync(start, ct);
        await verificationTransaction.CommitAsync(ct);
        return ToResponse(proposal);
    }

    private async Task<ReconciliationException> LockExceptionAsync(long id, CancellationToken ct) =>
        (await db.ReconciliationExceptions.FromSql($"SELECT * FROM ReconciliationExceptions WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
        ?? throw new NotFoundException(MessageCode.ResourceNotFound);

    private void AddHistory(ReconciliationException exception, string status, string note)
    {
        db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory { Exception = exception,
            OldStatus = exception.Status, NewStatus = status, Note = note, ActorId = user.GetCurrentUserId(), CreatedAtUtc = DateTime.UtcNow });
        exception.Status = status;
        exception.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static CorrectionProposalResponse ToResponse(CorrectionProposal p) => new(p.Id, p.ExceptionId, p.Revision,
        p.Kind, p.Status, p.Verification, p.ProposerId, p.ReviewerId, p.SoleManagerApproval,
        JsonSerializer.Deserialize<CorrectionInputs>(p.InputsJson)!, string.IsNullOrEmpty(p.PreviewJson) ? null : JsonSerializer.Deserialize<CorrectionPreview>(p.PreviewJson),
        p.ExecutionTransactionId, p.ReviewNote, p.VerificationNote, p.CreatedAtUtc);
}

