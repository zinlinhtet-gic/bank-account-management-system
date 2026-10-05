using bams.server.Models.Accounting;
using bams.server.Models.Accounts;
using bams.server.Models.Audit;
using bams.server.Models.Customers;
using bams.server.Models.External;
using bams.server.Models.InterestFees;
using bams.server.Models.Organization;
using bams.server.Models.Jobs;
using bams.server.Models;
using bams.server.Models.Products;
using bams.server.Models.Security;
using bams.server.Models.Transactions;
using bams.server.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace bams.server.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    // Security
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // Organization
    public DbSet<Branch> Branches => Set<Branch>();

    // Customer / KYC
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerDocument> CustomerDocuments => Set<CustomerDocument>();

    // Product configuration
    public DbSet<AccountType> AccountTypes => Set<AccountType>();
    public DbSet<AccountTypeRequiredDocument> AccountTypeRequiredDocuments => Set<AccountTypeRequiredDocument>();
    public DbSet<InterestRateRule> InterestRateRules => Set<InterestRateRule>();
    public DbSet<FeeRule> FeeRules => Set<FeeRule>();

    // Customer accounts
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountHolder> AccountHolders => Set<AccountHolder>();
    public DbSet<AccountReferer> AccountReferers => Set<AccountReferer>();
    public DbSet<FixedDeposit> FixedDeposits => Set<FixedDeposit>();
    public DbSet<AccountStatusHistory> AccountStatusHistories => Set<AccountStatusHistory>();
    public DbSet<AccountNumberGeneration> AccountNumberGenerations => Set<AccountNumberGeneration>();
    public DbSet<AccountDocument> AccountDocuments => Set<AccountDocument>();

    // Transactions
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionEntry> TransactionEntries => Set<TransactionEntry>();
    public DbSet<AccountTransaction> AccountTransactions => Set<AccountTransaction>();
    public DbSet<NrcCashTransferDetail> NrcCashTransferDetails => Set<NrcCashTransferDetail>();
    public DbSet<InterbankTransferDetail> InterbankTransferDetails => Set<InterbankTransferDetail>();

    // Interest / fees
    public DbSet<InterestAccrual> InterestAccruals => Set<InterestAccrual>();
    public DbSet<FeeAccrual> FeeAccruals => Set<FeeAccrual>();

    // Scheduled jobs
    public DbSet<ScheduledJob> ScheduledJobs => Set<ScheduledJob>();
    public DbSet<ScheduledJobExecution> ScheduledJobExecutions => Set<ScheduledJobExecution>();
    public DbSet<ScheduledJobRetryRequest> ScheduledJobRetryRequests => Set<ScheduledJobRetryRequest>();

    // External banking
    public DbSet<OtherBank> OtherBanks => Set<OtherBank>();
    public DbSet<ReconciliationBatch> ReconciliationBatches => Set<ReconciliationBatch>();
    public DbSet<ReconciliationItem> ReconciliationItems => Set<ReconciliationItem>();

    // Accounting / reporting
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    public DbSet<MonthlySummary> MonthlySummaries => Set<MonthlySummary>();
    public DbSet<BusinessDate> BusinessDates => Set<BusinessDate>();
    public DbSet<AccountReconciliationRun> AccountReconciliationRuns => Set<AccountReconciliationRun>();
    public DbSet<AccountReconciliationResult> AccountReconciliationResults => Set<AccountReconciliationResult>();
    public DbSet<ReconciliationException> ReconciliationExceptions => Set<ReconciliationException>();
    public DbSet<ReconciliationExceptionHistory> ReconciliationExceptionHistories => Set<ReconciliationExceptionHistory>();
    public DbSet<CashPositionSession> CashPositionSessions => Set<CashPositionSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<CashCount> CashCounts => Set<CashCount>();
    public DbSet<CashHandoff> CashHandoffs => Set<CashHandoff>();
    public DbSet<CashHandoffHistory> CashHandoffHistories => Set<CashHandoffHistory>();
    public DbSet<EndOfDayRun> EndOfDayRuns => Set<EndOfDayRun>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // MySql.EntityFrameworkCore's ADO.NET reader cannot materialize DateOnly directly
        // (it throws InvalidCastException reading a `date` column), so every DateOnly
        // property round-trips through DateTime instead, while keeping the `date` column type.
        configurationBuilder.Properties<DateOnly>()
            .HaveConversion<DateOnlyConverter>()
            .HaveColumnType("date");

        configurationBuilder.Properties<DateOnly?>()
            .HaveConversion<NullableDateOnlyConverter>()
            .HaveColumnType("date");
    }

    private const string DateColumnType = "date";

    // Converts DateOnly to midnight DateTime for the database and back.
    private sealed class DateOnlyToDateTimeConverter()
        : ValueConverter<DateOnly, DateTime>(
            date => date.ToDateTime(TimeOnly.MinValue),
            dateTime => DateOnly.FromDateTime(dateTime));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    // Converts DateOnly <-> DateTime so the MySQL provider never has to read a `date`
    // column as DateOnly directly.
    private sealed class DateOnlyConverter : ValueConverter<DateOnly, DateTime>
    {
        public DateOnlyConverter()
            : base(
                dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
                dateTime => DateOnly.FromDateTime(dateTime))
        {
        }
    }

    // Nullable counterpart of DateOnlyConverter for optional DateOnly columns.
    private sealed class NullableDateOnlyConverter : ValueConverter<DateOnly?, DateTime?>
    {
        public NullableDateOnlyConverter()
            : base(
                dateOnly => dateOnly.HasValue ? dateOnly.Value.ToDateTime(TimeOnly.MinValue) : null,
                dateTime => dateTime.HasValue ? DateOnly.FromDateTime(dateTime.Value) : null)
        {
        }
    }
}
