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
using bams.server.Models.Products;
using bams.server.Models.Transactions;
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
    // Customer-initiated debits count against an account type's daily, weekly and monthly limits; fees and refunds
    // do not.
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
    /// <param name="customerAccountIds">
    /// The customer accounts the request posts to (the accounts that receive an account entry). A replay must name
    /// the same accounts, so a key reused for another account is rejected instead of silently returning the first.
    /// </param>
    /// <param name="postAsync">Posts and saves the transaction; receives the normalized idempotency key to store.</param>
    /// <param name="debitedAccountId">
    /// For postings that touch several customer accounts, the account that is debited. A replay must debit the same
    /// account, so a key reused for the reverse transfer is rejected instead of returning the original direction.
    /// </param>
    public async Task<TransactionResponse> RunIdempotentPostingAsync(
        string? idempotencyKey,
        TransactionType transactionType,
        decimal amount,
        long userId,
        IReadOnlyCollection<long> customerAccountIds,
        Func<string?, Task<TransactionResponse>> postAsync,
        CancellationToken cancellationToken,
        long? debitedAccountId = null)
    {
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        var replay = await FindIdempotentReplayAsync(
            normalizedKey,
            transactionType,
            amount,
            userId,
            customerAccountIds,
            debitedAccountId,
            cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        try
        {
            return await RunInTransactionAsync(() => postAsync(normalizedKey), cancellationToken);
        }
        catch (Exception exception) when (
            normalizedKey is not null && exception is DbUpdateException or BusinessRuleException)
        {
            // A parallel request with the same key committed first. Either the unique index rejected this one, or it
            // waited on the account lock and then failed a business rule (e.g. the balance the original already
            // spent). The rollback already happened; forget the rejected changes and return the committed result.
            _dbContext.ChangeTracker.Clear();
            var concurrentReplay = await FindIdempotentReplayAsync(
                normalizedKey,
                transactionType,
                amount,
                userId,
                customerAccountIds,
                debitedAccountId,
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
    /// Applies the account type's deposit or debit rules. Deposits check deposit permission and the minimum deposit
    /// amount; debits check permission, the minimum withdrawal amount, available balance, minimum maintained balance,
    /// and the daily, weekly, monthly and daily-withdrawal limits. The account must be locked by
    /// <see cref="LockAccountsAsync"/>, which also keeps the limit totals stable until the posting commits.
    /// </summary>
    /// <param name="feeAmount">
    /// Fee debited together with <paramref name="amount"/>. The balance must cover both, but only the amount itself
    /// counts against the transaction limits.
    /// </param>
    public async Task EnsureCanDebitAsync(
        Account account,
        decimal amount,
        DebitPurpose purpose,
        DateTime now,
        CancellationToken cancellationToken,
        decimal feeAmount = 0m)
    {
        var accountType = account.AccountType
            ?? throw new InvalidOperationException("The account type must be loaded before checking debit rules.");

        if (purpose == DebitPurpose.Deposit)
        {
            // A deposit adds money, so balance, minimum-balance and debit-limit checks do not apply.
            if (!accountType.AllowDeposit)
            {
                throw new BusinessRuleException(MessageCode.DepositNotAllowed);
            }

            // A null minimum means any positive amount is accepted.
            if (amount < accountType.MinimumDepositAmount)
            {
                throw new BusinessRuleException(MessageCode.DepositBelowMinimumAmount);
            }

            return;
        }

        EnsureDebitIsPermitted(accountType, amount, purpose);
        EnsureBalanceCoversDebit(account, amount + feeAmount);
        await EnsureWithinTransactionLimitsAsync(account, amount, purpose, now, cancellationToken);
    }

    // Rejects a withdrawal or transfer the account type does not allow, and a withdrawal below its minimum amount.
    private static void EnsureDebitIsPermitted(AccountType accountType, decimal amount, DebitPurpose purpose)
    {
        if (purpose == DebitPurpose.Withdrawal && !accountType.AllowWithdrawal)
        {
            throw new BusinessRuleException(MessageCode.WithdrawalNotAllowed);
        }

        // A null minimum means any positive amount is accepted.
        if (purpose == DebitPurpose.Withdrawal && amount < accountType.MinimumWithdrawalAmount)
        {
            throw new BusinessRuleException(MessageCode.WithdrawalBelowMinimumAmount);
        }

        if (purpose == DebitPurpose.Transfer && !accountType.AllowTransfer)
        {
            throw new BusinessRuleException(MessageCode.TransferNotAllowed);
        }
    }

    // The whole debit (amount plus any fee) must be available and must leave the minimum maintained balance.
    private static void EnsureBalanceCoversDebit(Account account, decimal totalDebit)
    {
        if (account.AvailableBalance < totalDebit)
        {
            throw new BusinessRuleException(MessageCode.InsufficientBalance);
        }

        if (account.AvailableBalance - totalDebit < account.AccountType!.MinimumMaintainedBalance)
        {
            throw new BusinessRuleException(MessageCode.MinimumBalanceRequired);
        }
    }

    // Checks the debit against the account type's daily, daily-withdrawal, weekly and monthly limits. A null limit
    // means no limit. Limits reset at Myanmar midnight, on Monday and on the 1st: posting dates are business dates.
    private async Task EnsureWithinTransactionLimitsAsync(
        Account account,
        decimal amount,
        DebitPurpose purpose,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var accountType = account.AccountType!;
        var withdrawalLimit = purpose == DebitPurpose.Withdrawal ? accountType.DailyWithdrawalLimit : null;
        if (accountType.DailyTransactionLimit is null
            && accountType.WeeklyTransactionLimit is null
            && accountType.MonthlyTransactionLimit is null
            && withdrawalLimit is null)
        {
            return;
        }

        var today = BusinessTime.ToBusinessDate(now);
        var firstDayOfMonth = new DateOnly(today.Year, today.Month, 1);
        var daysSinceWeekStart = ((int)today.DayOfWeek - (int)TransactionConstants.FirstDayOfBusinessWeek
            + TransactionConstants.DaysPerWeek) % TransactionConstants.DaysPerWeek;
        var firstDayOfWeek = today.AddDays(-daysSinceWeekStart);
        var debits = await GetLimitedDebitsSinceAsync(
            account.Id,
            firstDayOfWeek < firstDayOfMonth ? firstDayOfWeek : firstDayOfMonth,
            cancellationToken);

        decimal SumDebits(Func<LimitedDebit, bool> predicate) => debits.Where(predicate).Sum(debit => debit.Amount);

        if (accountType.DailyTransactionLimit is { } dailyLimit
            && SumDebits(debit => debit.PostingDate == today) + amount > dailyLimit)
        {
            throw new BusinessRuleException(MessageCode.DailyTransactionLimitExceeded);
        }

        if (withdrawalLimit is { } dailyWithdrawalLimit
            && SumDebits(debit => debit.PostingDate == today && debit.TransactionType == TransactionType.CashWithdrawal)
                + amount > dailyWithdrawalLimit)
        {
            throw new BusinessRuleException(MessageCode.DailyWithdrawalLimitExceeded);
        }

        if (accountType.WeeklyTransactionLimit is { } weeklyLimit
            && SumDebits(debit => debit.PostingDate >= firstDayOfWeek) + amount > weeklyLimit)
        {
            throw new BusinessRuleException(MessageCode.WeeklyTransactionLimitExceeded);
        }

        if (accountType.MonthlyTransactionLimit is { } monthlyLimit
            && SumDebits(debit => debit.PostingDate >= firstDayOfMonth) + amount > monthlyLimit)
        {
            throw new BusinessRuleException(MessageCode.MonthlyTransactionLimitExceeded);
        }
    }

    // Loads the account's limit-counted debits posted on or after windowStart, one row per transaction: a transfer
    // with a fee has two debit entries (amount and fee), but only the transaction amount counts against the limits.
    // The window is at most about five weeks of one account's limited debits, so it is summed in memory.
    private async Task<List<LimitedDebit>> GetLimitedDebitsSinceAsync(
        long accountId,
        DateOnly windowStart,
        CancellationToken cancellationToken)
    {
        var debits = await _dbContext.AccountTransactions
            .AsNoTracking()
            .Where(entry => entry.AccountId == accountId
                && entry.EntryType == EntryType.Debit
                && entry.PostingDate >= windowStart
                && LimitedTransactionTypes.Contains(entry.Transaction!.TransactionType)
                && !RefundedStatuses.Contains(entry.Transaction!.TransactionStatus))
            .Select(entry => new
            {
                entry.TransactionId,
                entry.PostingDate,
                entry.Transaction!.TransactionType,
                entry.Transaction!.Amount
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        return debits
            .Select(debit => new LimitedDebit(debit.TransactionId, debit.PostingDate, debit.TransactionType, debit.Amount))
            .ToList();
    }

    // A customer debit that counts against the transaction limits.
    private sealed record LimitedDebit(
        long TransactionId,
        DateOnly PostingDate,
        TransactionType TransactionType,
        decimal Amount);

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
    /// <param name="amount">Amount of this entry; defaults to the transaction amount (a fee line passes the fee).</param>
    /// <param name="description">Entry description; defaults to the transaction description.</param>
    public async Task PostCustomerEntryAsync(
        Transaction transaction,
        Account account,
        EntryType entryType,
        DateTime now,
        CancellationToken cancellationToken,
        decimal? amount = null,
        string? description = null)
    {
        var entryAmount = amount ?? transaction.Amount;
        var ledgerBalanceBefore = account.LedgerBalance;
        var availableBalanceBefore = account.AvailableBalance;
        var signedAmount = entryType == EntryType.Debit ? -entryAmount : entryAmount;

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
            Amount = entryAmount,
            LedgerBalanceBefore = ledgerBalanceBefore,
            LedgerBalanceAfter = account.LedgerBalance,
            AvailableBalanceBefore = availableBalanceBefore,
            AvailableBalanceAfter = account.AvailableBalance,
            ValueDate = BusinessTime.ToBusinessDate(now),
            PostingDate = BusinessTime.ToBusinessDate(now),
            Description = description ?? transaction.Description,
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
            cancellationToken,
            entryAmount);
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
    /// Writes one general-ledger line for the transaction amount, or for <paramref name="amount"/> when given (fee
    /// lines). Every posting writes lines whose debits equal its credits.
    /// </summary>
    public async Task PostGlEntryAsync(
        Transaction transaction,
        string glAccountCode,
        EntryType entryType,
        long? customerAccountId,
        DateTime now,
        CancellationToken cancellationToken,
        decimal? amount = null)
    {
        _dbContext.TransactionEntries.Add(new TransactionEntry
        {
            Transaction = transaction,
            GlAccountId = await GetGlAccountIdAsync(glAccountCode, cancellationToken),
            CustomerAccountId = customerAccountId,
            EntryType = entryType,
            Amount = amount ?? transaction.Amount,
            PostingDate = BusinessTime.ToBusinessDate(now),
            Description = transaction.Description,
            CreatedAt = now
        });
    }

    /// <summary>
    /// Charges a transfer fee to the account the transfer debits, as a second debit entry on the same transaction,
    /// and records it in <see cref="Transaction.FeeAmount"/>. A zero fee posts nothing.
    /// Ledger: debit Customer Deposits, credit Transfer Fee Income.
    /// </summary>
    public async Task PostTransferFeeAsync(
        Transaction transaction,
        Account account,
        decimal feeAmount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (feeAmount <= 0m)
        {
            return;
        }

        transaction.FeeAmount = feeAmount;
        await PostCustomerEntryAsync(
            transaction,
            account,
            EntryType.Debit,
            now,
            cancellationToken,
            feeAmount,
            TransactionConstants.TransferFeeEntryDescription);
        await PostGlEntryAsync(
            transaction,
            AccountingConstants.TransferFeeIncomeGlCode,
            EntryType.Credit,
            null,
            now,
            cancellationToken,
            feeAmount);
    }

    /// <summary>
    /// Creates a completed Reversal transaction that returns the original transfer's amount from the clearing ledger
    /// account the way it was paid in: to the account the original debited, or in cash when the sender paid cash
    /// (an NRC transfer without a source account). A transfer fee the original charged is returned too. The caller
    /// changes the original's status and saves.
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
            await RefundTransferFeeAsync(original, refund, accounts[accountId], now, cancellationToken);
        }
        else
        {
            // Paid in cash, so the sender gets the cash back at the counter.
            await PostGlEntryAsync(refund, AccountingConstants.CashOnHandGlCode, EntryType.Credit, null, now, cancellationToken);
        }

        await _dbContext.Transactions.AddAsync(refund, cancellationToken);

        return refund;
    }

    // The transfer did not go through, so the fee it charged goes back to the same account on the refund.
    // Ledger: debit Transfer Fee Income, credit Customer Deposits.
    private async Task RefundTransferFeeAsync(
        Transaction original,
        Transaction refund,
        Account account,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (original.FeeAmount <= 0m)
        {
            return;
        }

        await PostGlEntryAsync(
            refund,
            AccountingConstants.TransferFeeIncomeGlCode,
            EntryType.Debit,
            null,
            now,
            cancellationToken,
            original.FeeAmount);
        await PostCustomerEntryAsync(
            refund,
            account,
            EntryType.Credit,
            now,
            cancellationToken,
            original.FeeAmount,
            TransactionConstants.TransferFeeRefundEntryDescription);
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
    // A key reused for a different user, transaction type, amount or set of accounts is a client bug and is rejected.
    private async Task<TransactionResponse?> FindIdempotentReplayAsync(
        string? idempotencyKey,
        TransactionType transactionType,
        decimal amount,
        long userId,
        IReadOnlyCollection<long> customerAccountIds,
        long? debitedAccountId,
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
        var postedEntries = await _dbContext.AccountTransactions
            .AsNoTracking()
            .Where(entry => entry.TransactionId == existing.Id)
            .Select(entry => new { entry.AccountId, entry.EntryType })
            .ToListAsync(cancellationToken);
        if (!postedEntries.Select(entry => entry.AccountId).ToHashSet().SetEquals(customerAccountIds))
        {
            throw new ConflictException(MessageCode.IdempotencyKeyReused);
        }

        if (debitedAccountId is { } expectedDebitedAccountId
            && !postedEntries.Any(entry =>
                entry.AccountId == expectedDebitedAccountId && entry.EntryType == EntryType.Debit))
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
