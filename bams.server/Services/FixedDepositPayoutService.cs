using System.Globalization;
using System.Text.Json;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.DTO.Common;
using bams.server.DTO.Transactions;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Audit;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using bams.server.Services.Jobs;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>
/// Moves money out of fixed deposits, the only way it leaves them: fixed-deposit account types allow no
/// withdrawals or transfers, so these postings deliberately skip the teller debit rules.
/// <list type="bullet">
/// <item>Maturity (scheduled): credits the term's remaining interest, then follows the renewal instruction —
/// pay everything out and close, renew the principal and pay out the interest, or renew everything.</item>
/// <item>Early withdrawal (manager request): credits interest to date, forfeits interest above the rate rule's
/// early-withdrawal rate, pays the rest out, and closes the deposit and its account.</item>
/// </list>
/// Payouts always go to the deposit's payout account, as one customer-to-customer posting.
/// </summary>
/// <remarks>
/// Interest is settled in its own committed steps before the payout transaction (the interest postings manage their
/// own database transactions). Each step is safe to repeat, so a payout that fails is completed by the next attempt.
/// </remarks>
public sealed class FixedDepositPayoutService : IFixedDepositPayoutService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly LedgerPostingService _ledger;
    private readonly InterestAccumulationService _interest;
    private readonly ScheduledFinancialPostingService _scheduledPostings;
    private readonly IBusinessDateService _businessDates;
    private readonly IFixedDepositService _fixedDeposits;
    private readonly ILogger<FixedDepositPayoutService> _logger;

    public FixedDepositPayoutService(
        ApplicationDbContext dbContext,
        LedgerPostingService ledger,
        InterestAccumulationService interest,
        ScheduledFinancialPostingService scheduledPostings,
        IBusinessDateService businessDates,
        IFixedDepositService fixedDeposits,
        ILogger<FixedDepositPayoutService> logger)
    {
        _fixedDeposits = fixedDeposits;
        _dbContext = dbContext;
        _ledger = ledger;
        _interest = interest;
        _scheduledPostings = scheduledPostings;
        _businessDates = businessDates;
        _logger = logger;
    }

    // ===== Maturity =====

    /// <summary>
    /// Scheduled job: pays out or renews every active fixed deposit whose maturity date has arrived. Runs only while
    /// the business date is open, because payouts post on it. A deposit that fails is logged and retried next run;
    /// the run then fails so the failure shows on the scheduled-jobs page.
    /// </summary>
    public async Task MatureDueFixedDepositsAsync(
        ScheduledJobExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        if (!await IsBusinessDateOpenAsync(now, cancellationToken))
        {
            _logger.LogInformation(
                "Fixed-deposit maturity run {ScheduledForUtc} skipped: the business date is not open.",
                executionContext.ScheduledForUtc);
            return;
        }

        DateOnly today = BusinessTime.ToBusinessDate(now);
        IReadOnlyList<long> dueDepositIds = await GetDueFixedDepositIdsAsync(today, cancellationToken);
        RequestActor systemActor = new(
            await _scheduledPostings.GetSystemActorIdAsync(cancellationToken), null, null);
        List<Exception> failures = [];

        foreach (long fixedDepositId in dueDepositIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await MatureFixedDepositAsync(fixedDepositId, today, systemActor, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One deposit's problem (e.g. a frozen payout account) must not stop the others.
                _dbContext.ChangeTracker.Clear();
                _logger.LogError(exception, "Fixed deposit {FixedDepositId} could not be matured.", fixedDepositId);
                failures.Add(new InvalidOperationException(
                    $"Fixed deposit {fixedDepositId} could not be matured: {exception.Message}", exception));
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"Fixed-deposit maturity failed for {failures.Count} deposit(s).", failures);
        }
    }

    // Postings need the open business date; before a manager opens the day there is nothing to do yet.
    private async Task<bool> IsBusinessDateOpenAsync(DateTime now, CancellationToken cancellationToken)
    {
        try
        {
            await _businessDates.GetPostingBusinessDateValueAsync(now, cancellationToken);
            return true;
        }
        catch (BusinessRuleException exception) when (exception.Code == MessageCode.BusinessDateTransitionConflict)
        {
            return false;
        }
    }

    // Active deposits due today or earlier, oldest first. Deposits on a frozen, suspended or closed account stay
    // put until the account is usable again: paying them out would move restricted money. The same holds for the
    // payout account of a deposit that pays something out, so such deposits do not fill every batch and starve
    // the deposits behind them.
    private async Task<IReadOnlyList<long>> GetDueFixedDepositIdsAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        string activeStatus = FixedDepositStatus.Active.ToString();

        return await _dbContext.FixedDeposits.AsNoTracking()
            .Where(deposit => deposit.Status == activeStatus && deposit.MaturityDate <= today &&
                deposit.Account != null &&
                (deposit.Account.Status == AccountStatus.Active || deposit.Account.Status == AccountStatus.Dormant) &&
                (deposit.RenewalInstruction == RenewalInstruction.PrincipalAndInterest ||
                    (deposit.PayoutAccount != null &&
                        (deposit.PayoutAccount.Status == AccountStatus.Active ||
                            deposit.PayoutAccount.Status == AccountStatus.Dormant))))
            .OrderBy(deposit => deposit.MaturityDate)
            .ThenBy(deposit => deposit.Id)
            .Select(deposit => deposit.Id)
            .Take(FixedDepositConstants.MaturityBatchSize)
            .ToListAsync(cancellationToken);
    }

    // Settles the term's interest, then pays out and/or renews the deposit in one transaction.
    private async Task MatureFixedDepositAsync(
        long fixedDepositId,
        DateOnly today,
        RequestActor actor,
        CancellationToken cancellationToken)
    {
        FixedDeposit deposit = await GetFixedDepositWithAccountsAsync(fixedDepositId, cancellationToken);
        if (deposit.Status != FixedDepositStatus.Active.ToString() || deposit.MaturityDate > today)
        {
            return;
        }

        // A term earns interest up to, but not including, its maturity date.
        await SettleInterestAsync(
            deposit,
            deposit.MaturityDate.AddDays(-1),
            today,
            FixedDepositConstants.MaturityInterestCreditDescription,
            cancellationToken);

        // The interest postings changed the account; reload everything under the locks below.
        _dbContext.ChangeTracker.Clear();
        await _ledger.RunInTransactionAsync(async () =>
        {
            await PayOutMaturedDepositAsync(deposit, actor, cancellationToken);
            return true;
        }, cancellationToken);
    }

    // Applies the renewal instruction to the settled balance. Must run inside a database transaction.
    private async Task PayOutMaturedDepositAsync(
        FixedDeposit settledDeposit,
        RequestActor actor,
        CancellationToken cancellationToken)
    {
        // Renewing principal and interest pays nothing out, so the payout account is not needed then.
        bool paysOut = settledDeposit.RenewalInstruction != RenewalInstruction.PrincipalAndInterest;
        List<long> accountIds = paysOut
            ? [settledDeposit.AccountId, settledDeposit.PayoutAccountId]
            : [settledDeposit.AccountId];
        IReadOnlyDictionary<long, Account> accounts = await _ledger.LockAccountsAsync(
            accountIds, isRefund: false, cancellationToken);

        FixedDeposit deposit = await GetTrackedFixedDepositAsync(settledDeposit.Id, cancellationToken);
        if (deposit.Status != FixedDepositStatus.Active.ToString())
        {
            // Another run or an early withdrawal handled it first.
            return;
        }

        EnsureSettingsUnchanged(settledDeposit, deposit);

        DateTime now = DateTime.UtcNow;
        Account depositAccount = accounts[deposit.AccountId];
        FixedDepositResponse depositBefore = FixedDepositService.ToResponse(deposit);
        (decimal renewalPrincipal, decimal payoutAmount) = SplitMaturityBalance(deposit, depositAccount.LedgerBalance);

        Transaction? payout = payoutAmount > 0m
            ? await PostPayoutAsync(
                TransactionType.FdMaturity,
                depositAccount,
                accounts[deposit.PayoutAccountId],
                payoutAmount,
                forfeitedInterest: 0m,
                FixedDepositConstants.MaturityPayoutDescription,
                idempotencyKey: null,
                actor,
                now,
                cancellationToken)
            : null;

        FixedDeposit? renewedDeposit = null;
        if (renewalPrincipal > 0m)
        {
            // The new term starts on the maturity date, so no day is skipped even when the payout runs late.
            deposit.Status = FixedDepositStatus.Matured.ToString();
            // It takes the rate in force on that date, not the matured term's rate.
            renewedDeposit = await _fixedDeposits.BuildRenewedDepositAsync(
                deposit, renewalPrincipal, deposit.MaturityDate, now, cancellationToken);
            await _dbContext.FixedDeposits.AddAsync(renewedDeposit, cancellationToken);
        }
        else
        {
            deposit.Status = FixedDepositStatus.Closed.ToString();
            await CloseDepositAccountAsync(depositAccount, FixedDepositConstants.MaturityClosureReason, actor, now,
                cancellationToken);
        }

        deposit.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Audit rows are written after the first save so the renewed deposit and the payout have their ids.
        AddFixedDepositAuditLog(AuditConstants.FixedDepositUpdatedAction, deposit.Id, depositBefore,
            FixedDepositService.ToResponse(deposit), actor, now);
        if (renewedDeposit is not null)
        {
            AddFixedDepositAuditLog(AuditConstants.FixedDepositCreatedAction, renewedDeposit.Id, null,
                FixedDepositService.ToResponse(renewedDeposit), actor, now);
        }

        if (payout is not null)
        {
            _ledger.AddAuditLog(AuditConstants.FixedDepositMaturedAction, payout, actor, new
            {
                FixedDepositId = deposit.Id,
                deposit.RenewalInstruction,
                PayoutAmount = payoutAmount,
                RenewedPrincipal = renewalPrincipal,
                RenewedFixedDepositId = renewedDeposit?.Id
            }, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (payout is not null)
        {
            await _ledger.ValidateTransactionAccountingEntriesAsync(payout.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Splits a matured deposit's balance into what renews and what is paid out:
    /// no renewal pays everything; principal-only renews the term's principal and pays the interest;
    /// principal-and-interest renews everything.
    /// </summary>
    private static (decimal RenewalPrincipal, decimal PayoutAmount) SplitMaturityBalance(
        FixedDeposit deposit,
        decimal balance)
    {
        switch (deposit.RenewalInstruction)
        {
            case RenewalInstruction.PrincipalOnly:
                decimal principal = Math.Min(deposit.PrincipalAmount, balance);
                return (principal, balance - principal);
            case RenewalInstruction.PrincipalAndInterest:
                return (balance, 0m);
            default:
                return (0m, balance);
        }
    }

    // ===== Early withdrawal =====

    /// <inheritdoc />
    public async Task<FixedDepositEarlyWithdrawalQuoteResponse> GetEarlyWithdrawalQuoteAsync(
        long fixedDepositId,
        CancellationToken cancellationToken)
    {
        FixedDeposit deposit = await GetFixedDepositWithAccountsAsync(fixedDepositId, cancellationToken);
        DateOnly today = BusinessTime.Today;
        ValidateCanWithdrawEarly(deposit, today);

        // Interest earned but not credited yet is credited first when the withdrawal is made, so it counts here.
        decimal pendingInterest = await CalculatePendingInterestAsync(deposit, today.AddDays(-1), cancellationToken);
        EarlyWithdrawalBreakdown breakdown = CalculateEarlyWithdrawal(
            deposit, deposit.Account!.LedgerBalance + pendingInterest, today);

        return BuildQuoteResponse(deposit, breakdown);
    }

    /// <inheritdoc />
    public async Task<TransactionResponse> WithdrawFixedDepositEarlyAsync(
        long fixedDepositId,
        EarlyWithdrawFixedDepositRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.ValidateCommonFields(request.Reason, null);

        // The deposit is closed after the first success, so a retried request must be answered before validation.
        TransactionResponse? replay = await FindEarlyWithdrawalReplayAsync(idempotencyKey, actor, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        DateTime now = DateTime.UtcNow;
        await _businessDates.GetPostingBusinessDateValueAsync(now, cancellationToken);
        DateOnly today = BusinessTime.ToBusinessDate(now);
        FixedDeposit deposit = await GetFixedDepositWithAccountsAsync(fixedDepositId, cancellationToken);
        if (deposit.Status != FixedDepositStatus.Active.ToString())
        {
            // A parallel retry with the same key may have closed the deposit since the replay check above.
            replay = await FindEarlyWithdrawalReplayAsync(idempotencyKey, actor, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        ValidateCanWithdrawEarly(deposit, today);

        // Interest accrues up to yesterday; today is the withdrawal day.
        await SettleInterestAsync(
            deposit,
            today.AddDays(-1),
            today,
            FixedDepositConstants.EarlyWithdrawalInterestCreditDescription,
            cancellationToken);
        _dbContext.ChangeTracker.Clear();

        decimal settledBalance = await _dbContext.Accounts.AsNoTracking()
            .Where(account => account.Id == deposit.AccountId)
            .Select(account => account.LedgerBalance)
            .SingleAsync(cancellationToken);
        EarlyWithdrawalBreakdown expected = CalculateEarlyWithdrawal(deposit, settledBalance, today);

        return await _ledger.RunIdempotentPostingAsync(
            idempotencyKey,
            TransactionType.FdEarlyWithdrawal,
            expected.NetPayout,
            actor.UserId,
            [deposit.AccountId, deposit.PayoutAccountId],
            key => PostEarlyWithdrawalAsync(deposit, expected, request.Reason, key, actor, today, cancellationToken),
            cancellationToken,
            debitedAccountId: deposit.AccountId);
    }

    // Pays the settled deposit out with the interest forfeit and closes it. Runs inside the idempotent posting.
    private async Task<TransactionResponse> PostEarlyWithdrawalAsync(
        FixedDeposit settledDeposit,
        EarlyWithdrawalBreakdown expected,
        string? reason,
        string? idempotencyKey,
        RequestActor actor,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<long, Account> accounts = await _ledger.LockAccountsAsync(
            [settledDeposit.AccountId, settledDeposit.PayoutAccountId], isRefund: false, cancellationToken);
        FixedDeposit deposit = await GetTrackedFixedDepositAsync(settledDeposit.Id, cancellationToken);
        if (deposit.Status != FixedDepositStatus.Active.ToString())
        {
            throw new BusinessRuleException(MessageCode.FixedDepositNotActive);
        }

        EnsureSettingsUnchanged(settledDeposit, deposit);

        // The balance can only have moved if interest was credited in between; recalculate rather than pay a stale amount.
        Account depositAccount = accounts[deposit.AccountId];
        EarlyWithdrawalBreakdown actual = CalculateEarlyWithdrawal(deposit, depositAccount.LedgerBalance, today);
        if (actual != expected)
        {
            throw new ConflictException(MessageCode.ConcurrentModification);
        }

        DateTime now = DateTime.UtcNow;
        FixedDepositResponse depositBefore = FixedDepositService.ToResponse(deposit);
        Account payoutAccount = accounts[deposit.PayoutAccountId];
        Transaction payout = await PostPayoutAsync(
            TransactionType.FdEarlyWithdrawal,
            depositAccount,
            payoutAccount,
            actual.NetPayout,
            actual.InterestForfeited,
            FixedDepositConstants.EarlyWithdrawalPayoutDescription,
            idempotencyKey,
            actor,
            now,
            cancellationToken);

        deposit.Status = FixedDepositStatus.Closed.ToString();
        deposit.UpdatedAt = now;
        await CloseDepositAccountAsync(depositAccount, FixedDepositConstants.EarlyWithdrawalClosureReason, actor, now,
            cancellationToken);
        _ledger.AddAuditLog(AuditConstants.FixedDepositEarlyWithdrawalAction, payout, actor, new
        {
            FixedDepositId = deposit.Id,
            Reason = TransactionRequestValidator.TrimToNull(reason),
            actual.Principal,
            actual.DaysHeld,
            actual.EarlyWithdrawalRate,
            actual.InterestEarned,
            actual.InterestAtEarlyWithdrawalRate,
            actual.InterestForfeited,
            actual.NetPayout
        }, now);
        AddFixedDepositAuditLog(AuditConstants.FixedDepositUpdatedAction, deposit.Id, depositBefore,
            FixedDepositService.ToResponse(deposit), actor, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return LedgerPostingService.ToResponse(payout, depositAccount.Id, payoutAccount.Id);
    }

    // Only a running deposit can be broken; on or after its maturity date the maturity run pays it out instead.
    // Both accounts must be usable, checked here so nothing is posted for a withdrawal that cannot finish.
    private static void ValidateCanWithdrawEarly(FixedDeposit deposit, DateOnly today)
    {
        if (deposit.Status != FixedDepositStatus.Active.ToString())
        {
            throw new BusinessRuleException(MessageCode.FixedDepositNotActive);
        }

        if (today >= deposit.MaturityDate)
        {
            throw new BusinessRuleException(MessageCode.FixedDepositAlreadyMatured);
        }

        if (!IsOperational(deposit.Account) || !IsOperational(deposit.PayoutAccount))
        {
            throw new BusinessRuleException(MessageCode.AccountNotOperational);
        }
    }

    // Accounts that can take part in a posting: the same rule LockAccountsAsync applies.
    private static bool IsOperational(Account? account)
    {
        return account is { Status: AccountStatus.Active or AccountStatus.Dormant };
    }

    /// <summary>
    /// Early-withdrawal amounts for a deposit whose settled balance (principal plus all interest earned this term) is
    /// <paramref name="settledBalance"/>. The customer keeps interest only up to what the term earns at the rate rule's
    /// early-withdrawal rate; never more than was actually earned.
    /// </summary>
    private static EarlyWithdrawalBreakdown CalculateEarlyWithdrawal(
        FixedDeposit deposit,
        decimal settledBalance,
        DateOnly today)
    {
        int daysHeld = today.DayNumber - deposit.StartDate.DayNumber;
        decimal earlyWithdrawalRate = deposit.InterestRateRule?.EarlyWithdrawalRate ?? 0m;
        decimal interestEarned = Math.Max(0m, settledBalance - deposit.PrincipalAmount);
        decimal interestAtEarlyRate = InterestAccumulationService.CalculateFixedDepositInterest(
            deposit.PrincipalAmount, earlyWithdrawalRate, daysHeld);
        decimal interestForfeited = Math.Max(0m, interestEarned - interestAtEarlyRate);

        return new EarlyWithdrawalBreakdown(
            deposit.PrincipalAmount,
            daysHeld,
            earlyWithdrawalRate,
            interestEarned,
            Math.Min(interestAtEarlyRate, interestEarned),
            interestForfeited,
            settledBalance - interestForfeited);
    }

    // Interest of the term that is earned through lastInterestDay but not credited to the account yet: accrued
    // amounts waiting for the quarterly credit plus days not accrued at all.
    private async Task<decimal> CalculatePendingInterestAsync(
        FixedDeposit deposit,
        DateOnly lastInterestDay,
        CancellationToken cancellationToken)
    {
        decimal accruedNotCredited = await _dbContext.InterestAccruals.AsNoTracking()
            .Where(accrual => accrual.AccountId == deposit.AccountId &&
                accrual.Status == OperationConstants.InterestAccruedStatus)
            .SumAsync(accrual => accrual.CalculatedAmount, cancellationToken);
        DateOnly firstUnaccruedDay = await GetFirstUnaccruedDayAsync(deposit, cancellationToken);
        int unaccruedDays = lastInterestDay.DayNumber - firstUnaccruedDay.DayNumber + 1;

        return accruedNotCredited + InterestAccumulationService.CalculateFixedDepositInterest(
            deposit.CurrentPrincipal, deposit.AppliedAnnualRate, unaccruedDays);
    }

    // Returns the earlier result when the idempotency key was already used for an early withdrawal by this user.
    private async Task<TransactionResponse?> FindEarlyWithdrawalReplayAsync(
        string? idempotencyKey,
        RequestActor actor,
        CancellationToken cancellationToken)
    {
        string? key = TransactionRequestValidator.TrimToNull(idempotencyKey);
        if (key is null)
        {
            return null;
        }

        var existing = await _dbContext.Transactions.AsNoTracking()
            .Where(transaction => transaction.IdempotencyKey == key)
            .Select(transaction => new { transaction.Id, transaction.TransactionType, transaction.InitiatedBy })
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (existing.TransactionType != TransactionType.FdEarlyWithdrawal || existing.InitiatedBy != actor.UserId)
        {
            throw new ConflictException(MessageCode.IdempotencyKeyReused);
        }

        return await _ledger.BuildTransactionResponseAsync(existing.Id, cancellationToken);
    }

    private static FixedDepositEarlyWithdrawalQuoteResponse BuildQuoteResponse(
        FixedDeposit deposit,
        EarlyWithdrawalBreakdown breakdown)
    {
        return new FixedDepositEarlyWithdrawalQuoteResponse(
            deposit.Id,
            deposit.Account!.AccountNo,
            deposit.PayoutAccount!.AccountNo,
            breakdown.Principal,
            deposit.StartDate,
            deposit.MaturityDate,
            breakdown.DaysHeld,
            deposit.AppliedAnnualRate,
            breakdown.EarlyWithdrawalRate,
            breakdown.InterestEarned,
            breakdown.InterestAtEarlyWithdrawalRate,
            breakdown.InterestForfeited,
            breakdown.NetPayout);
    }

    // The amounts of one early withdrawal. A record, so a stale quote is detected by simple equality.
    private sealed record EarlyWithdrawalBreakdown(
        decimal Principal,
        int DaysHeld,
        decimal EarlyWithdrawalRate,
        decimal InterestEarned,
        decimal InterestAtEarlyWithdrawalRate,
        decimal InterestForfeited,
        decimal NetPayout);

    // ===== Shared steps =====

    /// <summary>
    /// Accrues the term's interest through <paramref name="lastInterestDay"/> for days not accrued yet, then credits
    /// every accrued amount to the deposit. Both steps commit on their own and are safe to repeat.
    /// </summary>
    private async Task SettleInterestAsync(
        FixedDeposit deposit,
        DateOnly lastInterestDay,
        DateOnly postingDate,
        string creditDescription,
        CancellationToken cancellationToken)
    {
        DateOnly firstUnaccruedDay = await GetFirstUnaccruedDayAsync(deposit, cancellationToken);
        if (firstUnaccruedDay <= lastInterestDay)
        {
            await _interest.AccrueFixedDepositInterestAsync(
                deposit.Account!, deposit, firstUnaccruedDay, lastInterestDay, cancellationToken, recordOn: postingDate);
        }

        // Credit every accrual still waiting, not only those ending by lastInterestDay: a monthly run that happened
        // before a late maturity payout records the term's last days under a period ending at month end. Accruals
        // not yet recorded (an earlier attempt failed) are recorded today, since their period end may be closed.
        await _interest.PostAccruedInterestAsync(
            deposit.AccountId, postingDate, postingDate, deposit.Id, creditDescription, cancellationToken,
            recordAccrualsOn: postingDate);
    }

    // First day of the term that no interest accrual of the account covers yet.
    private async Task<DateOnly> GetFirstUnaccruedDayAsync(FixedDeposit deposit, CancellationToken cancellationToken)
    {
        DateOnly? lastAccruedDay = await _dbContext.InterestAccruals.AsNoTracking()
            .Where(accrual => accrual.AccountId == deposit.AccountId)
            .MaxAsync(accrual => (DateOnly?)accrual.PeriodEnd, cancellationToken);

        return lastAccruedDay.HasValue && lastAccruedDay.Value >= deposit.StartDate
            ? lastAccruedDay.Value.AddDays(1)
            : deposit.StartDate;
    }

    /// <summary>
    /// Posts a payout from the deposit account to the payout account. A forfeited interest amount is debited from
    /// the deposit as a second entry and credited back to Interest Expense (stored as the transaction's fee).
    /// The accounts must be locked; fixed-deposit debit rules are bypassed on purpose.
    /// A scheduled maturity payout gets the scheduled transaction-number prefix and does not count as customer
    /// activity (it neither refreshes last activity nor reactivates a dormant account).
    /// </summary>
    private async Task<Transaction> PostPayoutAsync(
        TransactionType transactionType,
        Account depositAccount,
        Account payoutAccount,
        decimal amount,
        decimal forfeitedInterest,
        string description,
        string? idempotencyKey,
        RequestActor actor,
        DateTime now,
        CancellationToken cancellationToken)
    {
        LedgerPostingService.EnsureCanReceiveTransfer(payoutAccount);

        // Maturity is posted by the scheduled job; early withdrawal is a customer request at the counter.
        bool isScheduled = transactionType == TransactionType.FdMaturity;
        Transaction transaction = await _ledger.CreateTransactionAsync(
            transactionType,
            TransactionStatus.Completed,
            amount,
            description,
            null,
            idempotencyKey,
            actor.UserId,
            now,
            BusinessTime.ToBusinessDate(now),
            cancellationToken,
            isScheduled);
        await _ledger.PostCustomerEntryAsync(transaction, depositAccount, EntryType.Debit, now, cancellationToken,
            countsAsCustomerActivity: !isScheduled);
        await _ledger.PostCustomerEntryAsync(transaction, payoutAccount, EntryType.Credit, now, cancellationToken,
            countsAsCustomerActivity: !isScheduled);

        if (forfeitedInterest > 0m)
        {
            transaction.FeeAmount = forfeitedInterest;
            await _ledger.PostCustomerEntryAsync(transaction, depositAccount, EntryType.Debit, now, cancellationToken,
                forfeitedInterest, FixedDepositConstants.ForfeitedInterestEntryDescription,
                countsAsCustomerActivity: !isScheduled);
            await _ledger.PostGlEntryAsync(transaction, AccountingConstants.InterestExpenseGlCode, EntryType.Credit,
                null, now, cancellationToken, forfeitedInterest);
        }

        await _dbContext.Transactions.AddAsync(transaction, cancellationToken);
        return transaction;
    }

    // A fully paid-out deposit account has nothing left to hold, so it is closed with a status-history entry.
    private async Task CloseDepositAccountAsync(
        Account account,
        string reason,
        RequestActor actor,
        DateTime now,
        CancellationToken cancellationToken)
    {
        AccountStatus oldStatus = account.Status;
        account.Status = AccountStatus.Closed;
        account.ClosedAt = now;
        account.UpdatedAt = now;
        await _dbContext.AccountStatusHistories.AddAsync(new AccountStatusHistory
        {
            Account = account,
            OldStatus = oldStatus,
            NewStatus = AccountStatus.Closed,
            Reason = reason,
            ChangedBy = actor.UserId,
            ChangedAt = now
        }, cancellationToken);
        AddAuditLog(AuditConstants.AccountStatusUpdatedAction, nameof(Account), account.Id,
            new { Status = oldStatus }, new { Status = AccountStatus.Closed, Reason = reason }, actor, now);
    }

    // The payout uses the payout account and instruction seen when interest was settled; a change in between means
    // the caller acted on stale data.
    private static void EnsureSettingsUnchanged(FixedDeposit settledDeposit, FixedDeposit currentDeposit)
    {
        if (currentDeposit.PayoutAccountId != settledDeposit.PayoutAccountId ||
            currentDeposit.RenewalInstruction != settledDeposit.RenewalInstruction)
        {
            throw new ConflictException(MessageCode.ConcurrentModification);
        }
    }

    // Loads a deposit with its accounts and rate rule for reading.
    private async Task<FixedDeposit> GetFixedDepositWithAccountsAsync(
        long fixedDepositId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.FixedDeposits.AsNoTracking()
            .Include(deposit => deposit.Account)
            .Include(deposit => deposit.PayoutAccount)
            .Include(deposit => deposit.InterestRateRule)
            .SingleOrDefaultAsync(deposit => deposit.Id == fixedDepositId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.FixedDepositNotFound);
    }

    // Loads a deposit for changing, after its account is locked.
    private async Task<FixedDeposit> GetTrackedFixedDepositAsync(
        long fixedDepositId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.FixedDeposits
            .Include(deposit => deposit.InterestRateRule)
            .SingleOrDefaultAsync(deposit => deposit.Id == fixedDepositId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.FixedDepositNotFound);
    }

    private void AddFixedDepositAuditLog(
        string action,
        long fixedDepositId,
        FixedDepositResponse? oldValues,
        FixedDepositResponse newValues,
        RequestActor actor,
        DateTime now)
    {
        AddAuditLog(action, nameof(FixedDeposit), fixedDepositId, oldValues, newValues, actor, now);
    }

    // Writes an audit row for the given actor. The shared audit service reads the signed-in user, which a scheduled
    // run does not have, so payouts record the actor explicitly.
    private void AddAuditLog(
        string action,
        string entityType,
        long entityId,
        object? oldValues,
        object newValues,
        RequestActor actor,
        DateTime now)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(CultureInfo.InvariantCulture),
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(newValues),
            IpAddress = actor.IpAddress,
            DeviceInfo = actor.DeviceInfo,
            CreatedAt = now
        });
    }
}
