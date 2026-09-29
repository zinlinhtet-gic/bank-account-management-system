using System.Security.Cryptography;
using System.Text.Json;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Transactions;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Audit;
using bams.server.Models.Transactions;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>
/// What a debit is for. It decides which account-type permission (withdrawal or transfer) applies.
/// </summary>
public enum DebitPurpose
{
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

    // General-ledger ids do not change at runtime, so each is looked up at most once per request.
    private readonly Dictionary<string, long> _glAccountIdsByCode = new();

    public LedgerPostingService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Runs a posting inside a database transaction. When the client sent an idempotency key that was already used
    /// for the same kind of transaction, the original result is returned and nothing is posted again.
    /// </summary>
    /// <param name="postAsync">Posts and saves the transaction; receives the normalized idempotency key to store.</param>
    public async Task<TransactionResponse> RunIdempotentPostingAsync(
        string? idempotencyKey,
        TransactionType transactionType,
        decimal amount,
        long userId,
        Func<string?, Task<TransactionResponse>> postAsync,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        var replay = await FindIdempotentReplayAsync(normalizedKey, transactionType, amount, userId, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        try
        {
            return await RunInTransactionAsync(() => postAsync(normalizedKey), cancellationToken);
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
                cancellationToken);
            if (concurrentReplay is null)
            {
                throw;
            }

            return concurrentReplay;
        }
    }

    /// <summary>
    /// Runs an operation inside a database transaction and commits it when the operation succeeds.
    /// The operation must save its own changes. Any exception rolls everything back.
    /// </summary>
    public async Task<T> RunInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
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
    /// Applies the account type's debit rules: withdrawal/transfer permission, available balance, minimum maintained
    /// balance, and daily and monthly limits. The account must have been locked by <see cref="LockAccountsAsync"/>,
    /// which also keeps the limit totals stable until the posting commits.
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

        // Posting dates are UTC dates, the same dates stored on every account entry.
        var today = DateOnly.FromDateTime(now);
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

        account.LedgerBalance += signedAmount;
        account.AvailableBalance += signedAmount;
        account.LastActivityAt = now;
        account.UpdatedAt = now;

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
            ValueDate = DateOnly.FromDateTime(now),
            PostingDate = DateOnly.FromDateTime(now),
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
        _dbContext.TransactionEntries.Add(new TransactionEntry
        {
            Transaction = transaction,
            GlAccountId = await GetGlAccountIdAsync(glAccountCode, cancellationToken),
            CustomerAccountId = customerAccountId,
            EntryType = entryType,
            Amount = transaction.Amount,
            PostingDate = DateOnly.FromDateTime(now),
            Description = transaction.Description,
            CreatedAt = now
        });
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
            pickupCode);
    }

    // Trims the key, treats a blank key as absent and rejects keys longer than the column.
    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        var key = TransactionRequestValidator.TrimToNull(idempotencyKey);
        TransactionRequestValidator.EnsureMaximumLength(key, TransactionConstants.IdempotencyKeyMaximumLength);

        return key;
    }

    // Returns the original result when the key was already used for the same request, or null when the key is new.
    // A key reused for a different user, transaction type or amount is a client bug and is rejected.
    private async Task<TransactionResponse?> FindIdempotentReplayAsync(
        string? idempotencyKey,
        TransactionType transactionType,
        decimal amount,
        long userId,
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
