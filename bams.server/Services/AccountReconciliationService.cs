using bams.server.Data;
using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.DTO.Common;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Compares customer operational balances with customer-linked deposit-liability journal balances.</summary>
public sealed class AccountReconciliationService : IAccountReconciliationService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AccountReconciliationService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AccountReconciliationRunResponse> ReconcileAccountsAsync(AccountReconciliationRequest request, CancellationToken cancellationToken)
        => await RunReconciliationAsync(request, _currentUser.GetCurrentUserId(), null, cancellationToken);

    /// <summary>Runs the same account reconciliation calculation with an explicitly authorized scheduler actor.</summary>
    public async Task<AccountReconciliationRunResponse> ReconcileAccountsAsAsync(AccountReconciliationRequest request, long actorId, long? scheduledJobExecutionId, CancellationToken cancellationToken)
        => await RunReconciliationAsync(request, actorId, scheduledJobExecutionId, cancellationToken);

    private async Task<AccountReconciliationRunResponse> RunReconciliationAsync(AccountReconciliationRequest request, long actorId,
        long? scheduledJobExecutionId, CancellationToken cancellationToken)
    {
        if (scheduledJobExecutionId.HasValue)
        {
            var priorRun = await _db.AccountReconciliationRuns.AsNoTracking()
                .Include(item => item.Results).ThenInclude(item => item.Account)
                .SingleOrDefaultAsync(item => item.ScheduledJobExecutionId == scheduledJobExecutionId, cancellationToken);
            if (priorRun is not null)
                return new AccountReconciliationRunResponse(priorRun.Id, priorRun.FromDate, priorRun.ToDate, priorRun.Status,
                    priorRun.PerformedAtUtc, priorRun.Results.Select(item => new AccountReconciliationResultResponse(
                        item.AccountId, item.Account!.AccountNo, item.BusinessDate, item.OperationalBalance,
                        item.LedgerBalance, item.Difference, item.Status)).ToList());
        }
        if (request.FromDate > request.ToDate)
            throw new ValidationException(MessageCode.InvalidDateRange);
        if (request.AccountId is <= 0)
            throw new ValidationException(MessageCode.InvalidRequest);
        if (request.ToDate.DayNumber - request.FromDate.DayNumber + 1 > OperationsConstants.MaximumReconciliationRangeDays)
            throw new ValidationException(MessageCode.ReconciliationDateRangeTooLong);

        var accountsQuery = _db.Accounts.AsNoTracking().AsQueryable();
        if (request.AccountId.HasValue)
            accountsQuery = accountsQuery.Where(account => account.Id == request.AccountId.Value);
        var accounts = await accountsQuery.OrderBy(account => account.Id)
            .Select(account => new { account.Id, account.AccountNo }).ToListAsync(cancellationToken);
        if (request.AccountId.HasValue && accounts.Count == 0)
            throw new NotFoundException(MessageCode.AccountNotFound);

        var dates = EnumerateDates(request.FromDate, request.ToDate);
        var baselineLedger = await (from entry in _db.TransactionEntries.AsNoTracking()
            join account in accountsQuery on entry.CustomerAccountId equals (long?)account.Id
            where entry.PostingDate < request.FromDate && entry.GlAccount!.Code == AccountingConstants.CustomerDepositsGlCode
            group entry by entry.CustomerAccountId!.Value into accountEntries
            select new
            {
                AccountId = accountEntries.Key,
                Credits = accountEntries.Where(entry => entry.EntryType == EntryType.Credit).Sum(entry => entry.Amount),
                Debits = accountEntries.Where(entry => entry.EntryType == EntryType.Debit).Sum(entry => entry.Amount)
            }).ToDictionaryAsync(item => item.AccountId, item => item.Credits - item.Debits, cancellationToken);
        var dailyLedger = await (from entry in _db.TransactionEntries.AsNoTracking()
            join account in accountsQuery on entry.CustomerAccountId equals (long?)account.Id
            where entry.PostingDate >= request.FromDate && entry.PostingDate <= request.ToDate &&
                entry.GlAccount!.Code == AccountingConstants.CustomerDepositsGlCode
            group entry by new { AccountId = entry.CustomerAccountId!.Value, entry.PostingDate } into accountEntries
            select new
            {
                accountEntries.Key.AccountId,
                accountEntries.Key.PostingDate,
                Net = accountEntries.Where(entry => entry.EntryType == EntryType.Credit).Sum(entry => entry.Amount) -
                    accountEntries.Where(entry => entry.EntryType == EntryType.Debit).Sum(entry => entry.Amount)
            }).ToListAsync(cancellationToken);

        // Pick the last snapshot by financial posting date, then use its final posting sequence for that date.
        // Selecting only the greatest identity can choose a later-inserted backdated posting as the baseline.
        var baselineOperationalDates = from entry in _db.AccountTransactions.AsNoTracking()
            join account in accountsQuery on entry.AccountId equals account.Id
            where entry.PostingDate < request.FromDate
            group entry by entry.AccountId into accountTransactions
            select new { AccountId = accountTransactions.Key, PostingDate = accountTransactions.Max(entry => entry.PostingDate) };
        var baselineOperationalIds = from entry in _db.AccountTransactions.AsNoTracking()
            join latest in baselineOperationalDates on new { entry.AccountId, entry.PostingDate } equals new { latest.AccountId, latest.PostingDate }
            group entry by entry.AccountId into accountTransactions
            select accountTransactions.Max(entry => entry.Id);
        var operationalBalances = await _db.AccountTransactions.AsNoTracking()
            .Where(entry => baselineOperationalIds.Contains(entry.Id))
            .ToDictionaryAsync(entry => entry.AccountId, entry => entry.LedgerBalanceAfter, cancellationToken);
        var dailyOperationalIds = from entry in _db.AccountTransactions.AsNoTracking()
            join account in accountsQuery on entry.AccountId equals account.Id
            where entry.PostingDate >= request.FromDate && entry.PostingDate <= request.ToDate
            group entry by new { entry.AccountId, entry.PostingDate } into accountTransactions
            select accountTransactions.Max(entry => entry.Id);
        var dailyOperational = await _db.AccountTransactions.AsNoTracking()
            .Where(entry => dailyOperationalIds.Contains(entry.Id))
            .Select(entry => new { entry.AccountId, entry.PostingDate, entry.LedgerBalanceAfter })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var activeExceptions = await (from item in _db.ReconciliationExceptions
            join account in accountsQuery on item.AccountId equals (long?)account.Id
            where item.Type == "AccountBalance" && item.BusinessDate >= request.FromDate &&
                item.BusinessDate <= request.ToDate && item.Status != "Resolved"
            select item)
            .OrderByDescending(item => item.Id).ToListAsync(cancellationToken);
        var activeByAccountAndDate = activeExceptions.GroupBy(item => (item.AccountId!.Value, item.BusinessDate))
            .ToDictionary(group => group.Key, group => group.First());
        var approvedCorrectionExceptions = await _db.ReconciliationExceptions
            .Include(item => item.CorrectionTransaction)
            .Where(item => item.AccountId.HasValue && item.Status != OperationsConstants.ExceptionResolved &&
                item.CorrectionRequestStatus == "Approved")
            .ToListAsync(cancellationToken);
        var approvedCorrectionsByAccount = approvedCorrectionExceptions.GroupBy(item => item.AccountId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var run = new AccountReconciliationRun
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            AccountId = request.AccountId,
            ScheduledJobExecutionId = scheduledJobExecutionId,
            PerformedBy = actorId,
            PerformedAtUtc = now
        };
        _db.AccountReconciliationRuns.Add(run);
        var responses = new List<AccountReconciliationResultResponse>(checked(accounts.Count * dates.Count));
        var ledgerBalances = accounts.ToDictionary(account => account.Id, account => baselineLedger.GetValueOrDefault(account.Id));
        var dailyLedgerByDate = dailyLedger.GroupBy(item => item.PostingDate)
            .ToDictionary(group => group.Key, group => group.ToDictionary(item => item.AccountId, item => item.Net));
        var operationalByDate = dailyOperational.GroupBy(item => item.PostingDate)
            .ToDictionary(group => group.Key, group => group.ToDictionary(item => item.AccountId, item => item.LedgerBalanceAfter));
        foreach (var date in dates)
        {
            if (dailyLedgerByDate.TryGetValue(date, out var dayLedger))
            {
                foreach (var item in dayLedger) ledgerBalances[item.Key] += item.Value;
            }
            if (operationalByDate.TryGetValue(date, out var dayOperational))
            {
                foreach (var item in dayOperational) operationalBalances[item.Key] = item.Value;
            }
            foreach (var account in accounts)
            {
                var operational = operationalBalances.GetValueOrDefault(account.Id);
                var ledger = ledgerBalances.GetValueOrDefault(account.Id);
                var difference = operational - ledger;
                var status = difference == 0m ? "Matched" : "Unmatched";
                run.Results.Add(new AccountReconciliationResult
                {
                    AccountId = account.Id,
                    BusinessDate = date,
                    OperationalBalance = operational,
                    LedgerBalance = ledger,
                    Difference = difference,
                    Status = status
                });
                UpsertAccountException(account.Id, date, operational, ledger, difference, actorId, now, activeByAccountAndDate);
                // Closed periods stay unchanged: a current-date match after the linked reversal is the resolution evidence.
                if (difference == 0m && approvedCorrectionsByAccount.TryGetValue(account.Id, out var correctedExceptions))
                    foreach (var correctedException in correctedExceptions.Where(item => item.Status != OperationsConstants.ExceptionResolved &&
                                 item.CorrectionTransaction?.BusinessDate is { } reversalDate && reversalDate <= date))
                        AddHistoryAndSetStatus(correctedException, OperationsConstants.ExceptionResolved,
                            "A reconciliation run on or after the posted reversal date confirmed that account and ledger balances match.", actorId, now);
                responses.Add(new AccountReconciliationResultResponse(account.Id, account.AccountNo, date, operational, ledger, difference, status));
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new AccountReconciliationRunResponse(run.Id, request.FromDate, request.ToDate, run.Status, now, responses);
    }

    private static List<DateOnly> EnumerateDates(DateOnly fromDate, DateOnly toDate)
    {
        var dates = new List<DateOnly>();
        for (var date = fromDate; ; date = date.AddDays(1))
        {
            dates.Add(date);
            if (date == toDate) return dates;
        }
    }

    private static bool IsAllowedStatusTransition(string current, string requested) => (current, requested) switch
    {
        (OperationsConstants.ExceptionOpen, OperationsConstants.ExceptionUnderInvestigation or OperationsConstants.ExceptionAdjustmentRequired) => true,
        (OperationsConstants.ExceptionUnderInvestigation, OperationsConstants.ExceptionOpen or OperationsConstants.ExceptionAdjustmentRequired) => true,
        (OperationsConstants.ExceptionAdjustmentRequired, OperationsConstants.ExceptionUnderInvestigation) => true,
        _ => false
    };

    public async Task<PagedResponse<ReconciliationExceptionResponse>> GetExceptionsAsync(DateOnly? fromDate, DateOnly? toDate, string? status, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            throw new ValidationException(MessageCode.InvalidDateRange);
        if (fromDate.HasValue && toDate.HasValue &&
            toDate.Value.DayNumber - fromDate.Value.DayNumber + 1 > OperationsConstants.MaximumReconciliationRangeDays)
            throw new ValidationException(MessageCode.ReconciliationDateRangeTooLong);
        if (!string.IsNullOrWhiteSpace(status) && status is not (OperationsConstants.ExceptionOpen or
            OperationsConstants.ExceptionUnderInvestigation or OperationsConstants.ExceptionAdjustmentRequired or OperationsConstants.ExceptionResolved))
            throw new ValidationException(MessageCode.InvalidReconciliationExceptionStatus);
        var (resolvedPage, resolvedSize) = TransactionRequestValidator.ResolvePaging(page, pageSize ?? 20);
        var query = _db.ReconciliationExceptions.AsNoTracking().AsQueryable();
        if (fromDate.HasValue) query = query.Where(item => item.BusinessDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(item => item.BusinessDate <= toDate.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.BusinessDate).ThenByDescending(item => item.Id)
            .Skip((resolvedPage - 1) * resolvedSize).Take(resolvedSize)
            .Select(item => new ReconciliationExceptionResponse(item.Id, item.Type, item.Source, item.BusinessDate,
                item.AccountId, item.PositionSessionId, item.ExpectedAmount, item.ActualAmount, item.Difference, item.Severity,
                item.Status, item.AssignedTo, item.RelatedTransactionId, item.CorrectionTransactionId,
                item.Notes, item.CreatedAtUtc, item.UpdatedAtUtc, item.CorrectionRequestStatus,
                item.RequestedCorrectionTransactionId, item.CorrectionRequestedBy, item.CorrectionRequestedAtUtc,
                item.CorrectionReviewedBy, item.CorrectionReviewedAtUtc, item.CorrectionRequestReason,
                item.CorrectionExternalRecoveryReference, item.CorrectionReviewNote)).ToListAsync(cancellationToken);
        return new PagedResponse<ReconciliationExceptionResponse>(items, resolvedPage, resolvedSize, count,
            (int)Math.Ceiling(count / (double)resolvedSize));
    }

    /// <summary>Returns one discrepancy and its append-only investigation timeline.</summary>
    public async Task<ReconciliationExceptionDetailResponse> GetExceptionByIdAsync(long exceptionId, CancellationToken cancellationToken)
    {
        var exception = await _db.ReconciliationExceptions.AsNoTracking()
            .Where(item => item.Id == exceptionId)
            .Select(item => new ReconciliationExceptionResponse(item.Id, item.Type, item.Source, item.BusinessDate,
                item.AccountId, item.PositionSessionId, item.ExpectedAmount, item.ActualAmount, item.Difference,
                item.Severity, item.Status, item.AssignedTo, item.RelatedTransactionId, item.CorrectionTransactionId,
                item.Notes, item.CreatedAtUtc, item.UpdatedAtUtc, item.CorrectionRequestStatus,
                item.RequestedCorrectionTransactionId, item.CorrectionRequestedBy, item.CorrectionRequestedAtUtc,
                item.CorrectionReviewedBy, item.CorrectionReviewedAtUtc, item.CorrectionRequestReason,
                item.CorrectionExternalRecoveryReference, item.CorrectionReviewNote))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        var timeline = await _db.ReconciliationExceptionHistories.AsNoTracking()
            .Where(item => item.ExceptionId == exceptionId).OrderBy(item => item.CreatedAtUtc)
            .Select(item => new ReconciliationExceptionHistoryResponse(item.Id, item.OldStatus, item.NewStatus,
                item.Note, item.ActorId, item.CreatedAtUtc)).ToListAsync(cancellationToken);
        return new ReconciliationExceptionDetailResponse(exception, timeline);
    }

    public async Task<ReconciliationExceptionResponse> UpdateExceptionAsync(long exceptionId, UpdateReconciliationExceptionRequest request, CancellationToken cancellationToken)
    {
        var exception = await _db.ReconciliationExceptions.SingleOrDefaultAsync(item => item.Id == exceptionId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        var allowed = new[] { OperationsConstants.ExceptionOpen, OperationsConstants.ExceptionUnderInvestigation,
            OperationsConstants.ExceptionAdjustmentRequired, OperationsConstants.ExceptionResolved };
        if (!allowed.Contains(request.Status, StringComparer.Ordinal))
            throw new ValidationException(MessageCode.InvalidReconciliationExceptionStatus);
        if (request.Status == "Resolved")
            throw new BusinessRuleException(MessageCode.ReconciliationExceptionNotResolvable);
        if (request.Notes?.Trim().Length > 2000)
            throw new ValidationException(MessageCode.FieldTooLong);
        if ((request.AssignedTo.HasValue && request.AssignedTo.Value <= 0) ||
            (request.CorrectionTransactionId.HasValue && request.CorrectionTransactionId.Value <= 0))
            throw new ValidationException(MessageCode.InvalidRequest);
        if (request.Status != exception.Status && !IsAllowedStatusTransition(exception.Status, request.Status))
            throw new ValidationException(MessageCode.ReconciliationStatusTransitionInvalid);
        if (request.AssignedTo.HasValue && !await _db.Users.AnyAsync(item => item.Id == request.AssignedTo && item.Status != bams.server.Models.Security.UserStatus.Deleted, cancellationToken))
            throw new NotFoundException(MessageCode.UserNotFound);
        if (request.CorrectionTransactionId.HasValue && !await _db.Transactions.AnyAsync(item =>
                item.Id == request.CorrectionTransactionId && item.PostedAt.HasValue &&
                (item.TransactionStatus == TransactionStatus.Posted || item.TransactionStatus == TransactionStatus.Completed ||
                 item.TransactionStatus == TransactionStatus.Accrued || item.TransactionStatus == TransactionStatus.Reversed),
                cancellationToken))
            throw new NotFoundException(MessageCode.TransactionNotFound);

        var actorId = _currentUser.GetCurrentUserId();
        var now = DateTime.UtcNow;
        _db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
        {
            Exception = exception,
            OldStatus = exception.Status,
            NewStatus = request.Status,
            Note = request.Notes,
            ActorId = actorId,
            CreatedAtUtc = now
        });
        exception.Status = request.Status;
        exception.Notes = request.Notes ?? exception.Notes;
        exception.AssignedTo = request.AssignedTo ?? exception.AssignedTo;
        exception.CorrectionTransactionId = request.CorrectionTransactionId ?? exception.CorrectionTransactionId;
        exception.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return new ReconciliationExceptionResponse(exception.Id, exception.Type, exception.Source, exception.BusinessDate,
            exception.AccountId, exception.PositionSessionId, exception.ExpectedAmount, exception.ActualAmount, exception.Difference,
            exception.Severity, exception.Status, exception.AssignedTo, exception.RelatedTransactionId,
            exception.CorrectionTransactionId, exception.Notes, exception.CreatedAtUtc, exception.UpdatedAtUtc);
    }

    /// <summary>Requests a full reversal of a posted account transaction from its reconciliation exception.</summary>
    public async Task<ReconciliationExceptionResponse> RequestTransactionCorrectionAsync(long exceptionId, RequestTransactionCorrectionRequest request, CancellationToken cancellationToken)
    {
        if (request.TransactionId <= 0 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 2000)
            throw new ValidationException(MessageCode.InvalidRequest);
        if (request.ExternalRecoveryReference?.Trim().Length > 200)
            throw new ValidationException(MessageCode.FieldTooLong);
        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var exception = (await _db.ReconciliationExceptions.FromSql($"SELECT * FROM ReconciliationExceptions WHERE Id = {exceptionId} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault() ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (exception.Status == OperationsConstants.ExceptionResolved || exception.CorrectionRequestStatus == "Pending")
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        if (exception.AccountId is null || !await _db.AccountTransactions.AnyAsync(item => item.TransactionId == request.TransactionId && item.AccountId == exception.AccountId, cancellationToken))
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        var transaction = (await _db.Transactions.FromSql($"SELECT * FROM Transactions WHERE Id = {request.TransactionId} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault(item => item.PostedAt != null &&
            (item.TransactionStatus == TransactionStatus.Posted || item.TransactionStatus == TransactionStatus.Completed || item.TransactionStatus == TransactionStatus.Accrued))
            ?? throw new NotFoundException(MessageCode.TransactionNotFound);
        if (await _db.Transactions.AnyAsync(item => item.ReversalOfTransactionId == transaction.Id, cancellationToken))
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        if (await _db.ReconciliationExceptions.AnyAsync(item => item.Id != exception.Id &&
                item.RequestedCorrectionTransactionId == transaction.Id && item.CorrectionRequestStatus == "Pending", cancellationToken))
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        if (transaction.TransactionType is TransactionType.InterbankTransfer or TransactionType.NrcTransfer && transaction.TransactionStatus == TransactionStatus.Completed &&
            string.IsNullOrWhiteSpace(request.ExternalRecoveryReference))
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        exception.RequestedCorrectionTransactionId = transaction.Id;
        exception.CorrectionRequestStatus = "Pending";
        var oldExceptionStatus = exception.Status;
        exception.CorrectionRequestedBy = _currentUser.GetCurrentUserId();
        exception.CorrectionRequestedAtUtc = DateTime.UtcNow;
        exception.CorrectionRequestReason = request.Reason.Trim();
        exception.CorrectionExternalRecoveryReference = string.IsNullOrWhiteSpace(request.ExternalRecoveryReference) ? null : request.ExternalRecoveryReference.Trim();
        exception.CorrectionReviewedBy = null;
        exception.CorrectionReviewedAtUtc = null;
        exception.CorrectionReviewNote = null;
        exception.Status = OperationsConstants.ExceptionAdjustmentRequired;
        exception.UpdatedAtUtc = DateTime.UtcNow;
        _db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
        {
            Exception = exception, OldStatus = oldExceptionStatus,
            NewStatus = exception.Status, Note = $"Correction reversal requested for transaction {transaction.TransactionNo}: {exception.CorrectionRequestReason}",
            ActorId = exception.CorrectionRequestedBy.Value, CreatedAtUtc = exception.CorrectionRequestedAtUtc.Value
        });
        await _db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return await GetExceptionResponseAsync(exceptionId, cancellationToken);
    }

    /// <summary>Approves and posts a balanced full reversal, or rejects a correction request with a reason.</summary>
    public async Task<ReconciliationExceptionResponse> ReviewTransactionCorrectionAsync(long exceptionId, ReviewTransactionCorrectionRequest request, CancellationToken cancellationToken)
    {
        var reviewerId = _currentUser.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Trim().Length > 2000)
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var exception = (await _db.ReconciliationExceptions.FromSql($"SELECT * FROM ReconciliationExceptions WHERE Id = {exceptionId} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault() ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (exception.CorrectionRequestStatus != "Pending")
            throw new BusinessRuleException(MessageCode.CashAdjustmentApprovalRequired);
        if (reviewerId == exception.CorrectionRequestedBy)
            throw new BusinessRuleException(MessageCode.CashAdjustmentApprovalRequired);
        var now = DateTime.UtcNow;
        if (!request.Approve)
        {
            exception.CorrectionRequestStatus = "Rejected";
            exception.CorrectionReviewNote = request.Note.Trim();
            exception.CorrectionReviewedBy = reviewerId;
            exception.CorrectionReviewedAtUtc = now;
            exception.UpdatedAtUtc = now;
            _db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
            {
                Exception = exception, OldStatus = exception.Status, NewStatus = exception.Status,
                Note = $"Correction request rejected: {exception.CorrectionReviewNote}", ActorId = reviewerId, CreatedAtUtc = now
            });
            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
            return await GetExceptionResponseAsync(exceptionId, cancellationToken);
        }
        if (exception.CorrectionExternalRecoveryReference is not null && string.IsNullOrWhiteSpace(request.RecoveryAttestation))
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        if (request.RecoveryAttestation?.Trim().Length > 1000)
            throw new ValidationException(MessageCode.FieldTooLong);

        var originalId = exception.RequestedCorrectionTransactionId!.Value;
        var original = (await _db.Transactions.FromSql($"SELECT * FROM Transactions WHERE Id = {originalId} FOR UPDATE")
            .ToListAsync(cancellationToken)).Single();
        if (original.TransactionStatus is not (TransactionStatus.Posted or TransactionStatus.Completed or TransactionStatus.Accrued))
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        if (await _db.Transactions.AnyAsync(item => item.ReversalOfTransactionId == original.Id, cancellationToken))
            throw new BusinessRuleException(MessageCode.InvalidRequest);
        var accountEntries = await _db.AccountTransactions.Where(item => item.TransactionId == originalId).ToListAsync(cancellationToken);
        var glEntries = await _db.TransactionEntries.Where(item => item.TransactionId == originalId).ToListAsync(cancellationToken);
        if (accountEntries.Count == 0 || glEntries.Count == 0)
            throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete);
        var openBusinessDate = await _db.BusinessDates.Where(item => item.Status == "Open").OrderByDescending(item => item.Date)
            .Select(item => (DateOnly?)item.Date).FirstOrDefaultAsync(cancellationToken);
        if (openBusinessDate is null)
            throw new BusinessRuleException(MessageCode.BusinessDateClosed);
        var businessDate = openBusinessDate.Value;
        var reversal = LedgerPostingService.CreateTransaction(original.TransactionType, TransactionStatus.Completed, original.Amount,
            $"Reviewed reversal of transaction {original.TransactionNo}", exception.CorrectionExternalRecoveryReference,
            null, reviewerId, now);
        reversal.BusinessDate = businessDate;
        reversal.AuthorizedBy = reviewerId;
        reversal.AuthorizedAt = now;
        reversal.ReversalOfTransactionId = original.Id;
        _db.Transactions.Add(reversal);
        foreach (var item in accountEntries)
        {
            var account = await _db.Accounts.SingleAsync(account => account.Id == item.AccountId, cancellationToken);
            var debit = item.EntryType == EntryType.Credit;
            if (debit && (account.LedgerBalance < item.Amount || account.AvailableBalance < item.Amount))
                throw new BusinessRuleException(MessageCode.InsufficientBalance);
            var beforeLedger = account.LedgerBalance;
            var beforeAvailable = account.AvailableBalance;
            account.LedgerBalance += debit ? -item.Amount : item.Amount;
            account.AvailableBalance += debit ? -item.Amount : item.Amount;
            account.UpdatedAt = now;
            _db.AccountTransactions.Add(new AccountTransaction
            {
                Transaction = reversal, AccountId = account.Id, EntryType = debit ? EntryType.Debit : EntryType.Credit,
                Amount = item.Amount, LedgerBalanceBefore = beforeLedger, LedgerBalanceAfter = account.LedgerBalance,
                AvailableBalanceBefore = beforeAvailable, AvailableBalanceAfter = account.AvailableBalance,
                ValueDate = businessDate, PostingDate = businessDate, Description = reversal.Description,
                ReferenceNo = original.TransactionNo, Status = TransactionConstants.CompletedStatus, CreatedAt = now
            });
        }
        foreach (var item in glEntries)
            _db.TransactionEntries.Add(new TransactionEntry
            {
                Transaction = reversal, GlAccountId = item.GlAccountId, CustomerAccountId = item.CustomerAccountId,
                EntryType = item.EntryType == EntryType.Debit ? EntryType.Credit : EntryType.Debit,
                Amount = item.Amount, PostingDate = businessDate, Description = reversal.Description, CreatedAt = now
            });
        original.TransactionStatus = TransactionStatus.Reversed;
        original.UpdatedAt = now;
        exception.CorrectionRequestStatus = "Approved";
        exception.CorrectionTransaction = reversal;
        exception.CorrectionReviewedBy = reviewerId;
        exception.CorrectionReviewedAtUtc = now;
        exception.CorrectionReviewNote = request.Note.Trim() + (string.IsNullOrWhiteSpace(request.RecoveryAttestation) ? string.Empty : $"\nRecovery attested: {request.RecoveryAttestation.Trim()}");
        exception.UpdatedAtUtc = now;
        _db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
        {
            Exception = exception, OldStatus = exception.Status, NewStatus = exception.Status,
            Note = $"Correction approved; reversal transaction {reversal.TransactionNo} posted. {exception.CorrectionReviewNote}",
            ActorId = reviewerId, CreatedAtUtc = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return await GetExceptionResponseAsync(exceptionId, cancellationToken);
    }

    private async Task<ReconciliationExceptionResponse> GetExceptionResponseAsync(long id, CancellationToken cancellationToken) =>
        await _db.ReconciliationExceptions.AsNoTracking().Where(item => item.Id == id).Select(item => new ReconciliationExceptionResponse(
            item.Id, item.Type, item.Source, item.BusinessDate, item.AccountId, item.PositionSessionId, item.ExpectedAmount,
            item.ActualAmount, item.Difference, item.Severity, item.Status, item.AssignedTo, item.RelatedTransactionId,
            item.CorrectionTransactionId, item.Notes, item.CreatedAtUtc, item.UpdatedAtUtc, item.CorrectionRequestStatus,
            item.RequestedCorrectionTransactionId, item.CorrectionRequestedBy, item.CorrectionRequestedAtUtc,
            item.CorrectionReviewedBy, item.CorrectionReviewedAtUtc, item.CorrectionRequestReason,
            item.CorrectionExternalRecoveryReference, item.CorrectionReviewNote)).SingleAsync(cancellationToken);

    /// <summary>Lists posted transactions that affected the selected exception's customer account with journal summaries.</summary>
    public async Task<IReadOnlyList<CorrectionTransactionCandidateResponse>> GetCorrectionCandidatesAsync(long exceptionId, CancellationToken cancellationToken)
    {
        var exception = await _db.ReconciliationExceptions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == exceptionId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (exception.AccountId is null) return [];
        var candidates = await (from accountEntry in _db.AccountTransactions.AsNoTracking()
            join transaction in _db.Transactions.AsNoTracking() on accountEntry.TransactionId equals transaction.Id
            where accountEntry.AccountId == exception.AccountId && transaction.PostedAt.HasValue &&
                (transaction.TransactionStatus == TransactionStatus.Posted || transaction.TransactionStatus == TransactionStatus.Completed ||
                 transaction.TransactionStatus == TransactionStatus.Accrued) &&
                !_db.Transactions.Any(reversal => reversal.ReversalOfTransactionId == transaction.Id)
            orderby transaction.PostedAt descending
            select new { transaction.Id, transaction.TransactionNo, transaction.TransactionType, transaction.TransactionStatus,
                transaction.Amount, transaction.PostedAt, transaction.Description }).Distinct().Take(100).ToListAsync(cancellationToken);
        var ids = candidates.Select(item => item.Id).ToList();
        var journalLines = await (from entry in _db.TransactionEntries.AsNoTracking()
            join gl in _db.GlAccounts.AsNoTracking() on entry.GlAccountId equals gl.Id
            where ids.Contains(entry.TransactionId)
            select new { entry.TransactionId, gl.Code, entry.EntryType, entry.Amount })
            .ToListAsync(cancellationToken);
        var journalById = journalLines.GroupBy(item => item.TransactionId)
            .ToDictionary(group => group.Key, group => string.Join(" | ", group.Select(line => $"{line.Code} {line.EntryType} {line.Amount:N2}")));
        return candidates.Select(item => new CorrectionTransactionCandidateResponse(item.Id, item.TransactionNo,
            item.TransactionType.ToString(), item.TransactionStatus.ToString(), item.Amount, item.PostedAt,
            item.Description, journalById.GetValueOrDefault(item.Id, "No journal lines found."))).ToList();
    }

    /// <summary>Searches customer accounts by account number for the reconciliation scope picker.</summary>
    public async Task<IReadOnlyList<ReconciliationAccountOptionResponse>> SearchAccountsAsync(string? search, CancellationToken cancellationToken)
    {
        var query = _db.Accounts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.AccountNo.Contains(term));
        }
        return await query.OrderBy(item => item.AccountNo).Take(50)
            .Select(item => new ReconciliationAccountOptionResponse(item.Id, item.AccountNo,
                item.AccountType!.Code, item.Status.ToString())).ToListAsync(cancellationToken);
    }

    /// <summary>Returns non-deleted staff who can be assigned to an investigation.</summary>
    public async Task<IReadOnlyList<ReconciliationStaffOptionResponse>> GetInvestigatorOptionsAsync(CancellationToken cancellationToken)
    {
        var users = _db.Users.AsNoTracking().Where(user => user.Status != bams.server.Models.Security.UserStatus.Deleted);
        return await (from user in users
            orderby user.FullName, user.Username
            select new ReconciliationStaffOptionResponse(user.Id, user.FullName, user.Username,
                (from userRole in _db.UserRoles.AsNoTracking()
                 join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                 where userRole.UserId == user.Id
                 orderby role.Name
                 select role.Name).FirstOrDefault() ?? "staff"))
            .ToListAsync(cancellationToken);
    }

    private void UpsertAccountException(long accountId, DateOnly date, decimal expected, decimal actual,
        decimal difference, long actorId, DateTime now,
        IReadOnlyDictionary<(long AccountId, DateOnly BusinessDate), ReconciliationException> activeByAccountAndDate)
    {
        activeByAccountAndDate.TryGetValue((accountId, date), out var existing);
        if (difference == 0m)
        {
            if (existing is not null)
                AddHistoryAndSetStatus(existing, "Resolved", "Account and ledger balances now match.", actorId, now);
            return;
        }

        if (existing is null)
        {
            var exception = new ReconciliationException
            {
                Type = "AccountBalance", Source = "AccountReconciliation", BusinessDate = date, AccountId = accountId,
                ExpectedAmount = expected, ActualAmount = actual, Difference = difference, Severity = "Critical",
                Status = "Open", CreatedBy = actorId, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            _db.ReconciliationExceptions.Add(exception);
            _db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
            {
                Exception = exception,
                OldStatus = "None",
                NewStatus = exception.Status,
                Note = "Account and ledger balances did not match during reconciliation.",
                ActorId = actorId,
                CreatedAtUtc = now
            });
            return;
        }

        _db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
        {
            Exception = existing,
            OldStatus = existing.Status,
            NewStatus = existing.Status,
            Note = $"Reconciliation rerun recorded. Previous expected {existing.ExpectedAmount:N2}, actual {existing.ActualAmount:N2}, difference {existing.Difference:N2}; current expected {expected:N2}, actual {actual:N2}, difference {difference:N2}.",
            ActorId = actorId,
            CreatedAtUtc = now
        });
        existing.ExpectedAmount = expected;
        existing.ActualAmount = actual;
        existing.Difference = difference;
        existing.UpdatedAtUtc = now;
    }

    private void AddHistoryAndSetStatus(ReconciliationException exception, string status, string note, long actorId, DateTime now)
    {
        _db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
        {
            Exception = exception, OldStatus = exception.Status, NewStatus = status, Note = note, ActorId = actorId, CreatedAtUtc = now
        });
        exception.Status = status;
        exception.UpdatedAtUtc = now;
    }
}
