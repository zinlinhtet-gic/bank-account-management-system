using bams.server.Utils;
using System.Security.Cryptography;
using System.Text.Json;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Transactions;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Audit;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>
/// Identifies the posting purpose so account-type permissions can be enforced consistently.
/// </summary>
public enum DebitPurpose
{
    Deposit = 0,
    Withdrawal = 1,
    Transfer = 2
}

/// <summary>
/// Shared building blocks for the services that move money: database transactions with idempotency, account row
/// locks, account-type debit rules, customer and general-ledger entries, refunds and audit logging.
/// Every balance change goes through <see cref="PostCustomerEntryAsync"/>.
/// </summary>
public sealed class LedgerPostingService
{
    // Customer-initiated debits count against an account type's daily and monthly limits; fees and refunds do not.
    private static readonly TransactionType[] LimitedTransactionTypes =
    [
        TransactionType.CashWithdrawal,
        TransactionType.InternalTransfer,
        TransactionType.InterbankTransfer,
        TransactionType.NrcTransfer
    ];

    // Debits of cancelled or failed transactions were refunded, so they no longer use up the limits.
    private static readonly TransactionStatus[] RefundedStatuses =
    [
        TransactionStatus.Cancelled,
        TransactionStatus.Failed
    ];

    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;
    private readonly IBusinessDateService _businessDates;

    // General-ledger ids do not change at runtime, so each is looked up at most once per request.
    private readonly Dictionary<string, long> _glAccountIdsByCode = new();

    public LedgerPostingService(ApplicationDbContext dbContext, IBusinessDateService businessDates)
    {
        _dbContext = dbContext;
        _businessDates = businessDates;
    }

    /// <summary>
    /// Runs a posting inside a database transaction. When the client sent an idempotency key that was already used
    /// for the same kind of transaction, the original result is returned and nothing is posted again.
    /// </summary>
    /// <param name="customerAccountIds">
    /// The customer accounts the request posts to (the accounts that receive an account entry). A replay must name
    /// the same accounts, so a key reused for another account is rejected instead of silently returning the first.
    /// </param>
    /// <param name="postAsync">Posts and saves the transaction; receives the normalized idempotency key to store.</param>
    public async Task<TransactionResponse> RunIdempotentPostingAsync(
        string? idempotencyKey,
        TransactionType transactionType,
        decimal amount,
        long userId,
        IReadOnlyCollection<long> customerAccountIds,
        Func<string?, Task<TransactionResponse>> postAsync,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        var replay = await FindIdempotentReplayAsync(
            normalizedKey,
            transactionType,
            amount,
            userId,
            customerAccountIds,
            cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        try
        {
            return await RunInTransactionAsync(async () =>
            {
                var response = await postAsync(normalizedKey);
                await ValidateTransactionAccountingEntriesAsync(response.Id, cancellationToken);
                return response;
            }, cancellationToken);
        }
        catch (DbUpdateException) when (normalizedKey is not null)
        {
            // A parallel request with the same key committed first and the unique index rejected this one.
            // The rollback already happened; forget the rejected changes and return the committed result.
            _dbContext.ChangeTracker.Clear();
            var concurrentReplay = await FindIdempotentReplayAsync(
                normalizedKey,
                transactionType,
                amount,
                userId,
                customerAccountIds,
                cancellationToken);
            if (concurrentReplay is null)
            {
                throw;
            }

            return concurrentReplay;
        }
    }

    /// <summary>Ensures a posted transaction has a complete, positive, balanced double-entry journal before commit.</summary>
    public async Task ValidateTransactionAccountingEntriesAsync(long transactionId, CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.Transactions.AsNoTracking()
            .Where(item => item.Id == transactionId)
            .Select(item => new { item.TransactionType, item.TransactionStatus, item.Amount, item.ReversalOfTransactionId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(MessageCode.TransactionNotFound);
        var totals = await _dbContext.TransactionEntries
            .AsNoTracking()
            .Where(entry => entry.TransactionId == transactionId)
            .GroupBy(entry => entry.EntryType)
            .Select(group => new
            {
                EntryType = group.Key,
                Count = group.Count(),
                Total = group.Sum(entry => entry.Amount),
                Minimum = group.Min(entry => entry.Amount),
                Maximum = group.Max(entry => entry.Amount)
            })
            .ToListAsync(cancellationToken);
        var lineCount = totals.Sum(item => item.Count);
        var expectedLineCount = GetExpectedJournalLineCount(transaction.TransactionType, transaction.TransactionStatus);

        var debit = totals.SingleOrDefault(item => item.EntryType == EntryType.Debit);
        var credit = totals.SingleOrDefault(item => item.EntryType == EntryType.Credit);
        if (totals.Count != 2 || debit is null || credit is null || debit.Count == 0 || credit.Count == 0 ||
            debit.Minimum != transaction.Amount || debit.Maximum != transaction.Amount ||
            credit.Minimum != transaction.Amount || credit.Maximum != transaction.Amount || lineCount != expectedLineCount ||
            debit.Total != transaction.Amount * debit.Count || credit.Total != transaction.Amount * credit.Count)
        {
            throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete);
        }

        var expectedLines = await GetExpectedJournalLinesAsync(transactionId, transaction.TransactionType,
            transaction.TransactionStatus, transaction.ReversalOfTransactionId, cancellationToken);
        var expectedShape = expectedLines.GroupBy(line => (line.GlCode, line.EntryType))
            .ToDictionary(group => group.Key, group => group.Count());
        var actualLines = await (from entry in _dbContext.TransactionEntries.AsNoTracking()
            join glAccount in _dbContext.GlAccounts.AsNoTracking() on entry.GlAccountId equals glAccount.Id
            where entry.TransactionId == transactionId
            select new { GlCode = glAccount.Code, entry.EntryType, entry.CustomerAccountId })
            .ToListAsync(cancellationToken);
        var actualShape = actualLines.GroupBy(line => (line.GlCode, line.EntryType))
            .ToDictionary(group => group.Key, group => group.Count());
        if (actualShape.Count != expectedShape.Count || expectedShape.Any(pair =>
                !actualShape.TryGetValue(pair.Key, out var actualCount) || actualCount != pair.Value))
            throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete);

        if (transaction.TransactionType is not (TransactionType.InterestAccrual or TransactionType.MaintenanceAccrual or TransactionType.DormantPenaltyAccrual))
        {
            var customerLedgerAccounts = actualLines.Where(line => line.GlCode == AccountingConstants.CustomerDepositsGlCode)
                .GroupBy(line => (line.CustomerAccountId, line.EntryType))
                .ToDictionary(group => group.Key, group => group.Count());
            var operationalAccounts = await _dbContext.AccountTransactions.AsNoTracking()
                .Where(entry => entry.TransactionId == transactionId)
                .GroupBy(entry => new { entry.AccountId, entry.EntryType })
                .Select(group => new { AccountId = (long?)group.Key.AccountId, group.Key.EntryType, Count = group.Count() })
                .ToDictionaryAsync(item => (item.AccountId, item.EntryType), item => item.Count, cancellationToken);
            if (customerLedgerAccounts.Count != operationalAccounts.Count || customerLedgerAccounts.Any(pair =>
                    !operationalAccounts.TryGetValue(pair.Key, out var operationalCount) || operationalCount != pair.Value))
                throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete);
        }

        if (transaction.TransactionType == TransactionType.InternalTransfer)
        {
            var customerLines = await _dbContext.TransactionEntries.AsNoTracking()
                .Where(entry => entry.TransactionId == transactionId && entry.CustomerAccountId.HasValue)
                .Select(entry => new { entry.CustomerAccountId, entry.EntryType }).ToListAsync(cancellationToken);
            if (customerLines.Count != 2 || customerLines.Select(entry => entry.CustomerAccountId).Distinct().Count() != 2 ||
                customerLines.Count(entry => entry.EntryType == EntryType.Debit) != 1 ||
                customerLines.Count(entry => entry.EntryType == EntryType.Credit) != 1)
                throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete);
        }

        if (debit.Total != credit.Total)
        {
            throw new BusinessRuleException(MessageCode.TransactionEntriesUnbalanced);
        }
    }

    // Internal transfers use two customer-liability lines; completed interbank/NRC transfers add two settlement lines
    // to the two lines recorded when the transfer was initiated.
    private static int GetExpectedJournalLineCount(TransactionType transactionType, TransactionStatus transactionStatus) =>
        transactionStatus == TransactionStatus.Completed &&
        transactionType is (TransactionType.InterbankTransfer or TransactionType.NrcTransfer)
            ? 4
            : 2;

    private async Task<IReadOnlyList<(string GlCode, EntryType EntryType)>> GetExpectedJournalLinesAsync(
        long transactionId, TransactionType transactionType, TransactionStatus status, long? reversalOfTransactionId,
        CancellationToken cancellationToken)
    {
        var lines = new List<(string GlCode, EntryType EntryType)>();
        void Add(string code, EntryType entryType) => lines.Add((code, entryType));
        switch (transactionType)
        {
            case TransactionType.CashDeposit:
                Add(AccountingConstants.CashOnHandGlCode, EntryType.Debit);
                Add(AccountingConstants.CustomerDepositsGlCode, EntryType.Credit);
                break;
            case TransactionType.CashWithdrawal:
                Add(AccountingConstants.CustomerDepositsGlCode, EntryType.Debit);
                Add(AccountingConstants.CashOnHandGlCode, EntryType.Credit);
                break;
            case TransactionType.InternalTransfer:
                Add(AccountingConstants.CustomerDepositsGlCode, EntryType.Debit);
                Add(AccountingConstants.CustomerDepositsGlCode, EntryType.Credit);
                break;
            case TransactionType.InterbankTransfer:
                Add(AccountingConstants.CustomerDepositsGlCode, EntryType.Debit);
                Add(AccountingConstants.InterbankClearingGlCode, EntryType.Credit);
                if (status == TransactionStatus.Completed)
                {
                    Add(AccountingConstants.InterbankClearingGlCode, EntryType.Debit);
                    Add(AccountingConstants.DueFromOtherBanksGlCode, EntryType.Credit);
                }
                break;
            case TransactionType.NrcTransfer:
            {
                var accountFunded = await _dbContext.AccountTransactions.AsNoTracking()
                    .AnyAsync(entry => entry.TransactionId == transactionId && entry.EntryType == EntryType.Debit, cancellationToken);
                Add(accountFunded ? AccountingConstants.CustomerDepositsGlCode : AccountingConstants.CashOnHandGlCode, EntryType.Debit);
                Add(AccountingConstants.NrcTransfersPayableGlCode, EntryType.Credit);
                if (status == TransactionStatus.Completed)
                {
                    var deliveryType = await _dbContext.NrcCashTransferDetails.AsNoTracking()
                        .Where(detail => detail.TransactionId == transactionId)
                        .Select(detail => detail.DeliveryType).SingleOrDefaultAsync(cancellationToken);
                    if (deliveryType is not (TransactionConstants.NrcDeliveryAtBranch or TransactionConstants.NrcDeliveryAtOtherBank))
                        throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete);
                    Add(AccountingConstants.NrcTransfersPayableGlCode, EntryType.Debit);
                    Add(deliveryType == TransactionConstants.NrcDeliveryAtBranch
                        ? AccountingConstants.CashOnHandGlCode : AccountingConstants.DueFromOtherBanksGlCode, EntryType.Credit);
                }
                break;
            }
            case TransactionType.InterestAccrual:
                Add(AccountingConstants.InterestExpenseGlCode, EntryType.Debit);
                Add(AccountingConstants.InterestPayableGlCode, EntryType.Credit);
                break;
            case TransactionType.MaintenanceAccrual:
                Add(AccountingConstants.MaintenanceFeeReceivableGlCode, EntryType.Debit);
                Add(AccountingConstants.MaintenanceFeeIncomeGlCode, EntryType.Credit);
                break;
            case TransactionType.DormantPenaltyAccrual:
                Add(AccountingConstants.DormantPenaltyReceivableGlCode, EntryType.Debit);
                Add(AccountingConstants.DormantPenaltyIncomeGlCode, EntryType.Credit);
                break;
            case TransactionType.InterestCredit:
                Add(AccountingConstants.InterestPayableGlCode, EntryType.Debit);
                Add(AccountingConstants.CustomerDepositsGlCode, EntryType.Credit);
                break;
            case TransactionType.MaintenanceFee:
                Add(AccountingConstants.CustomerDepositsGlCode, EntryType.Debit);
                Add(AccountingConstants.MaintenanceFeeReceivableGlCode, EntryType.Credit);
                break;
            case TransactionType.Penalty:
                Add(AccountingConstants.CustomerDepositsGlCode, EntryType.Debit);
                Add(AccountingConstants.DormantPenaltyReceivableGlCode, EntryType.Credit);
                break;
            case TransactionType.Reversal:
            {
                if (!reversalOfTransactionId.HasValue)
                    throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete);
                var originalType = await _dbContext.Transactions.AsNoTracking()
                    .Where(item => item.Id == reversalOfTransactionId.Value)
                    .Select(item => (TransactionType?)item.TransactionType).SingleOrDefaultAsync(cancellationToken)
                    ?? throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete);
                var sourceAccountExists = await _dbContext.AccountTransactions.AsNoTracking()
                    .AnyAsync(entry => entry.TransactionId == reversalOfTransactionId.Value && entry.EntryType == EntryType.Debit, cancellationToken);
                var clearingCode = originalType switch
                {
                    TransactionType.InterbankTransfer => AccountingConstants.InterbankClearingGlCode,
                    TransactionType.NrcTransfer => AccountingConstants.NrcTransfersPayableGlCode,
                    _ => throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete)
                };
                Add(clearingCode, EntryType.Debit);
                Add(sourceAccountExists ? AccountingConstants.CustomerDepositsGlCode : AccountingConstants.CashOnHandGlCode, EntryType.Credit);
                break;
            }
            default:
                throw new BusinessRuleException(MessageCode.TransactionAccountingEntriesIncomplete);
        }
        return lines;
    }

    /// <summary>
    /// Runs an operation inside a database transaction and commits it when the operation succeeds.
    /// The operation must save its own changes. Any exception rolls everything back.
    /// </summary>
    public async Task<T> RunInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Hold the currently open business-date row through commit so EOD cannot close across an in-flight posting.
        await GetPostingBusinessDateAsync(DateTime.UtcNow, cancellationToken);
        var result = await operation();
        await dbTransaction.CommitAsync(cancellationToken);

        return result;
    }


    /// <summary>
    /// Loads and row-locks (SELECT ... FOR UPDATE) the accounts in ascending id order, with their account types.
    /// Concurrent postings on the same account wait for each other, and two transfers cannot deadlock.
    /// Must be called inside a database transaction; the locks are released on commit or rollback.
    /// </summary>
    /// <param name="isRefund">
    /// Refunds only need the account to be open: money being returned may go into a frozen or suspended account.
    /// Normal postings need an operational account.
    /// </param>
    public async Task<IReadOnlyDictionary<long, Account>> LockAccountsAsync(
        IEnumerable<long> accountIds,
        bool isRefund,
        CancellationToken cancellationToken)
    {
        var accounts = new Dictionary<long, Account>();
        foreach (var accountId in accountIds.Distinct().Order())
        {
            // The SQL is not composed with LINQ operators so EF does not wrap the locking clause in a subquery.
            var account = (await _dbContext.Accounts
                    .FromSql($"SELECT * FROM Accounts WHERE Id = {accountId} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                .SingleOrDefault();

            if (account is null)
            {
                throw new NotFoundException(MessageCode.AccountNotFound);
            }

            var isUsable = isRefund
                ? account.Status != AccountStatus.Closed
                : account.Status is not (AccountStatus.Closed or AccountStatus.Frozen or AccountStatus.Suspended);
            if (!isUsable)
            {
                throw new BusinessRuleException(MessageCode.AccountNotOperational);
            }

            accounts.Add(accountId, account);
        }

        // Tracked loading lets EF attach each account type to its locked account.
        var accountTypeIds = accounts.Values.Select(account => account.AccountTypeId).Distinct().ToList();
        await _dbContext.AccountTypes
            .Where(type => accountTypeIds.Contains(type.Id))
            .LoadAsync(cancellationToken);

        return accounts;
    }

    /// <summary>
    /// Applies the account type's deposit or debit rules. Deposits check deposit permission only; withdrawals and
    /// transfers also check available balance, minimum maintained balance, and configured debit limits. The account
    /// must be locked by <see cref="LockAccountsAsync"/>, which keeps balances and limit totals stable until commit.
    /// </summary>
    public async Task EnsureCanDebitAsync(
        Account account,
        decimal amount,
        DebitPurpose purpose,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var accountType = account.AccountType
            ?? throw new InvalidOperationException("The account type must be loaded before checking debit rules.");

        if (purpose == DebitPurpose.Deposit)
        {
            if (!accountType.AllowDeposit)
            {
                throw new BusinessRuleException(MessageCode.DepositNotAllowed);
            }

            return;
        }
        if (purpose == DebitPurpose.Withdrawal && !accountType.AllowWithdrawal)
        {
            throw new BusinessRuleException(MessageCode.WithdrawalNotAllowed);
        }

        if (purpose == DebitPurpose.Transfer && !accountType.AllowTransfer)
        {
            throw new BusinessRuleException(MessageCode.TransferNotAllowed);
        }

        if (account.AvailableBalance < amount)
        {
            throw new BusinessRuleException(MessageCode.InsufficientBalance);
        }

        if (account.AvailableBalance - amount < accountType.MinimumMaintainedBalance)
        {
            throw new BusinessRuleException(MessageCode.MinimumBalanceRequired);
        }

        if (accountType.DailyTransactionLimit is null && accountType.MonthlyTransactionLimit is null)
        {
            return;
        }

        // Limits reset at Myanmar midnight: posting dates are business dates, the same dates stored on every entry.
        var today = await GetPostingBusinessDateAsync(now, cancellationToken);
        var firstDayOfMonth = new DateOnly(today.Year, today.Month, 1);
        var monthDebits = _dbContext.AccountTransactions
            .AsNoTracking()
            .Where(entry => entry.AccountId == account.Id
                && entry.EntryType == EntryType.Debit
                && entry.PostingDate >= firstDayOfMonth
                && LimitedTransactionTypes.Contains(entry.Transaction!.TransactionType)
                && !RefundedStatuses.Contains(entry.Transaction!.TransactionStatus));

        if (accountType.DailyTransactionLimit is { } dailyLimit)
        {
            var debitedToday = await monthDebits
                .Where(entry => entry.PostingDate == today)
                .SumAsync(entry => (decimal?)entry.Amount, cancellationToken) ?? 0m;
            if (debitedToday + amount > dailyLimit)
            {
                throw new BusinessRuleException(MessageCode.DailyTransactionLimitExceeded);
            }
        }

        if (accountType.MonthlyTransactionLimit is { } monthlyLimit)
        {
            var debitedThisMonth = await monthDebits
                .SumAsync(entry => (decimal?)entry.Amount, cancellationToken) ?? 0m;
            if (debitedThisMonth + amount > monthlyLimit)
            {
                throw new BusinessRuleException(MessageCode.MonthlyTransactionLimitExceeded);
            }
        }
    }

    /// <summary>
    /// Creates a transaction header. Completed transactions are also marked as posted by the initiating user.
    /// </summary>
    public static Transaction CreateTransaction(
        TransactionType type,
        TransactionStatus status,
        decimal amount,
        string? description,
        string? referenceNo,
        string? idempotencyKey,
        long userId,
        DateTime now)
    {
        var isCompleted = status == TransactionStatus.Completed;

        return new Transaction
        {
            TransactionNo = GenerateTransactionNumber(now),
            TransactionType = type,
            TransactionStatus = status,
            InitiatedBy = userId,
            PostedBy = isCompleted ? userId : null,
            PostedAt = isCompleted ? now : null,
            Amount = amount,
            FeeAmount = 0m,
            TransactionAt = now,
            Description = TransactionRequestValidator.TrimToNull(description),
            ReferenceNo = TransactionRequestValidator.TrimToNull(referenceNo),
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Applies the transaction amount to the customer account, records the account entry with before/after balances,
    /// and writes the matching Customer Deposits ledger line. Callers must check debit rules first.
    /// </summary>
    public async Task PostCustomerEntryAsync(
        Transaction transaction,
        Account account,
        EntryType entryType,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var amount = transaction.Amount;
        var ledgerBalanceBefore = account.LedgerBalance;
        var availableBalanceBefore = account.AvailableBalance;
        var signedAmount = entryType == EntryType.Debit ? -amount : amount;
        var postingDate = await GetPostingBusinessDateAsync(now, cancellationToken);
        transaction.BusinessDate ??= postingDate;

        account.LedgerBalance += signedAmount;
        account.AvailableBalance += signedAmount;
        account.LastActivityAt = now;
        account.UpdatedAt = now;
        await ReactivateDormantAccountAsync(account, transaction.InitiatedBy, now, cancellationToken);

        _dbContext.AccountTransactions.Add(new AccountTransaction
        {
            Transaction = transaction,
            Account = account,
            EntryType = entryType,
            Amount = amount,
            LedgerBalanceBefore = ledgerBalanceBefore,
            LedgerBalanceAfter = account.LedgerBalance,
            AvailableBalanceBefore = availableBalanceBefore,
            AvailableBalanceAfter = account.AvailableBalance,
            ValueDate = postingDate,
            PostingDate = postingDate,
            Description = transaction.Description,
            ReferenceNo = transaction.ReferenceNo,
            Status = TransactionConstants.CompletedStatus,
            CreatedAt = now
        });

        // Customer deposits are a liability: a customer debit debits it and a customer credit credits it.
        await PostGlEntryAsync(
            transaction,
            AccountingConstants.CustomerDepositsGlCode,
            entryType,
            account.Id,
            now,
            cancellationToken);
    }

    /// <summary>
    /// Moves a dormant account back to Active when a teller posting touches it, so the monthly dormant penalty
    /// stops once the customer uses the account again. Scheduled interest, fee and penalty postings do not come
    /// through here and therefore never count as customer activity.
    /// </summary>
    private async Task ReactivateDormantAccountAsync(
        Account account,
        long changedBy,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (account.Status != AccountStatus.Dormant)
        {
            return;
        }

        account.Status = AccountStatus.Active;
        account.ActiveAt = now;
        await _dbContext.AccountStatusHistories.AddAsync(new AccountStatusHistory
        {
            Account = account,
            OldStatus = AccountStatus.Dormant,
            NewStatus = AccountStatus.Active,
            Reason = AccountConstants.DormantReactivationReason,
            ChangedBy = changedBy,
            ChangedAt = now
        }, cancellationToken);
    }

    /// <summary>
    /// Writes one general-ledger line for the transaction amount. Every posting writes lines whose debits equal
    /// its credits.
    /// </summary>
    public async Task PostGlEntryAsync(
        Transaction transaction,
        string glAccountCode,
        EntryType entryType,
        long? customerAccountId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var postingDate = await GetPostingBusinessDateAsync(now, cancellationToken);
        transaction.BusinessDate ??= postingDate;
        _dbContext.TransactionEntries.Add(new TransactionEntry
        {
            Transaction = transaction,
            GlAccountId = await GetGlAccountIdAsync(glAccountCode, cancellationToken),
            CustomerAccountId = customerAccountId,
            EntryType = entryType,
            Amount = transaction.Amount,
            PostingDate = postingDate,
            Description = transaction.Description,
            CreatedAt = now
        });
    }

    private async Task<DateOnly> GetPostingBusinessDateAsync(DateTime now, CancellationToken cancellationToken)
    {
        // Posting and EOD share the same locked business-date row and rollover validation.
        return await _businessDates.GetPostingBusinessDateValueAsync(now, cancellationToken);
    }

    /// <summary>
    /// Creates a completed Reversal transaction that returns the original transfer's amount from the clearing ledger
    /// account the way it was paid in: to the account the original debited, or in cash when the sender paid cash
    /// (an NRC transfer without a source account). The caller changes the original's status and saves.
    /// </summary>
    public async Task<Transaction> CreateRefundAsync(
        Transaction original,
        string clearingGlAccountCode,
        string? reason,
        RequestActor actor,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var sourceAccountId = await _dbContext.AccountTransactions
            .AsNoTracking()
            .Where(entry => entry.TransactionId == original.Id && entry.EntryType == EntryType.Debit)
            .Select(entry => (long?)entry.AccountId)
            .FirstOrDefaultAsync(cancellationToken);

        var refund = CreateTransaction(
            TransactionType.Reversal,
            TransactionStatus.Completed,
            original.Amount,
            reason,
            original.ReferenceNo,
            null,
            actor.UserId,
            now);
        refund.ReversalOfTransactionId = original.Id;
        await PostGlEntryAsync(refund, clearingGlAccountCode, EntryType.Debit, null, now, cancellationToken);

        if (sourceAccountId is { } accountId)
        {
            var accounts = await LockAccountsAsync([accountId], isRefund: true, cancellationToken);
            await PostCustomerEntryAsync(refund, accounts[accountId], EntryType.Credit, now, cancellationToken);
        }
        else
        {
            // Paid in cash, so the sender gets the cash back at the counter.
            await PostGlEntryAsync(refund, AccountingConstants.CashOnHandGlCode, EntryType.Credit, null, now, cancellationToken);
        }

        await _dbContext.Transactions.AddAsync(refund, cancellationToken);

        return refund;
    }

    /// <summary>
    /// Adds an audit-log row for a transaction action. <paramref name="details"/> is stored as JSON and must never
    /// contain secrets such as pickup codes.
    /// </summary>
    public void AddAuditLog(
        string action,
        Transaction transaction,
        RequestActor actor,
        object details,
        DateTime now)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            Action = action,
            EntityType = AuditConstants.TransactionEntityType,

            // The transaction number is known before saving, unlike the database id.
            EntityId = transaction.TransactionNo,
            NewValues = JsonSerializer.Serialize(details, AuditJsonOptions),
            IpAddress = actor.IpAddress,
            DeviceInfo = actor.DeviceInfo,
            CreatedAt = now
        });
    }

    /// <summary>
    /// Builds the standard response for a saved transaction, taking the source and destination accounts from its
    /// entries. A pending NRC transfer has no credit yet, so its destination comes from the NRC detail.
    /// </summary>
    public async Task<TransactionResponse> BuildTransactionResponseAsync(
        long transactionId,
        CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.Transactions
            .AsNoTracking()
            .FirstAsync(item => item.Id == transactionId, cancellationToken);
        var entries = await _dbContext.AccountTransactions
            .AsNoTracking()
            .Where(entry => entry.TransactionId == transactionId)
            .Select(entry => new { entry.AccountId, entry.EntryType })
            .ToListAsync(cancellationToken);

        var sourceAccountId = entries.FirstOrDefault(entry => entry.EntryType == EntryType.Debit)?.AccountId;
        var destinationAccountId = entries.FirstOrDefault(entry => entry.EntryType == EntryType.Credit)?.AccountId;
        if (destinationAccountId is null && transaction.TransactionType == TransactionType.NrcTransfer)
        {
            destinationAccountId = await _dbContext.NrcCashTransferDetails
                .AsNoTracking()
                .Where(detail => detail.TransactionId == transactionId)
                .Select(detail => detail.DestinationAccountId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return ToResponse(transaction, sourceAccountId, destinationAccountId);
    }

    /// <summary>
    /// Converts a transaction entity into the API response contract.
    /// </summary>
    public static TransactionResponse ToResponse(
        Transaction transaction,
        long? sourceAccountId,
        long? destinationAccountId,
        string? pickupCode = null)
    {
        return new TransactionResponse(
            transaction.Id,
            transaction.TransactionNo,
            transaction.TransactionType,
            transaction.TransactionStatus,
            transaction.Amount,
            transaction.FeeAmount,
            transaction.TransactionAt,
            transaction.ReferenceNo,
            sourceAccountId,
            destinationAccountId,
            pickupCode)
        {
            BusinessDate = transaction.BusinessDate
        };
    }

    // Trims the key, treats a blank key as absent and rejects keys longer than the column.
    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        var key = TransactionRequestValidator.TrimToNull(idempotencyKey);
        TransactionRequestValidator.EnsureMaximumLength(key, TransactionConstants.IdempotencyKeyMaximumLength);

        return key;
    }

    // Returns the original result when the key was already used for the same request, or null when the key is new.
    // A key reused for a different user, transaction type, amount or set of accounts is a client bug and is rejected.
    private async Task<TransactionResponse?> FindIdempotentReplayAsync(
        string? idempotencyKey,
        TransactionType transactionType,
        decimal amount,
        long userId,
        IReadOnlyCollection<long> customerAccountIds,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is null)
        {
            return null;
        }

        var existing = await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.IdempotencyKey == idempotencyKey)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.TransactionType,
                transaction.Amount,
                transaction.InitiatedBy
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (existing.TransactionType != transactionType
            || existing.Amount != amount
            || existing.InitiatedBy != userId)
        {
            throw new ConflictException(MessageCode.IdempotencyKeyReused);
        }

        // Compare the accounts the original posting touched with the accounts this request names.
        var postedAccountIds = await _dbContext.AccountTransactions
            .AsNoTracking()
            .Where(entry => entry.TransactionId == existing.Id)
            .Select(entry => entry.AccountId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (!postedAccountIds.ToHashSet().SetEquals(customerAccountIds))
        {
            throw new ConflictException(MessageCode.IdempotencyKeyReused);
        }

        return await BuildTransactionResponseAsync(existing.Id, cancellationToken);
    }

    // Resolves a general-ledger account id by code. A missing account is a setup error, not a client error.
    private async Task<long> GetGlAccountIdAsync(string code, CancellationToken cancellationToken)
    {
        if (_glAccountIdsByCode.TryGetValue(code, out var cachedId))
        {
            return cachedId;
        }

        var glAccountId = await _dbContext.GlAccounts
            .AsNoTracking()
            .Where(account => account.Code == code)
            .Select(account => (long?)account.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"General-ledger account {code} is missing; it is created by ChartOfAccountsSeeder.");

        _glAccountIdsByCode[code] = glAccountId;
        return glAccountId;
    }

    // Generates a readable transaction number; the random suffix keeps numbers unique within the same millisecond.
    private static string GenerateTransactionNumber(DateTime now)
    {
        return string.Concat(
            TransactionConstants.TransactionNumberPrefix,
            now.ToString(TransactionConstants.TransactionNumberTimestampFormat),
            RandomNumberGenerator
                .GetInt32(TransactionConstants.TransactionNumberSuffixExclusiveMaximum)
                .ToString(TransactionConstants.TransactionNumberSuffixFormat));
    }
}
