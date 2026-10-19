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
    IEndOfDayAuditService endOfDayAudit,
    ReconciliationProjectionService projections, UnifiedReconciliationService checks,
    ReconciliationExclusionService exclusions) : IEndOfDayWorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken) =>
        businessDates.GetCurrentBusinessDateAsync(cancellationToken);

    public Task<BusinessDateResponse> StartCurrentBusinessDateAsync(StartBusinessDateRequest request, CancellationToken cancellationToken) =>
        businessDates.StartCurrentBusinessDateAsync(request, cancellationToken);

    public async Task<IReadOnlyList<BusinessDateResponse>> SearchBusinessDatesAsync(DateOnly? fromDate, DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        DateOnly today = BusinessTime.Today;
        IQueryable<BusinessDate> query = db.BusinessDates.AsNoTracking().Where(item => item.Date <= today);
        if (fromDate.HasValue) query = query.Where(item => item.Date >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(item => item.Date <= toDate.Value);
        return await query.OrderByDescending(item => item.Date)
            .Select(item => new BusinessDateResponse(item.Date, item.Status, item.OpenedAtUtc, item.ClosedAtUtc, item.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<EndOfDayRunResponse?> GetLatestRunAsync(DateOnly date, CancellationToken cancellationToken)
    {
        EndOfDayRun? run = await db.EndOfDayRuns.Where(item => item.BusinessDate == date).OrderByDescending(item => item.Status == OperationsConstants.EodClosed).ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null) return null;
        EndOfDayRun? pending = await db.EndOfDayRuns.AsNoTracking().Where(item => item.BusinessDate == date && item.Status == "OverridePending")
            .OrderByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        return ToResponse(run, DeserializeStages(run.StageSummaryJson)) with
        {
            PendingForceClose = pending is not null && pending.Id != run.Id
                ? ToResponse(pending, DeserializeStages(pending.StageSummaryJson)) : null
        };
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
        List<EndOfDayStageResponse> stages = await BuildForceCloseStagesAsync(date, cancellationToken);
        EndOfDayRun run = CreateForceCloseRun(date, normalizedReason, managerIds, actorId, stages);
        db.EndOfDayRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        if (run.Status == "OverrideApproved")
        {
            EndOfDayRunResponse closedRun = await CloseWithinTransactionAsync(run.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return closedRun;
        }

        await transaction.CommitAsync(cancellationToken);
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
        if (date > BusinessTime.Today)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(cancellationToken);
        List<BusinessDate> businessDates = await db.BusinessDates
            .FromSql($"SELECT * FROM BusinessDates WHERE Date = {date} FOR UPDATE")
            .ToListAsync(cancellationToken);
        BusinessDate? businessDate = businessDates.SingleOrDefault();
        // Earlier dates remain eligible for EOD after ordinary posting moves to a newer active date.
        if (businessDate is null || businessDate.Status != OperationsConstants.BusinessDateOpen)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
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
        UnifiedReconciliationResponse results = await checks.RunAsync(new UnifiedReconciliationRequest(date, date), cancellationToken);
        List<EndOfDayStageResponse> stages = await BuildPreCloseStagesAsync(date, cancellationToken);
        int accountExceptions = results.AccountAndGl.Count;
        stages.Add(new EndOfDayStageResponse(OperationsConstants.EodStageAccountReconciliation,
            accountExceptions == 0 ? "Complete" : "Exceptions", accountExceptions, null));
        stages.Add(new EndOfDayStageResponse("CashMismatch", results.Cash.Count == 0 ? "Complete" : "Exceptions",
            results.Cash.Count, null));
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
        EndOfDayRun run = await LockCloseRunAsync(runId, cancellationToken);
        if (run.Status == OperationsConstants.EodClosed)
        {
            ValidateOverrideReplay(run, actorId);
            return ToResponse(run, DeserializeStages(run.StageSummaryJson));
        }
        if (run.Status == OperationsConstants.EodOverrideCancelled)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        await EnsureDateOpenAsync(run.BusinessDate, cancellationToken);
        if (DeserializeUserIds(run.OverrideApprovedUserIdsJson).Contains(actorId))
        {
            ValidateOverrideReplay(run, actorId);
            if (run.Status == "OverrideApproved")
            {
                EndOfDayRunResponse closed = await CloseWithinTransactionAsync(runId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return closed;
            }
            return ToResponse(run, DeserializeStages(run.StageSummaryJson));
        }
        ForceCloseApprovalState approvalState = await ValidateForceCloseApprovalAsync(run, actorId, cancellationToken);
        RecordForceCloseApproval(run, actorId, approvalState);
        await db.SaveChangesAsync(cancellationToken);
        EndOfDayRunResponse response = run.Status == "OverrideApproved"
            ? await CloseWithinTransactionAsync(runId, cancellationToken)
            : ToResponse(run, DeserializeStages(run.StageSummaryJson));
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private static void ValidateOverrideReplay(EndOfDayRun run, long actorId)
    {
        if (run.OverrideReason is null || !DeserializeUserIds(run.OverrideRequiredUserIdsJson).Contains(actorId))
            throw new ForbiddenException(MessageCode.EndOfDayApprovalRequired);
    }

    // All EOD mutations take business-date locks before the run lock, matching correction posting.
    private async Task<EndOfDayRun> LockCloseRunAsync(long runId, CancellationToken ct)
    {
        EndOfDayRun source = await db.EndOfDayRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == runId, ct)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(ct);
        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Date = {source.BusinessDate} FOR UPDATE").ToListAsync(ct);
        return await LoadForceCloseRunForUpdateAsync(runId, ct);
    }

    // Observe blockers without running validations that an authorized force close expressly bypasses.
    private async Task<List<EndOfDayStageResponse>> BuildForceCloseStagesAsync(DateOnly date, CancellationToken ct)
    {
        List<EndOfDayStageResponse> stages = await BuildPreCloseStagesAsync(date, ct, persistLedgerException: false);
        int account = await db.ReconciliationExceptions.CountAsync(x => x.BusinessDate == date && x.PositionSessionId == null &&
            x.Status != OperationsConstants.ExceptionResolved && x.Status != OperationsConstants.ExceptionForcePassed, ct);
        int cash = await db.ReconciliationExceptions.CountAsync(x => x.BusinessDate == date && x.PositionSessionId != null &&
            x.Status != OperationsConstants.ExceptionResolved && x.Status != OperationsConstants.ExceptionForcePassed, ct);
        int proposals = await db.CorrectionProposals.CountAsync(x => x.Exception!.BusinessDate == date &&
            (x.Status == ProposalStatus.PendingApproval || x.Status == ProposalStatus.Executed && x.Verification != VerificationStatus.Passed), ct);
        proposals += await db.CashCorrectionProposals.CountAsync(x => x.Exception!.BusinessDate == date &&
            (x.Status == ProposalStatus.PendingApproval || x.Status == ProposalStatus.Executed && x.Verification != VerificationStatus.Passed), ct);
        stages.Add(new(OperationsConstants.EodStageAccountReconciliation, account == 0 ? "Complete" : "Exceptions", account, null));
        stages.Add(new("CashMismatch", cash == 0 ? "Complete" : "Exceptions", cash, null));
        stages.Add(new("CorrectionProposals", proposals == 0 ? "Complete" : "Blocked", proposals, null));
        return stages;
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
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockAndValidateBusinessDateAsync(date, cancellationToken);
        await EnsurePreCloseCanRunAsync(date, cancellationToken);
        List<EndOfDayStageResponse> stages = await BuildCompleteStageSummaryAsync(date, cancellationToken);
        bool blocked = stages.Any(stage => stage.IssueCount > 0 || stage.Status is "Blocked" or "Exceptions");
        DateTime now = DateTime.UtcNow;
        EndOfDayRun run = CreatePreCloseRun(date, stages, blocked, now, currentUser.GetCurrentUserId());
        db.EndOfDayRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(run, stages);
    }

    public async Task<EndOfDayRunResponse> ReviewPreCloseAsync(DateOnly date, CancellationToken cancellationToken)
    {
        long reviewerId = currentUser.GetCurrentUserId();
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockAndValidateBusinessDateAsync(date, cancellationToken);

        EndOfDayRun? latestRun = await db.EndOfDayRuns
            .Where(item => item.BusinessDate == date && item.OverrideReason == null)
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestRun is null || latestRun.Status != OperationsConstants.EodReadyForApproval)
            throw new BusinessRuleException(MessageCode.EndOfDayApprovalRequired);
        EndOfDayRun run = latestRun;

        if (run.PreparedBy == reviewerId && !await IsSoleActiveManagerAsync(reviewerId, cancellationToken))
            throw new BusinessRuleException(MessageCode.EndOfDayApprovalRequired);

        List<EndOfDayStageResponse> stages = await BuildCompleteStageSummaryAsync(date, cancellationToken);
        bool blocked = stages.Any(stage => stage.IssueCount > 0 || stage.Status is "Blocked" or "Exceptions");
        run.StageSummaryJson = JsonSerializer.Serialize(stages, JsonOptions);
        run.Status = blocked ? OperationsConstants.EodBlocked : OperationsConstants.EodReadyForApproval;
        run.ReviewedBy = reviewerId;
        run.ReviewedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(run, stages);
    }

    private async Task EnsurePreCloseCanRunAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await EnsureDateOpenAsync(date, cancellationToken);
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
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        EndOfDayRun run = await LockCloseRunAsync(runId, cancellationToken);
        long actorId = currentUser.GetCurrentUserId();
        bool isBusinessDateOpen = await db.BusinessDates.AsNoTracking().AnyAsync(item =>
            item.Date == run.BusinessDate && item.Status == OperationsConstants.BusinessDateOpen, cancellationToken);
        if (!isBusinessDateOpen)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        bool isSoleManagerSelfApproval = actorId == run.PreparedBy &&
            await IsSoleActiveManagerAsync(actorId, cancellationToken);
        if (run.Status != OperationsConstants.EodReadyForApproval ||
            (actorId == run.PreparedBy && !isSoleManagerSelfApproval) ||
            run.ReviewedBy != actorId || !run.ReviewedAtUtc.HasValue)
            throw new BusinessRuleException(MessageCode.EndOfDayApprovalRequired);
        run.ApprovedBy = actorId;
        run.ApprovedAtUtc = DateTime.UtcNow;
        run.Status = OperationsConstants.EodApproved;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(run, DeserializeStages(run.StageSummaryJson));
    }

    public async Task<EndOfDayRunResponse> CloseAsync(long runId, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        EndOfDayRunResponse response = await CloseWithinTransactionAsync(runId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    // Applies a close inside the caller's transaction so override request, approval, and automatic close commit atomically.
    private async Task<EndOfDayRunResponse> CloseWithinTransactionAsync(long runId, CancellationToken cancellationToken)
    {
        long actorId = currentUser.GetCurrentUserId();
        EndOfDayRun run = await LockCloseRunAsync(runId, cancellationToken);
        if (run.Status == OperationsConstants.EodClosed)
            return ToResponse(run, DeserializeStages(run.StageSummaryJson));
        bool isManagerOverride = run.Status == "OverrideApproved";
        bool isSoleManagerSelfApproval = !isManagerOverride && run.PreparedBy == actorId &&
            run.ApprovedBy == actorId && run.ReviewedBy == actorId &&
            await IsSoleActiveManagerAsync(actorId, cancellationToken);
        ValidateInitialCloseRun(run, isManagerOverride, isSoleManagerSelfApproval);
        BusinessDate businessDate = await db.BusinessDates.SingleAsync(x => x.Date == run.BusinessDate, cancellationToken);
        if (isManagerOverride)
            run.StageSummaryJson = JsonSerializer.Serialize(await BuildForceCloseStagesAsync(run.BusinessDate, cancellationToken), JsonOptions);
        await ValidateLockedCloseStateAsync(run, businessDate, isManagerOverride, isSoleManagerSelfApproval, cancellationToken);
        await EnsureCloseAuditAsync(run.BusinessDate, isManagerOverride, cancellationToken);
        await projections.GenerateDateAsync(run.BusinessDate, true, cancellationToken);
        if (!isManagerOverride)
            await CancelPendingForceCloseAsync(run, actorId, cancellationToken);
        else
            await SupersedeNormalCloseRunsAsync(run, actorId, cancellationToken);
        await ApplyBusinessDateCloseAsync(run, businessDate, actorId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(run, DeserializeStages(run.StageSummaryJson));
    }

    // Cancellation and normal closure commit together; failed close checks leave the override available.
    private async Task CancelPendingForceCloseAsync(EndOfDayRun normalRun, long actorId, CancellationToken ct)
    {
        List<EndOfDayRun> pending = await db.EndOfDayRuns
            .FromSql($"SELECT * FROM EndOfDayRuns WHERE BusinessDate = {normalRun.BusinessDate} AND Status = 'OverridePending' ORDER BY Id FOR UPDATE").ToListAsync(ct);
        DateTime cancelledAt = DateTime.UtcNow;
        foreach (EndOfDayRun request in pending)
        {
            request.Status = OperationsConstants.EodOverrideCancelled;
            List<EndOfDayStageResponse> stages = DeserializeStages(request.StageSummaryJson).ToList();
            stages.Add(new("OverrideCancellation", OperationsConstants.EodOverrideCancelled, 0,
                $"Cancelled by successful normal-close run #{normalRun.Id}, user #{actorId}, at {cancelledAt:O}."));
            request.StageSummaryJson = JsonSerializer.Serialize(stages, JsonOptions);
        }
    }

    // A successful force close stops every unfinished normal-close attempt for the same date.
    private async Task SupersedeNormalCloseRunsAsync(EndOfDayRun forceRun, long actorId, CancellationToken ct)
    {
        List<EndOfDayRun> normalRuns = await db.EndOfDayRuns
            .FromSql($"SELECT * FROM EndOfDayRuns WHERE BusinessDate = {forceRun.BusinessDate} AND OverrideReason IS NULL AND Status <> 'Closed' AND Status <> 'Superseded' ORDER BY Id FOR UPDATE").ToListAsync(ct);
        DateTime supersededAt = DateTime.UtcNow;
        foreach (EndOfDayRun normal in normalRuns)
        {
            normal.Status = OperationsConstants.EodSuperseded;
            List<EndOfDayStageResponse> stages = DeserializeStages(normal.StageSummaryJson).ToList();
            stages.Add(new("NormalCloseSuperseded", OperationsConstants.EodSuperseded, 0,
                $"Stopped by force-close run #{forceRun.Id}, user #{actorId}, at {supersededAt:O}."));
            normal.StageSummaryJson = JsonSerializer.Serialize(stages, JsonOptions);
        }
    }

    private static void ValidateInitialCloseRun(EndOfDayRun run, bool isManagerOverride, bool isSoleManagerSelfApproval)
    {
        bool isApprovedEodRun = run.Status == OperationsConstants.EodApproved;
        bool wasApprovedByAnotherManager = run.ApprovedBy != run.PreparedBy;
        bool wasApprovedBySoleManager = isSoleManagerSelfApproval && run.ApprovedBy == run.PreparedBy &&
            run.ReviewedBy == run.PreparedBy;
        if ((!isManagerOverride && !isApprovedEodRun) ||
            (!isManagerOverride && !wasApprovedByAnotherManager && !wasApprovedBySoleManager))
            throw new BusinessRuleException(MessageCode.EndOfDayApprovalRequired);
    }

    private async Task ValidateLockedCloseStateAsync(EndOfDayRun run, BusinessDate businessDate,
        bool isManagerOverride, bool isSoleManagerSelfApproval, CancellationToken cancellationToken)
    {
        bool hasIndependentApproval = run.ApprovedBy != run.PreparedBy ||
            (isSoleManagerSelfApproval && run.ApprovedBy == run.PreparedBy && run.ReviewedBy == run.PreparedBy);
        bool isApprovedRun = (run.Status == OperationsConstants.EodApproved && hasIndependentApproval) || isManagerOverride;
        // Closing an earlier open date must not depend on which date currently accepts ordinary postings.
        bool isBusinessDateOpen = businessDate.Status == OperationsConstants.BusinessDateOpen;
        if (!isApprovedRun || !isBusinessDateOpen)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

        if (isManagerOverride)
        {
            EnsureAllRequiredManagerApprovals(run);
            return;
        }

        await exclusions.ValidateAsync(run.BusinessDate, run.BusinessDate, cancellationToken);
        if (await db.ReconciliationExceptions.AnyAsync(x => x.BusinessDate == run.BusinessDate &&
                x.Status != OperationsConstants.ExceptionResolved && x.Status != OperationsConstants.ExceptionForcePassed, cancellationToken) ||
            await db.CorrectionProposals.AnyAsync(x => x.Exception!.BusinessDate == run.BusinessDate && x.Exception.Status != OperationsConstants.ExceptionForcePassed && (x.Status == ProposalStatus.PendingApproval || x.Status == ProposalStatus.Executed && x.Verification != VerificationStatus.Passed), cancellationToken) ||
            await db.CashCorrectionProposals.AnyAsync(x => x.Exception!.BusinessDate == run.BusinessDate && x.Exception.Status != OperationsConstants.ExceptionForcePassed && (x.Status == ProposalStatus.PendingApproval || x.Status == ProposalStatus.Executed && x.Verification != VerificationStatus.Passed), cancellationToken) ||
            await db.BankExpenseRequests.AnyAsync(x => x.BusinessDate == run.BusinessDate && x.Status == "PendingApproval", cancellationToken))
            throw new BusinessRuleException(MessageCode.ReconciliationBlocked);
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

    private async Task EnsureCloseAuditAsync(DateOnly date, bool isManagerOverride, CancellationToken cancellationToken)
    {
        if (!isManagerOverride)
            await exclusions.ValidateAsync(date, date, cancellationToken);
        // The date lock stays held while audit summaries are generated so posting cannot race the close boundary.
        bool summariesExist = await db.DailySummaries.AnyAsync(item => item.SummaryDate == date, cancellationToken);
        bool auditExists = await db.ReconciliationBatches.AnyAsync(item => item.ReconciliationDate == date &&
            item.ReconciliationType == AuditConstants.EndOfDayReconciliationType, cancellationToken);
        if (!summariesExist && !auditExists)
            await endOfDayAudit.RunEndOfDayAuditAsync(date, cancellationToken, isManagerOverride);
        else if (summariesExist && !auditExists)
            throw new BusinessRuleException(MessageCode.EndOfDayAuditAlreadyProcessed);
    }

    private async Task ApplyBusinessDateCloseAsync(EndOfDayRun run, BusinessDate businessDate, long actorId,
        CancellationToken cancellationToken)
    {
        DateTime closedAtUtc = DateTime.UtcNow;
        businessDate.Status = OperationsConstants.BusinessDateClosed;
        businessDate.IsActive = false;
        businessDate.ClosedBy = actorId;
        businessDate.ClosedAtUtc = closedAtUtc;
        businessDate.Version++;

        run.Status = OperationsConstants.EodClosed;
        run.ClosedBy = actorId;
        run.ClosedAtUtc = closedAtUtc;
    }

    private async Task<List<EndOfDayStageResponse>> BuildPreCloseStagesAsync(DateOnly date, CancellationToken cancellationToken, bool persistLedgerException = true)
    {
        (DateTime startUtc, DateTime endUtc) = BusinessTime.GetUtcRange(date);
        IQueryable<Transaction> dayTransactions = db.Transactions.AsNoTracking().Where(item =>
            item.BusinessDate == date || (item.TransactionAt >= startUtc && item.TransactionAt < endUtc) ||
            db.TransactionEntries.Any(entry => (entry.TransactionId == item.Id || entry.Transaction!.AccountingRootTransactionId == item.Id) &&
                entry.PostingDate == date));
        int pending = await dayTransactions.CountAsync(item => item.TransactionStatus == TransactionStatus.Pending, cancellationToken);
        int failed = await dayTransactions.CountAsync(item => item.TransactionStatus == TransactionStatus.Failed, cancellationToken);
        int pendingApprovals = await dayTransactions.CountAsync(item => item.TransactionStatus == TransactionStatus.Authorized, cancellationToken);
        pendingApprovals += await (from movement in db.CashMovements.AsNoTracking()
            join session in db.CashPositionSessions.AsNoTracking() on movement.SessionId equals session.Id
            where movement.Type == OperationsConstants.CashMovementAdjustment &&
                movement.Status == OperationsConstants.CashMovementPendingApproval && session.BusinessDate == date
            select movement.Id).CountAsync(cancellationToken);
        IQueryable<long> postedIds = dayTransactions.Where(item => item.PostedAt.HasValue && item.AccountingRootTransactionId == null && item.TransactionType != TransactionType.AccountPostingRepair).Select(item => item.Id);
        HashSet<long> waivedTransactions = await exclusions.ActiveSourceIdsAsync(date, "Transaction", cancellationToken);
        HashSet<long> waivedLines = await exclusions.ActiveSourceIdsAsync(date, "GL", cancellationToken);
        int missingEntries = await postedIds.CountAsync(id => !waivedTransactions.Contains(id) &&
            !db.TransactionEntries.Any(entry => entry.TransactionId == id || entry.Transaction!.AccountingRootTransactionId == id), cancellationToken);
        List<TransactionEntryTotals> groupedEntries = await db.TransactionEntries.AsNoTracking().Where(entry =>
                postedIds.Contains(entry.Transaction!.AccountingRootTransactionId ?? entry.TransactionId) &&
                !waivedTransactions.Contains(entry.Transaction!.AccountingRootTransactionId ?? entry.TransactionId) &&
                !waivedLines.Contains(entry.Id))
            .GroupBy(entry => entry.Transaction!.AccountingRootTransactionId ?? entry.TransactionId)
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
        DailyEntryTotals? effectiveTotals = await ReconciliationLedgerQueries.Amounts(db).Where(entry =>
                entry.ReconciliationDate == date && !waivedLines.Contains(entry.Id) && !waivedTransactions.Contains(entry.RootTransactionId))
            .GroupBy(_ => 1).Select(group => new DailyEntryTotals(
                group.Where(entry => entry.Direction == EntryType.Debit).Sum(entry => entry.Amount),
                group.Where(entry => entry.Direction == EntryType.Credit).Sum(entry => entry.Amount)))
            .FirstOrDefaultAsync(cancellationToken);
        bool ledgerUnbalanced = effectiveTotals is not null && effectiveTotals.Debits != effectiveTotals.Credits;
        long actorId = currentUser.GetCurrentUserId();
        if (persistLedgerException)
            await UpsertLedgerReconciliationExceptionAsync(date, dailyTotals?.Debits ?? 0m, dailyTotals?.Credits ?? 0m,
                ledgerUnbalanced, actorId, cancellationToken);
        int openSessions = await db.CashPositionSessions.CountAsync(item => item.BusinessDate == date && item.Status != OperationsConstants.CashSessionClosed && item.Status != OperationsConstants.CashSessionCancelled, cancellationToken);
        bool hasFinalVaultCount = await db.CashPositionSessions.AnyAsync(item => item.BusinessDate == date &&
            item.PositionType == OperationsConstants.CashPositionVault && item.Status == OperationsConstants.CashSessionClosed &&
            db.CashCounts.Any(count => count.SessionId == item.Id), cancellationToken);
        int missingVaultCount = hasFinalVaultCount ? 0 : 1;
        int pendingHandoffs = await db.CashHandoffs.CountAsync(item => item.BusinessDate == date &&
              item.Status != OperationsConstants.CashHandoffAccepted, cancellationToken);
        int pendingCashProposals = await db.CashProposals.CountAsync(item => item.Session!.BusinessDate == date &&
            (item.Status == "PendingApproval" || item.Status == "PendingReceipt"), cancellationToken);
        pendingCashProposals += await db.CashCorrectionProposals.CountAsync(item => item.Exception!.BusinessDate == date &&
            item.Status == ProposalStatus.PendingApproval, cancellationToken);
        pendingCashProposals += await db.BankExpenseRequests.CountAsync(item => item.BusinessDate == date &&
            item.Status == "PendingApproval", cancellationToken);
        List<long> resolvedInUnitOfWork = db.ReconciliationExceptions.Local
            .Where(item => item.BusinessDate == date && item.Severity == "Critical" &&
                item.Status == OperationsConstants.ExceptionResolved && db.Entry(item).State == EntityState.Modified)
            .Select(item => item.Id).ToList();
        int criticalExceptions = await db.ReconciliationExceptions.AsNoTracking().CountAsync(item => item.BusinessDate == date &&
            item.Severity == "Critical" && item.Status != OperationsConstants.ExceptionResolved &&
            item.Status != OperationsConstants.ExceptionForcePassed && !resolvedInUnitOfWork.Contains(item.Id), cancellationToken);
        criticalExceptions += db.ChangeTracker.Entries<ReconciliationException>().Count(entry =>
            entry.State == EntityState.Added && entry.Entity.BusinessDate == date && entry.Entity.Severity == "Critical" &&
            entry.Entity.Status != OperationsConstants.ExceptionResolved);
        return
        [
            new(OperationsConstants.EodStageTransactions, pending == 0 && failed == 0 ? "Complete" : "Blocked", pending + failed, null),
            new(OperationsConstants.EodStagePendingApprovals, pendingApprovals == 0 ? "Complete" : "Blocked", pendingApprovals, null),
            new(OperationsConstants.EodStageAccountingEntries, missingEntries + unbalanced == 0 ? "Complete" : "Blocked", missingEntries + unbalanced, null),
            new(OperationsConstants.EodStageLedgerReconciliation, !ledgerUnbalanced ? "Complete" : "Blocked", ledgerUnbalanced ? 1 : 0, null),
            new(OperationsConstants.EodStageCash, openSessions + pendingHandoffs + pendingCashProposals + missingVaultCount == 0 ? "Complete" : "Blocked",
                  openSessions + pendingHandoffs + pendingCashProposals + missingVaultCount, openSessions + pendingHandoffs + pendingCashProposals + missingVaultCount == 0 ? null :
                      $"{openSessions} open or scheduled cash session(s); {pendingHandoffs} handoff(s) awaiting acceptance or reassignment; {pendingCashProposals} cash proposal(s) awaiting resolution; {missingVaultCount} final vault count(s) missing."),
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
        if (existing?.Status == OperationsConstants.ExceptionForcePassed) return;
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

    private async Task EnsureDateOpenAsync(DateOnly date, CancellationToken cancellationToken)
    {
        bool isOpen = date <= BusinessTime.Today && await db.BusinessDates.AnyAsync(item => item.Date == date &&
            item.Status == OperationsConstants.BusinessDateOpen, cancellationToken);
        if (!isOpen) throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
    }

    private async Task<List<long>> GetActiveManagerIdsAsync(CancellationToken cancellationToken) =>
        await (from userRole in db.UserRoles
               join user in db.Users on userRole.UserId equals user.Id
               join rolePermission in db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
               join permission in db.Permissions on rolePermission.PermissionId equals permission.Id
               where user.Status == UserStatus.Active && permission.Code == SecurityConstants.EndOfDayApproval
               select user.Id).Distinct().OrderBy(id => id).ToListAsync(cancellationToken);

    private async Task<bool> IsSoleActiveManagerAsync(long userId, CancellationToken cancellationToken)
    {
        List<long> managerIds = await GetActiveManagerIdsAsync(cancellationToken);
        return managerIds.Count == 1 && managerIds[0] == userId;
    }

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
            ReviewedBy = run.ReviewedBy,
            ReviewedAtUtc = run.ReviewedAtUtc,
            OverrideReason = run.OverrideReason,
            OverrideRequiredUserIds = DeserializeUserIds(run.OverrideRequiredUserIdsJson),
            OverrideApprovedUserIds = DeserializeUserIds(run.OverrideApprovedUserIdsJson),
            OverrideApprovalTimesUtc = DeserializeApprovalTimes(run.OverrideApprovalAuditJson)
        };
}

