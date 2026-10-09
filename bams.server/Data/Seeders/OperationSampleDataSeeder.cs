using System.Globalization;
using bams.server.Constants;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Products;
using bams.server.Services;
using bams.server.Services.Jobs;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

public sealed class OperationSampleDataSeeder
{
    /// Configuration flag (appsettings.Development.json) that turns this seeder on.
    public const string EnabledSettingKey = "SampleData:SeedOperations";

    private const string ActiveStatus = "Active";

    // Marker digits used by every seeded test account number (see TestDataSeeder), e.g. 05 99 0000 0001 0004.
    private const string TestAccountNumberMarker = "990000";
    private const int CustomerSequenceWidth = 4;
    private const int AccountSequenceWidth = 4;

    // Prefix of the seeded test customer numbers, followed by a 3-digit index (CUST-TEST-001).
    private const string TestCustomerNumberPrefix = "CUST-TEST-";

    // The jobs run at 00:00 Myanmar time on this day; each run handles the previous calendar month.
    private const int JobRunDayOfMonth = 5;

    // First replayed run: covers January 2026, the month the test accounts were opened.
    private static readonly DateOnly FirstReplayRunDate = new(2026, 2, JobRunDayOfMonth);

    // Seeded test accounts are opened on this date (same as TestDataSeeder).
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Fixed deposits spread across the maturity states the Fixed Deposit Maturity page shows:
    // due within days (highlighted), due later, long-running, already matured, and closed.
    private static readonly FixedDepositSeed[] FixedDepositSeeds =
    [
        new(1, 4, "HUNDRED_DAYS_DEPOSIT", 100, null, new DateOnly(2026, 6, 27), 1_000_000m, RenewalInstruction.PrincipalOnly, FixedDepositStatus.Active),
        new(2, 4, "NORMAL_DEPOSIT", null, 3, new DateOnly(2026, 7, 10), 2_000_000m, RenewalInstruction.PrincipalAndInterest, FixedDepositStatus.Active),
        new(3, 3, "SPECIAL_DEPOSIT", null, 6, new DateOnly(2026, 5, 1), 5_000_000m, RenewalInstruction.NoRenewal, FixedDepositStatus.Active),
        new(4, 3, "NORMAL_DEPOSIT", null, 12, new DateOnly(2026, 2, 1), 3_000_000m, RenewalInstruction.PrincipalOnly, FixedDepositStatus.Active),
        new(5, 3, "NORMAL_DEPOSIT", null, 1, new DateOnly(2026, 3, 1), 1_500_000m, RenewalInstruction.NoRenewal, FixedDepositStatus.Matured),
        new(6, 3, "SPECIAL_DEPOSIT", null, 3, new DateOnly(2026, 4, 15), 2_500_000m, RenewalInstruction.NoRenewal, FixedDepositStatus.Closed),
        new(7, 3, "HUNDRED_DAYS_DEPOSIT", 100, null, new DateOnly(2026, 6, 30), 800_000m, RenewalInstruction.PrincipalOnly, FixedDepositStatus.Active)
    ];

    // A dormant saving account, so the Fees page also shows monthly dormant penalties.
    private static readonly DormantAccountSeed DormantSaving =
        new(5, 4, "NORMAL_SAVING", 150_000m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    private readonly ApplicationDbContext _dbContext;
    private readonly AccountMaintenanceService _maintenanceService;
    private readonly InterestAccumulationService _interestService;
    private readonly ILogger<OperationSampleDataSeeder> _logger;

    public OperationSampleDataSeeder(
        ApplicationDbContext dbContext,
        AccountMaintenanceService maintenanceService,
        InterestAccumulationService interestService,
        ILogger<OperationSampleDataSeeder> logger)
    {
        _dbContext = dbContext;
        _maintenanceService = maintenanceService;
        _interestService = interestService;
        _logger = logger;
    }

    /// Adds the sample accounts, replays the monthly jobs for every past run date, then applies maturity statuses.
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await InsertMissingSampleAccountsAsync(cancellationToken);
        await ReplayMonthlyJobsAsync(cancellationToken);
        await ApplyMaturityStatusesAsync(cancellationToken);
    }

    // Inserts the fixed-deposit accounts with their deposits, and the dormant saving account, when missing.
    private async Task InsertMissingSampleAccountsAsync(CancellationToken cancellationToken)
    {
        var customersByNumber = await _dbContext.Customers
            .Where(customer => customer.CustomerNo.StartsWith(TestCustomerNumberPrefix))
            .ToDictionaryAsync(customer => customer.CustomerNo, cancellationToken);
        var accountTypesByCode = await _dbContext.AccountTypes
            .ToDictionaryAsync(accountType => accountType.Code, cancellationToken);

        foreach (var seed in FixedDepositSeeds)
        {
            if (!customersByNumber.TryGetValue(FormatCustomerNo(seed.CustomerIndex), out var customer))
            {
                continue;
            }

            var accountType = accountTypesByCode[seed.AccountTypeCode];
            var accountNo = FormatAccountNo(accountType.Id, seed.CustomerIndex, seed.AccountSequence);
            if (await _dbContext.Accounts.AnyAsync(account => account.AccountNo == accountNo, cancellationToken))
            {
                continue;
            }

            await InsertFixedDepositAccountAsync(seed, accountNo, accountType.Id, customer.Id, cancellationToken);
        }

        await InsertDormantSavingAccountAsync(customersByNumber, accountTypesByCode, cancellationToken);
    }

    // Creates one fixed-deposit account funded with its principal, its primary holder and the deposit record.
    private async Task InsertFixedDepositAccountAsync(
        FixedDepositSeed seed,
        string accountNo,
        long accountTypeId,
        long customerId,
        CancellationToken cancellationToken)
    {
        var rule = await _dbContext.InterestRateRules.SingleAsync(item =>
                item.AccountTypeId == accountTypeId && item.Status == ActiveStatus &&
                item.TermDays == seed.TermDays && item.TermMonths == seed.TermMonths &&
                item.BalanceMin == null && item.BalanceMax == null,
            cancellationToken);
        var payoutAccountId = await GetCustomerPayoutAccountIdAsync(customerId, cancellationToken);
        var openedAt = seed.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var account = CreateAccount(accountNo, accountTypeId, seed.Principal, AccountStatus.Active, openedAt);
        account.AccountHolders.Add(CreatePrimaryHolder(customerId, openedAt));
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Deposits start Active so the replayed interest job accrues them; maturity statuses are applied afterwards.
        _dbContext.FixedDeposits.Add(new FixedDeposit
        {
            AccountId = account.Id,
            PrincipalAmount = seed.Principal,
            OriginalPrincipal = seed.Principal,
            CurrentPrincipal = seed.Principal,
            InterestRateRuleId = rule.Id,
            AppliedAnnualRate = rule.AnnualRate,
            StartDate = seed.StartDate,
            MaturityDate = FixedDepositService.CalculateMaturityDate(seed.StartDate, seed.TermDays, seed.TermMonths),
            TermDays = seed.TermDays,
            TermMonths = seed.TermMonths,
            RenewalInstruction = seed.RenewalInstruction,
            PayoutAccountId = payoutAccountId,
            Status = FixedDepositStatus.Active.ToString(),
            CalculateFromCurrent = false,
            CreatedAt = openedAt,
            UpdatedAt = openedAt
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Creates the dormant saving account when it is missing.
    private async Task InsertDormantSavingAccountAsync(
        IReadOnlyDictionary<string, Models.Customers.Customer> customersByNumber,
        IReadOnlyDictionary<string, AccountType> accountTypesByCode,
        CancellationToken cancellationToken)
    {
        if (!customersByNumber.TryGetValue(FormatCustomerNo(DormantSaving.CustomerIndex), out var customer))
        {
            return;
        }

        var accountType = accountTypesByCode[DormantSaving.AccountTypeCode];
        var accountNo = FormatAccountNo(accountType.Id, DormantSaving.CustomerIndex, DormantSaving.AccountSequence);
        if (await _dbContext.Accounts.AnyAsync(account => account.AccountNo == accountNo, cancellationToken))
        {
            return;
        }

        var account = CreateAccount(accountNo, accountType.Id, DormantSaving.Balance, AccountStatus.Dormant, SeededAt);
        account.DormantAt = DormantSaving.DormantAt;
        account.AccountHolders.Add(CreatePrimaryHolder(customer.Id, SeededAt));
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Runs maintenance then interest (the production order) for each monthly run date up to today.
    private async Task ReplayMonthlyJobsAsync(CancellationToken cancellationToken)
    {
        var today = BusinessTime.Today;
        for (var runDate = FirstReplayRunDate; runDate <= today; runDate = runDate.AddMonths(1))
        {
            var context = new ScheduledJobExecutionContext(
                JobId: 0,
                ExecutionId: 0,
                ScheduledForUtc: BusinessTime.GetUtcRange(runDate).StartUtc,
                AttemptNumber: 1);

            _logger.LogInformation("Replaying monthly jobs for sample run date {RunDate}.",
                runDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            await ReplayJobAsync(() => _maintenanceService.ExecuteAsync(context, cancellationToken), runDate);
            await ReplayJobAsync(() => _interestService.ExecuteAsync(context, cancellationToken), runDate);
            _dbContext.ChangeTracker.Clear();
        }
    }

    // A job reports per-account failures together after finishing every other account (each account commits on
    // its own). Sample data must not stop the server from starting, so the failure is logged and the replay goes on;
    // the next startup retries the failed accounts because the jobs skip periods that already exist.
    private async Task ReplayJobAsync(Func<Task> runJobAsync, DateOnly runDate)
    {
        try
        {
            await runJobAsync();
        }
        catch (AggregateException exception)
        {
            _logger.LogError(exception, "Sample job replay for {RunDate} failed for some accounts.",
                runDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }

    // Moves sample deposits past their maturity date to the status their seed describes (Matured or Closed).
    private async Task ApplyMaturityStatusesAsync(CancellationToken cancellationToken)
    {
        var accountTypeIdsByCode = await _dbContext.AccountTypes
            .ToDictionaryAsync(accountType => accountType.Code, accountType => accountType.Id, cancellationToken);
        var today = BusinessTime.Today;

        foreach (var seed in FixedDepositSeeds.Where(item => item.FinalStatus != FixedDepositStatus.Active))
        {
            var accountNo = FormatAccountNo(accountTypeIdsByCode[seed.AccountTypeCode], seed.CustomerIndex,
                seed.AccountSequence);
            var deposit = await _dbContext.FixedDeposits
                .SingleOrDefaultAsync(item => item.Account!.AccountNo == accountNo, cancellationToken);
            var finalStatus = seed.FinalStatus.ToString();
            if (deposit is null || deposit.MaturityDate > today || deposit.Status == finalStatus)
            {
                continue;
            }

            deposit.Status = finalStatus;
            deposit.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // A fixed deposit pays out to one of the customer's own current or saving accounts (the first seeded one).
    private async Task<long> GetCustomerPayoutAccountIdAsync(long customerId, CancellationToken cancellationToken)
    {
        return await _dbContext.AccountHolders
            .Where(holder => holder.CustomerId == customerId && holder.IsPrimary &&
                holder.Account!.Status == AccountStatus.Active &&
                holder.Account.AccountType!.Category != AccountTypeCategory.FIXED)
            .OrderBy(holder => holder.AccountId)
            .Select(holder => holder.AccountId)
            .FirstAsync(cancellationToken);
    }

    // Builds a 16-digit test account number: account type id, marker, customer index, account sequence.
    private static string FormatAccountNo(long accountTypeId, int customerIndex, int accountSequence)
    {
        return string.Concat(
            accountTypeId.ToString($"D{AccountConstants.AccountTypeIdentifierWidth}", CultureInfo.InvariantCulture),
            TestAccountNumberMarker,
            customerIndex.ToString($"D{CustomerSequenceWidth}", CultureInfo.InvariantCulture),
            accountSequence.ToString($"D{AccountSequenceWidth}", CultureInfo.InvariantCulture));
    }

    // CUST-TEST-001 style customer number used by TestDataSeeder.
    private static string FormatCustomerNo(int customerIndex)
    {
        return $"{TestCustomerNumberPrefix}{customerIndex:D3}";
    }

    // Maps a sample definition into an individual account with its opening balance.
    private static Account CreateAccount(
        string accountNo,
        long accountTypeId,
        decimal balance,
        AccountStatus status,
        DateTime openedAt)
    {
        return new Account
        {
            AccountNo = accountNo,
            AccountTypeId = accountTypeId,
            Status = status,
            OpenedAt = openedAt,
            ActiveAt = openedAt,
            AvailableBalance = balance,
            LedgerBalance = balance,
            LastActivityAt = openedAt,
            CreatedAt = openedAt,
            UpdatedAt = openedAt
        };
    }

    // Creates the sole primary holder relationship for an individual account.
    private static AccountHolder CreatePrimaryHolder(long customerId, DateTime createdAt)
    {
        return new AccountHolder
        {
            CustomerId = customerId,
            OwnershipType = OwnershipType.Individual,
            OwnershipPercentage = AccountConstants.FullOwnershipPercentage,
            IsPrimary = true,
            Status = ActiveStatus,
            CreatedAt = createdAt
        };
    }

    private sealed record FixedDepositSeed(
        int CustomerIndex,
        int AccountSequence,
        string AccountTypeCode,
        int? TermDays,
        int? TermMonths,
        DateOnly StartDate,
        decimal Principal,
        RenewalInstruction RenewalInstruction,
        FixedDepositStatus FinalStatus);

    private sealed record DormantAccountSeed(
        int CustomerIndex,
        int AccountSequence,
        string AccountTypeCode,
        decimal Balance,
        DateTime DormantAt);
}
