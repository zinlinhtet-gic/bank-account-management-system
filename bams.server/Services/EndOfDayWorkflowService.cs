using System.Text.Json;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace bams.server.Services;

/// <summary>Coordinates pre-close checks, dual-control approval, and the bank-wide business-date transition.</summary>
public sealed class EndOfDayWorkflowService(
    ApplicationDbContext db,
    IBusinessDateService businessDates,
    ICurrentUserService currentUser,
    IAccountReconciliationService accountReconciliation,
    IEndOfDayAuditService endOfDayAudit) : IEndOfDayWorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken) =>
        businessDates.GetCurrentBusinessDateAsync(cancellationToken);

    public async Task<IReadOnlyList<BusinessDateResponse>> SearchBusinessDatesAsync(DateOnly? fromDate, DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        IQueryable<BusinessDate> query = db.BusinessDates.AsNoTracking();
        if (fromDate.HasValue) query = query.Where(item => item.Date >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(item => item.Date <= toDate.Value);
        return await query.OrderByDescending(item => item.Date)
            .Select(item => new BusinessDateResponse(item.Date, item.Status, item.OpenedAtUtc, item.ClosedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<EndOfDayRunResponse?> GetLatestRunAsync(DateOnly date, CancellationToken cancellationToken)
    {
        EndOfDayRun? run = await db.EndOfDayRuns.Where(item => item.BusinessDate == date).OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return run is null ? null : ToResponse(run, DeserializeStages(run.StageSummaryJson));
    }

    public async Task<EndOfDayRunResponse> RequestForceCloseAsync(DateOnly date, string reason, CancellationToken cancellationToken)
    {
        string normalizedReason = ValidateForceCloseReason(reason);
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockAndValidateBusinessDateAsync(date, cancellationToken);
        await EnsureNoPendingForceCloseAsync(date, cancellationToken);
        List<long> managerIds = await GetActiveManagerIdsAsync(cancellationToken);
        long actorId = currentUser.GetCurrentUserId();
        ValidateForceCloseRequester(managerIds, actorId);
        List<EndOfDayStageResponse> stages = await BuildCompleteStageSummaryAsync(date, cancellationToken);
        EndOfDayRun run = CreateForceCloseRun(date, normalizedReason, managerIds, actorId, stages);
        db.EndOfDayRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (run.Status == "OverrideApproved")
            return await CloseAsync(run.Id, cancellationToken);

        return ToResponse(run, stages);
    }

    private static string ValidateForceCloseReason(string reason)
    {
        string normalizedReason = (reason ?? string.Empty).Trim();
        if (normalizedReason.Length < 5 || normalizedReason.Length > 1000)
            throw new BusinessRuleException(MessageCode.RequiredFieldMissing);
        return normalizedReason;
    }

    private async Task LockAndValidateBusinessDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        List<BusinessDate> businessDates = await db.BusinessDates
            .FromSql($"SELECT * FROM BusinessDates WHERE Date = {date} FOR UPDATE")
            .ToListAsync(cancellationToken);
        BusinessDate? businessDate = businessDates.SingleOrDefault();
        if (businessDate is null)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        await EnsureOldestOpenDateAsync(date, cancellationToken);
    }

    private async Task EnsureNoPendingForceCloseAsync(DateOnly date, CancellationToken cancellationToken)
    {
        bool hasPendingForceClose = await db.EndOfDayRuns.AnyAsync(item => item.BusinessDate == date &&
            (item.Status == "OverridePending" || item.Status == "OverrideApproved"), cancellationToken);
        if (hasPendingForceClose)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
    }

    private static void ValidateForceCloseRequester(IReadOnlyCollection<long> managerIds, long actorId)
    {
        if (managerIds.Count == 0 || !managerIds.Contains(actorId))
            throw new BusinessRuleException(MessageCode.EndOfDayApprovalRequired);
    }

    private async Task<List<EndOfDayStageResponse>> BuildCompleteStageSummaryAsync(DateOnly date,
        CancellationToken cancellationToken)
    {
        List<EndOfDayStageResponse> stages = await BuildPreCloseStagesAsync(date, cancellationToken);
        AccountReconciliationRunResponse accountResults = await accountReconciliation.ReconcileAccountsAsync(
            new AccountReconciliationRequest(date, date, null), cancellationToken);
        int accountExceptions = accountResults.Results.Count(item => item.Status != OperationsConstants.ReconciliationMatched);
        stages.Add(new EndOfDayStageResponse(OperationsConstants.EodStageAccountReconciliation,
            accountExceptions == 0 ? "Complete" : "Exceptions", accountExceptions, null));
        return stages;
    }

    private static EndOfDayRun CreateForceCloseRun(DateOnly date, string reason, IReadOnlyList<long> managerIds,
        long actorId, IReadOnlyList<EndOfDayStageResponse> stages)
    {
        DateTime requestedAtUtc = DateTime.UtcNow;
        List<long> approvedIds = new() { actorId };
        bool isSingleManager = managerIds.Count == 1;
        return new EndOfDayRun
        {
            BusinessDate = date,
            Status = isSingleManager ? "OverrideApproved" : "OverridePending",
            StageSummaryJson = JsonSerializer.Serialize(stages, JsonOptions),
            PreparedBy = actorId,
            PreparedAtUtc = requestedAtUtc,
            ApprovedBy = isSingleManager ? actorId : null,
            ApprovedAtUtc = isSingleManager ? requestedAtUtc : null,
            OverrideReason = reason,
            OverrideRequiredUserIdsJson = JsonSerializer.Serialize(managerIds, JsonOptions),
            OverrideApprovedUserIdsJson = JsonSerializer.Serialize(approvedIds, JsonOptions),
            OverrideApprovalAuditJson = JsonSerializer.Serialize(new Dictionary<long, DateTime> { [actorId] = requestedAtUtc }, JsonOptions)
        };
    }

    public async Task<EndOfDayRunResponse> ApproveForceCloseAsync(long runId, CancellationToken cancellationToken)
    {
        long actorId = currentUser.GetCurrentUserId();
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        EndOfDayRun run = await LoadForceCloseRunForUpdateAsync(runId, cancellationToken);
        ForceCloseApprovalState approvalState = await ValidateForceCloseApprovalAsync(run, actorId, cancellationToken);
        RecordForceCloseApproval(run, actorId, approvalState);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(run, DeserializeStages(run.StageSummaryJson));
    }

    private async Task<EndOfDayRun> LoadForceCloseRunForUpdateAsync(long runId, CancellationToken cancellationToken)
    {
        List<EndOfDayRun> runs = await db.EndOfDayRuns
            .FromSql($"SELECT * FROM EndOfDayRuns WHERE Id = {runId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return runs.SingleOrDefault() ?? throw new NotFoundException(MessageCode.ResourceNotFound);
    }

    private async Task<ForceCloseApprovalState> ValidateForceCloseApprovalAsync(EndOfDayRun run, long actorId,
        CancellationToken cancellationToken)
    {
        if (run.Status != "OverridePending")
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        await EnsureOldestOpenDateAsync(run.BusinessDate, cancellationToken);

        List<long> requiredUserIds = DeserializeUserIds(run.OverrideRequiredUserIdsJson);
        List<long> approvedUserIds = DeserializeUserIds(run.OverrideApprovedUserIdsJson);
        Dictionary<long, DateTime> approvalTimes = DeserializeApprovalTimes(run.OverrideApprovalAuditJson);
        List<long> activeManagerIds = await GetActiveManagerIdsAsync(cancellationToken);
        bool isEligibleApprover = requiredUserIds.Contains(actorId) && activeManagerIds.Contains(actorId);
        bool hasAlreadyApproved = approvedUserIds.Contains(actorId);
        if (!isEligibleApprover || hasAlreadyApproved)
            throw new BusinessRuleException(MessageCode.EndOfDayApprovalRequired);

        return new ForceCloseApprovalState(requiredUserIds, approvedUserIds, approvalTimes);
    }

    private static void RecordForceCloseApproval(EndOfDayRun run, long actorId, ForceCloseApprovalState approvalState)
    {
        DateTime approvalTimeUtc = DateTime.UtcNow;
        approvalState.ApprovedUserIds.Add(actorId);
        approvalState.ApprovalTimesUtc[actorId] = approvalTimeUtc;
        run.OverrideApprovedUserIdsJson = JsonSerializer.Serialize(approvalState.ApprovedUserIds, JsonOptions);
        run.OverrideApprovalAuditJson = JsonSerializer.Serialize(approvalState.ApprovalTimesUtc, JsonOptions);

        if (!approvalState.RequiredUserIds.All(approvalState.ApprovedUserIds.Contains))
            return;

        run.Status = "OverrideApproved";
        run.ApprovedBy = actorId;
        run.ApprovedAtUtc = approvalTimeUtc;
    }

    public async Task<EndOfDayRunResponse> RunPreCloseAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await EnsurePreCloseCanRunAsync(date, cancellationToken);
        List<EndOfDayStageResponse> stages = await BuildCompleteStageSummaryAsync(date, cancellationToken);
        bool blocked = stages.Any(stage => stage.IssueCount > 0 || stage.Status is "Blocked" or "Exceptions");
        DateTime now = DateTime.UtcNow;
        EndOfDayRun run = CreatePreCloseRun(date, stages, blocked, now, currentUser.GetCurrentUserId());
        db.EndOfDayRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(run, stages);
    }

    private async Task EnsurePreCloseCanRunAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await EnsureOldestOpenDateAsync(date, cancellationToken);
        await EnsureNoPendingForceCloseAsync(date, cancellationToken);
    }

    private static EndOfDayRun CreatePreCloseRun(DateOnly date, IReadOnlyList<EndOfDayStageResponse> stages,
        bool isBlocked, DateTime preparedAtUtc, long preparedBy) => new()
    {
        BusinessDate = date,
        Status = isBlocked ? OperationsConstants.EodBlocked : OperationsConstants.EodReadyForApproval,
        StageSummaryJson = JsonSerializer.Serialize(stages, JsonOptions),
        PreparedBy = preparedBy,
        PreparedAtUtc = preparedAtUtc
    };

    public async Task<EndOfDayRunResponse> ApproveAsync(long runId, CancellationToken cancellationToken)
    {
        EndOfDayRun run = await db.EndOfDayRuns.SingleOrDefaultAsync(item => item.Id == runId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        long actorId = currentUser.GetCurrentUserId();
        bool isBusinessDateOpen = await db.BusinessDates.AsNoTracking().AnyAsync(item =>
            item.Date == run.BusinessDate && item.Status == OperationsConstants.BusinessDateOpen, cancellationToken);
        if (!isBusinessDateOpen)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        if (run.Status != OperationsConstants.EodReadyForApproval || actorId == run.PreparedBy)
            throw new BusinessRuleException(MessageCode.EndOfDayApprovalRequired);
        run.ApprovedBy = actorId;
        run.ApprovedAtUtc = DateTime.UtcNow;
        run.Status = OperationsConstants.EodApproved;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(run, DeserializeStages(run.StageSummaryJson));
    }

    public async Task<EndOfDayRunResponse> CloseAsync(long runId, CancellationToken cancellationToken)
    {
        long actorId = currentUser.GetCurrentUserId();
        EndOfDayRun initialRun = await db.EndOfDayRuns.AsNoTracking().SingleOrDefaultAsync(item => item.Id == runId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        bool isManagerOverride = initialRun.Status == "OverrideApproved";
        ValidateInitialCloseRun(initialRun, isManagerOverride);

        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        EndOfDayRun run = (await db.EndOfDayRuns.FromSql($"SELECT * FROM EndOfDayRuns WHERE Id = {runId} FOR UPDATE").ToListAsync(cancellationToken)).Single();
        BusinessDate businessDate = (await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Date = {run.BusinessDate} FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault()
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        isManagerOverride = run.Status == "OverrideApproved";
        await ValidateLockedCloseStateAsync(run, businessDate, isManagerOverride, cancellationToken);
        await EnsureCloseAuditAsync(run.BusinessDate, cancellationToken);
        await ApplyBusinessDateCloseAsync(run, businessDate, actorId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(run, DeserializeStages(run.StageSummaryJson));
    }

    private static void ValidateInitialCloseRun(EndOfDayRun run, bool isManagerOverride)
    {
        bool isApprovedEodRun = run.Status == OperationsConstants.EodApproved;
        bool wasApprovedByAnotherManager = run.ApprovedBy != run.PreparedBy;
        if ((!isManagerOverride && !isApprovedEodRun) || (!isManagerOverride && !wasApprovedByAnotherManager))
            throw new BusinessRuleException(MessageCode.EndOfDayApprovalRequired);
    }

    private async Task ValidateLockedCloseStateAsync(EndOfDayRun run, BusinessDate businessDate,
        bool isManagerOverride, CancellationToken cancellationToken)
    {
        bool isApprovedRun = run.Status == OperationsConstants.EodApproved || isManagerOverride;
        bool isBusinessDateOpen = businessDate.Status == OperationsConstants.BusinessDateOpen;
        if (!isApprovedRun || !isBusinessDateOpen)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

        bool hasEarlierOpenDate = await db.BusinessDates.AnyAsync(item => item.Date < run.BusinessDate &&
            item.Status == OperationsConstants.BusinessDateOpen, cancellationToken);
        if (hasEarlierOpenDate)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

        if (isManagerOverride)
            EnsureAllRequiredManagerApprovals(run);
        else
            await EnsureNoClosingBlockersAsync(run.BusinessDate, cancellationToken);
    }

    private static void EnsureAllRequiredManagerApprovals(EndOfDayRun run)
    {
        List<long> requiredUserIds = DeserializeUserIds(run.OverrideRequiredUserIdsJson);
        List<long> approvedUserIds = DeserializeUserIds(run.OverrideApprovedUserIdsJson);
        bool allRequiredManagersApproved = requiredUserIds.Count > 0 &&
            requiredUserIds.All(approvedUserIds.Contains);
        if (!allRequiredManagersApproved)
            throw new BusinessRuleException(MessageCode.EndOfDayApprovalRequired);
    }

    private async Task EnsureCloseAuditAsync(DateOnly date, CancellationToken cancellationToken)
    {
        // The date lock stays held while audit summaries are generated so posting cannot race the close boundary.
        bool summariesExist = await db.DailySummaries.AnyAsync(item => item.SummaryDate == date, cancellationToken);
        bool auditExists = await db.ReconciliationBatches.AnyAsync(item => item.ReconciliationDate == date &&
            item.ReconciliationType == AuditConstants.EndOfDayReconciliationType, cancellationToken);
        if (!summariesExist && !auditExists)
            await endOfDayAudit.RunEndOfDayAuditAsync(date, cancellationToken);
        else if (summariesExist != auditExists)
            throw new BusinessRuleException(MessageCode.EndOfDayAuditAlreadyProcessed);
    }

    private async Task ApplyBusinessDateCloseAsync(EndOfDayRun run, BusinessDate businessDate, long actorId,
        CancellationToken cancellationToken)
    {
        DateTime closedAtUtc = DateTime.UtcNow;
        businessDate.Status = OperationsConstants.BusinessDateClosed;
        businessDate.ClosedBy = actorId;
        businessDate.ClosedAtUtc = closedAtUtc;
        businessDate.Version++;

        DateOnly nextBusinessDateValue = run.BusinessDate.AddDays(1);
        BusinessDate? nextBusinessDate = await db.BusinessDates.SingleOrDefaultAsync(
            item => item.Date == nextBusinessDateValue, cancellationToken);
        if (nextBusinessDate is null)
        {
            BusinessDate nextBusinessDateRecord = new()
            {
                Date = nextBusinessDateValue,
                Status = OperationsConstants.BusinessDateOpen,
                OpenedBy = actorId,
                OpenedAtUtc = closedAtUtc
            };
            db.BusinessDates.Add(nextBusinessDateRecord);
        }
        else if (nextBusinessDate.Status != OperationsConstants.BusinessDateOpen)
        {
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        }

        run.Status = OperationsConstants.EodClosed;
        run.ClosedBy = actorId;
        run.ClosedAtUtc = closedAtUtc;
    }

    private async Task<List<EndOfDayStageResponse>> BuildPreCloseStagesAsync(DateOnly date, CancellationToken cancellationToken)
    {
        (DateTime startUtc, DateTime endUtc) = BusinessTime.GetUtcRange(date);
        IQueryable<Transaction> dayTransactions = db.Transactions.AsNoTracking().Where(item =>
            item.BusinessDate == date || (item.TransactionAt >= startUtc && item.TransactionAt < endUtc) ||
            db.TransactionEntries.Any(entry => entry.TransactionId == item.Id && entry.PostingDate == date));
        int pending = await dayTransactions.CountAsync(item => item.TransactionStatus == TransactionStatus.Pending, cancellationToken);
        int failed = await dayTransactions.CountAsync(item => item.TransactionStatus == TransactionStatus.Failed, cancellationToken);
        int pendingApprovals = await dayTransactions.CountAsync(item => item.TransactionStatus == TransactionStatus.Authorized, cancellationToken);
        pendingApprovals += await (from movement in db.CashMovements.AsNoTracking()
            join session in db.CashPositionSessions.AsNoTracking() on movement.SessionId equals session.Id
            where movement.Type == OperationsConstants.CashMovementAdjustment &&
                movement.Status == OperationsConstants.CashMovementPendingApproval && session.BusinessDate == date
            select movement.Id).CountAsync(cancellationToken);
        IQueryable<long> postedIds = dayTransactions.Where(item => item.PostedAt.HasValue).Select(item => item.Id);
        int missingEntries = await postedIds.CountAsync(id => !db.TransactionEntries.Any(entry => entry.TransactionId == id), cancellationToken);
        List<TransactionEntryTotals> groupedEntries = await db.TransactionEntries.AsNoTracking().Where(entry => postedIds.Contains(entry.TransactionId))
            .GroupBy(entry => entry.TransactionId)
            .Select(group => new TransactionEntryTotals(
                group.Where(entry => entry.EntryType == EntryType.Debit).Sum(entry => entry.Amount),
                group.Where(entry => entry.EntryType == EntryType.Credit).Sum(entry => entry.Amount)))
            .ToListAsync(cancellationToken);
        int unbalanced = groupedEntries.Count(item => item.Debit != item.Credit);
        DailyEntryTotals? dailyTotals = await db.TransactionEntries.AsNoTracking().Where(entry => entry.PostingDate == date)
            .GroupBy(_ => 1)
            .Select(group => new DailyEntryTotals(
                group.Where(entry => entry.EntryType == EntryType.Debit).Sum(entry => entry.Amount),
                group.Where(entry => entry.EntryType == EntryType.Credit).Sum(entry => entry.Amount)))
            .FirstOrDefaultAsync(cancellationToken);
        bool ledgerUnbalanced = dailyTotals is not null && dailyTotals.Debits != dailyTotals.Credits;
        long actorId = currentUser.GetCurrentUserId();
        await UpsertLedgerReconciliationExceptionAsync(date, dailyTotals?.Debits ?? 0m, dailyTotals?.Credits ?? 0m,
            ledgerUnbalanced, actorId, cancellationToken);
        int openSessions = await db.CashPositionSessions.CountAsync(item => item.BusinessDate == date && item.Status == OperationsConstants.CashSessionOpen, cancellationToken);
        int pendingHandoffs = await db.CashHandoffs.CountAsync(item => item.BusinessDate == date &&
              item.Status != OperationsConstants.CashHandoffAccepted, cancellationToken);
        List<long> resolvedInUnitOfWork = db.ReconciliationExceptions.Local
            .Where(item => item.BusinessDate == date && item.Severity == "Critical" &&
                item.Status == OperationsConstants.ExceptionResolved && db.Entry(item).State == EntityState.Modified)
            .Select(item => item.Id).ToList();
        int criticalExceptions = await db.ReconciliationExceptions.AsNoTracking().CountAsync(item => item.BusinessDate == date &&
            item.Severity == "Critical" && item.Status != OperationsConstants.ExceptionResolved && !resolvedInUnitOfWork.Contains(item.Id), cancellationToken);
        criticalExceptions += db.ChangeTracker.Entries<ReconciliationException>().Count(entry =>
            entry.State == EntityState.Added && entry.Entity.BusinessDate == date && entry.Entity.Severity == "Critical" &&
            entry.Entity.Status != OperationsConstants.ExceptionResolved);
        return
        [
            new(OperationsConstants.EodStageTransactions, pending == 0 && failed == 0 ? "Complete" : "Blocked", pending + failed, null),
            new(OperationsConstants.EodStagePendingApprovals, pendingApprovals == 0 ? "Complete" : "Blocked", pendingApprovals, null),
            new(OperationsConstants.EodStageAccountingEntries, missingEntries + unbalanced == 0 ? "Complete" : "Blocked", missingEntries + unbalanced, null),
            new(OperationsConstants.EodStageLedgerReconciliation, !ledgerUnbalanced ? "Complete" : "Blocked", ledgerUnbalanced ? 1 : 0, null),
              new(OperationsConstants.EodStageCash, openSessions + pendingHandoffs == 0 ? "Complete" : "Blocked",
                  openSessions + pendingHandoffs, openSessions + pendingHandoffs == 0 ? null :
                      $"{openSessions} open cash session(s); {pendingHandoffs} handoff(s) awaiting acceptance or reassignment."),
            new(OperationsConstants.EodStageCriticalExceptions, criticalExceptions == 0 ? "Complete" : "Exceptions", criticalExceptions, null)
        ];
    }

    // Persists a ledger mismatch as an investigation record and resolves it only after a balanced rerun.
    private async Task UpsertLedgerReconciliationExceptionAsync(DateOnly date, decimal debits, decimal credits,
        bool isUnbalanced, long actorId, CancellationToken cancellationToken)
    {
        ReconciliationException? existing = await db.ReconciliationExceptions
            .Where(item => item.Type == OperationsConstants.ExceptionLedgerBalance && item.BusinessDate == date &&
                item.Status != OperationsConstants.ExceptionResolved)
            .OrderByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        DateTime now = DateTime.UtcNow;
        if (isUnbalanced)
        {
            if (existing is null)
            {
                ReconciliationException exception = new()
                {
                    Type = OperationsConstants.ExceptionLedgerBalance,
                    Source = OperationsConstants.EodStageLedgerReconciliation,
                    BusinessDate = date,
                    ExpectedAmount = debits,
                    ActualAmount = credits,
                    Difference = debits - credits,
                    Severity = "Critical",
                    Status = OperationsConstants.ExceptionOpen,
                    CreatedBy = actorId,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                db.ReconciliationExceptions.Add(exception);
                db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
                {
                    Exception = exception,
                    OldStatus = "None",
                    NewStatus = exception.Status,
                    Note = $"Ledger debit total {debits:N2} did not match credit total {credits:N2}.",
                    ActorId = actorId,
                    CreatedAtUtc = now
                });
            }
            else
            {
                db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
                {
                    Exception = existing, OldStatus = existing.Status, NewStatus = existing.Status,
                    Note = $"Ledger rerun. Prior debit {existing.ExpectedAmount:N2}, credit {existing.ActualAmount:N2}; current debit {debits:N2}, credit {credits:N2}.",
                    ActorId = actorId, CreatedAtUtc = now
                });
                existing.ExpectedAmount = debits;
                existing.ActualAmount = credits;
                existing.Difference = debits - credits;
                existing.UpdatedAtUtc = now;
            }
        }
        else if (existing is not null)
        {
            db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
            {
                Exception = existing, OldStatus = existing.Status, NewStatus = OperationsConstants.ExceptionResolved,
                Note = "Ledger debit and credit totals now match.", ActorId = actorId, CreatedAtUtc = now
            });
            existing.ExpectedAmount = debits;
            existing.ActualAmount = credits;
            existing.Difference = 0m;
            existing.Status = OperationsConstants.ExceptionResolved;
            existing.UpdatedAtUtc = now;
        }
    }

    private async Task EnsureNoClosingBlockersAsync(DateOnly date, CancellationToken cancellationToken)
    {
        List<EndOfDayStageResponse> stages = await BuildCompleteStageSummaryAsync(date, cancellationToken);
        if (stages.Any(stage => stage.IssueCount > 0))
            throw new BusinessRuleException(MessageCode.ReconciliationBlocked);
        EndOfDayRun? latest = await db.EndOfDayRuns.Where(item => item.BusinessDate == date && item.Status == OperationsConstants.EodApproved)
            .OrderByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (latest is null) throw new BusinessRuleException(MessageCode.ReconciliationBlocked);
    }

    private static IReadOnlyList<EndOfDayStageResponse> DeserializeStages(string json) =>
        JsonSerializer.Deserialize<List<EndOfDayStageResponse>>(json, JsonOptions) ?? [];

    private async Task EnsureOldestOpenDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        bool isOpen = await db.BusinessDates.AnyAsync(item => item.Date == date &&
            item.Status == OperationsConstants.BusinessDateOpen, cancellationToken);
        bool earlierOpen = await db.BusinessDates.AnyAsync(item => item.Date < date &&
            item.Status == OperationsConstants.BusinessDateOpen, cancellationToken);
        if (!isOpen || earlierOpen) throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
    }

    private async Task<List<long>> GetActiveManagerIdsAsync(CancellationToken cancellationToken) =>
        await (from userRole in db.UserRoles
               join user in db.Users on userRole.UserId equals user.Id
               join rolePermission in db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
               join permission in db.Permissions on rolePermission.PermissionId equals permission.Id
               where user.Status == UserStatus.Active && permission.Code == SecurityConstants.EndOfDayApproval
               select user.Id).Distinct().OrderBy(id => id).ToListAsync(cancellationToken);

    private static List<long> DeserializeUserIds(string? json) => string.IsNullOrWhiteSpace(json)
        ? [] : JsonSerializer.Deserialize<List<long>>(json, JsonOptions) ?? [];

    private static Dictionary<long, DateTime> DeserializeApprovalTimes(string? json) => string.IsNullOrWhiteSpace(json)
        ? new Dictionary<long, DateTime>() : JsonSerializer.Deserialize<Dictionary<long, DateTime>>(json, JsonOptions) ?? new Dictionary<long, DateTime>();

    private sealed record TransactionEntryTotals(decimal Debit, decimal Credit);
    private sealed record DailyEntryTotals(decimal Debits, decimal Credits);
    private sealed record ForceCloseApprovalState(List<long> RequiredUserIds, List<long> ApprovedUserIds,
        Dictionary<long, DateTime> ApprovalTimesUtc);

    private static EndOfDayRunResponse ToResponse(EndOfDayRun run, IReadOnlyList<EndOfDayStageResponse> stages) =>
        new(run.Id, run.BusinessDate, run.Status, run.PreparedAtUtc, stages, run.PreparedBy, run.ApprovedBy, run.ClosedBy)
        {
            OverrideReason = run.OverrideReason,
            OverrideRequiredUserIds = DeserializeUserIds(run.OverrideRequiredUserIdsJson),
            OverrideApprovedUserIds = DeserializeUserIds(run.OverrideApprovedUserIdsJson),
            OverrideApprovalTimesUtc = DeserializeApprovalTimes(run.OverrideApprovalAuditJson)
        };
}
