using System.Text.Json;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

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

    public async Task<EndOfDayRunResponse> RunPreCloseAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var openDate = await businessDates.GetOpenBusinessDateValueAsync(cancellationToken);
        if (openDate != date) throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

        var stages = await BuildPreCloseStagesAsync(date, cancellationToken);
        var accountResults = await accountReconciliation.ReconcileAccountsAsync(
            new AccountReconciliationRequest(date, date, null), cancellationToken);
        var accountExceptions = accountResults.Results.Count(item => item.Status != OperationsConstants.ReconciliationMatched);
        stages.Add(new EndOfDayStageResponse(OperationsConstants.EodStageAccountReconciliation, accountExceptions == 0 ? "Complete" : "Exceptions", accountExceptions, null));
        var blocked = stages.Any(stage => stage.IssueCount > 0 || stage.Status is "Blocked" or "Exceptions");
        var now = DateTime.UtcNow;
        var run = new EndOfDayRun
        {
            BusinessDate = date, Status = blocked ? OperationsConstants.EodBlocked : OperationsConstants.EodReadyForApproval,
            StageSummaryJson = JsonSerializer.Serialize(stages, JsonOptions), PreparedBy = currentUser.GetCurrentUserId(), PreparedAtUtc = now
        };
        db.EndOfDayRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(run, stages);
    }

    public async Task<EndOfDayRunResponse> ApproveAsync(long runId, CancellationToken cancellationToken)
    {
        var run = await db.EndOfDayRuns.SingleOrDefaultAsync(item => item.Id == runId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        var actorId = currentUser.GetCurrentUserId();
        var isBusinessDateOpen = await db.BusinessDates.AsNoTracking().AnyAsync(item =>
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
        var actorId = currentUser.GetCurrentUserId();
        var initialRun = await db.EndOfDayRuns.AsNoTracking().SingleOrDefaultAsync(item => item.Id == runId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (initialRun.Status != OperationsConstants.EodApproved || initialRun.ApprovedBy == initialRun.PreparedBy)
            throw new BusinessRuleException(MessageCode.EndOfDayApprovalRequired);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var run = (await db.EndOfDayRuns.FromSql($"SELECT * FROM EndOfDayRuns WHERE Id = {runId} FOR UPDATE").ToListAsync(cancellationToken)).Single();
        var businessDate = (await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Date = {run.BusinessDate} FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault()
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (run.Status != OperationsConstants.EodApproved || businessDate.Status != OperationsConstants.BusinessDateOpen)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        await EnsureNoClosingBlockersAsync(run.BusinessDate, cancellationToken);

        // Keep the date lock while generating GL summaries so normal posting cannot race the audited close boundary.
        var summariesExist = await db.DailySummaries.AnyAsync(item => item.SummaryDate == run.BusinessDate, cancellationToken);
        var auditExists = await db.ReconciliationBatches.AnyAsync(item => item.ReconciliationDate == run.BusinessDate &&
            item.ReconciliationType == AuditConstants.EndOfDayReconciliationType, cancellationToken);
        if (!summariesExist && !auditExists)
            await endOfDayAudit.RunEndOfDayAuditAsync(run.BusinessDate, cancellationToken);
        else if (summariesExist != auditExists)
            throw new BusinessRuleException(MessageCode.EndOfDayAuditAlreadyProcessed);

        var now = DateTime.UtcNow;
        businessDate.Status = OperationsConstants.BusinessDateClosed;
        businessDate.ClosedBy = actorId;
        businessDate.ClosedAtUtc = now;
        businessDate.Version++;
        var nextDate = new BusinessDate
        {
            Date = run.BusinessDate.AddDays(1), Status = OperationsConstants.BusinessDateOpen,
            OpenedBy = actorId, OpenedAtUtc = now
        };
        db.BusinessDates.Add(nextDate);
        run.Status = OperationsConstants.EodClosed;
        run.ClosedBy = actorId;
        run.ClosedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(run, DeserializeStages(run.StageSummaryJson));
    }

    private async Task<List<EndOfDayStageResponse>> BuildPreCloseStagesAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var (startUtc, endUtc) = BusinessTime.GetUtcRange(date);
        var dayTransactions = db.Transactions.AsNoTracking().Where(item =>
            item.BusinessDate == date || (item.TransactionAt >= startUtc && item.TransactionAt < endUtc) ||
            db.TransactionEntries.Any(entry => entry.TransactionId == item.Id && entry.PostingDate == date));
        var pending = await dayTransactions.CountAsync(item => item.TransactionStatus == TransactionStatus.Pending, cancellationToken);
        var failed = await dayTransactions.CountAsync(item => item.TransactionStatus == TransactionStatus.Failed, cancellationToken);
        var pendingApprovals = await dayTransactions.CountAsync(item => item.TransactionStatus == TransactionStatus.Authorized, cancellationToken);
        var postedIds = dayTransactions.Where(item => item.PostedAt.HasValue).Select(item => item.Id);
        var missingEntries = await postedIds.CountAsync(id => !db.TransactionEntries.Any(entry => entry.TransactionId == id), cancellationToken);
        var groupedEntries = await db.TransactionEntries.AsNoTracking().Where(entry => postedIds.Contains(entry.TransactionId))
            .GroupBy(entry => entry.TransactionId)
            .Select(group => new { Debit = group.Where(entry => entry.EntryType == EntryType.Debit).Sum(entry => entry.Amount), Credit = group.Where(entry => entry.EntryType == EntryType.Credit).Sum(entry => entry.Amount) })
            .ToListAsync(cancellationToken);
        var unbalanced = groupedEntries.Count(item => item.Debit != item.Credit);
        var dailyTotals = await db.TransactionEntries.AsNoTracking().Where(entry => entry.PostingDate == date)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Debits = group.Where(entry => entry.EntryType == EntryType.Debit).Sum(entry => entry.Amount),
                Credits = group.Where(entry => entry.EntryType == EntryType.Credit).Sum(entry => entry.Amount)
            }).FirstOrDefaultAsync(cancellationToken);
        var ledgerUnbalanced = dailyTotals is not null && dailyTotals.Debits != dailyTotals.Credits;
        var actorId = currentUser.GetCurrentUserId();
        await UpsertLedgerReconciliationExceptionAsync(date, dailyTotals?.Debits ?? 0m, dailyTotals?.Credits ?? 0m,
            ledgerUnbalanced, actorId, cancellationToken);
          var openSessions = await db.CashPositionSessions.CountAsync(item => item.BusinessDate == date && item.Status == OperationsConstants.CashSessionOpen, cancellationToken);
          var pendingHandoffs = await db.CashHandoffs.CountAsync(item => item.BusinessDate == date &&
              item.Status != OperationsConstants.CashHandoffAccepted, cancellationToken);
        var resolvedInUnitOfWork = db.ReconciliationExceptions.Local
            .Where(item => item.BusinessDate == date && item.Severity == "Critical" &&
                item.Status == OperationsConstants.ExceptionResolved && db.Entry(item).State == EntityState.Modified)
            .Select(item => item.Id).ToList();
        var criticalExceptions = await db.ReconciliationExceptions.AsNoTracking().CountAsync(item => item.BusinessDate == date &&
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
        var existing = await db.ReconciliationExceptions
            .Where(item => item.Type == OperationsConstants.ExceptionLedgerBalance && item.BusinessDate == date &&
                item.Status != OperationsConstants.ExceptionResolved)
            .OrderByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        var now = DateTime.UtcNow;
        if (isUnbalanced)
        {
            if (existing is null)
            {
                var exception = new ReconciliationException
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
        var stages = await BuildPreCloseStagesAsync(date, cancellationToken);
        var accountResults = await accountReconciliation.ReconcileAccountsAsync(
            new AccountReconciliationRequest(date, date, null), cancellationToken);
        var accountExceptions = accountResults.Results.Count(item => item.Status != OperationsConstants.ReconciliationMatched);
        stages.Add(new EndOfDayStageResponse(OperationsConstants.EodStageAccountReconciliation,
            accountExceptions == 0 ? "Complete" : "Exceptions", accountExceptions, null));
        if (stages.Any(stage => stage.IssueCount > 0))
            throw new BusinessRuleException(MessageCode.ReconciliationBlocked);
        var latest = await db.EndOfDayRuns.Where(item => item.BusinessDate == date && item.Status == OperationsConstants.EodApproved)
            .OrderByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (latest is null) throw new BusinessRuleException(MessageCode.ReconciliationBlocked);
    }

    private static IReadOnlyList<EndOfDayStageResponse> DeserializeStages(string json) =>
        JsonSerializer.Deserialize<List<EndOfDayStageResponse>>(json, JsonOptions) ?? [];

    private static EndOfDayRunResponse ToResponse(EndOfDayRun run, IReadOnlyList<EndOfDayStageResponse> stages) =>
        new(run.Id, run.BusinessDate, run.Status, run.PreparedAtUtc, stages, run.PreparedBy, run.ApprovedBy, run.ClosedBy);
}
