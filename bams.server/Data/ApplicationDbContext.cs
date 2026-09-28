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

    // External banking
    public DbSet<OtherBank> OtherBanks => Set<OtherBank>();
    public DbSet<ReconciliationBatch> ReconciliationBatches => Set<ReconciliationBatch>();
    public DbSet<ReconciliationItem> ReconciliationItems => Set<ReconciliationItem>();

    // Accounting / reporting
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    public DbSet<MonthlySummary> MonthlySummaries => Set<MonthlySummary>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // MySql.EntityFrameworkCore writes DateOnly but cannot read it back (InvalidCastException from DateTime),
        // so DateOnly values travel as DateTime while the column stays a plain SQL date.
        configurationBuilder.Properties<DateOnly>()
            .HaveConversion<DateOnlyToDateTimeConverter>()
            .HaveColumnType(DateColumnType);
        // MySQL materializes DATE columns as DateTime, so convert them explicitly to DateOnly.
        configurationBuilder.Properties<DateOnly>()
            .HaveConversion<DateOnlyValueConverter>()
            .HaveColumnType("date");
        configurationBuilder.Properties<DateOnly?>()
            .HaveConversion<NullableDateOnlyValueConverter>()
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

    /// <summary>
    /// Saves changes after advancing application-managed concurrency versions.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AdvanceConcurrencyVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// Asynchronously saves changes after advancing application-managed concurrency versions.
    /// </summary>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AdvanceConcurrencyVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Advances only modified entities so newly inserted rows retain their initial version of one.
    private void AdvanceConcurrencyVersions()
    {
        ChangeTracker.DetectChanges();

        foreach (var entry in ChangeTracker.Entries<IConcurrencyTracked>()
                     .Where(entry => entry.State == EntityState.Modified))
        {
            var versionProperty = entry.Property(entity => entity.Version);
            versionProperty.CurrentValue = checked(versionProperty.OriginalValue + 1);
        }
    }
}
