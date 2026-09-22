using bams.server.Constants;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Customers;
using bams.server.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

/// <summary>
/// Creates deterministic development customers and their individual test accounts.
/// </summary>
public sealed class TestDataSeeder
{
    private const string ActiveStatus = "Active";

    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly CustomerSeed[] CustomerSeeds =
    [
        new(
            "CUST-TEST-001",
            "Aung Min Test",
            new DateOnly(1990, 5, 15),
            "12/ABC(N)123456",
            "09111111111",
            "aung.min.test@example.com",
            "1 Test Street",
            "Yangon"),
        new(
            "CUST-TEST-002",
            "Su Mon Test",
            new DateOnly(1994, 9, 20),
            "12/DEF(N)654321",
            "09222222222",
            "su.mon.test@example.com",
            "2 Test Street",
            "Mandalay")
    ];

    private static readonly AccountSeed[] AccountSeeds =
    [
        new("0199000000010001", "CUST-TEST-001", "CURRENT", 100_000m),
        new("0299000000010002", "CUST-TEST-001", "NORMAL_SAVING", 200_000m),
        new("0399000000010003", "CUST-TEST-001", "SPECIAL_SAVING", 300_000m),
        new("0199000000020001", "CUST-TEST-002", "CURRENT", 150_000m),
        new("0299000000020002", "CUST-TEST-002", "NORMAL_SAVING", 250_000m),
        new("0399000000020003", "CUST-TEST-002", "SPECIAL_SAVING", 350_000m)
    ];

    private readonly ApplicationDbContext _dbContext;

    public TestDataSeeder(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Idempotently creates two development customers with three accounts each.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await InsertMissingCustomersAsync(cancellationToken);

        var customerNumbers = CustomerSeeds.Select(seed => seed.CustomerNo).ToArray();
        var customersByNumber = await _dbContext.Customers
            .Where(customer => customerNumbers.Contains(customer.CustomerNo))
            .ToDictionaryAsync(customer => customer.CustomerNo, cancellationToken);

        var productCodes = AccountSeeds.Select(seed => seed.AccountTypeCode).Distinct().ToArray();
        var accountTypesByCode = await _dbContext.AccountTypes
            .Where(accountType => productCodes.Contains(accountType.Code))
            .ToDictionaryAsync(accountType => accountType.Code, cancellationToken);

        await InsertMissingAccountsAndHoldersAsync(
            customersByNumber,
            accountTypesByCode,
            cancellationToken);
    }

    // Inserts customers by stable customer number so repeated startup seeding is safe.
    private async Task InsertMissingCustomersAsync(CancellationToken cancellationToken)
    {
        var customerNumbers = CustomerSeeds.Select(seed => seed.CustomerNo).ToArray();
        var existingCustomerNumbers = await _dbContext.Customers
            .Where(customer => customerNumbers.Contains(customer.CustomerNo))
            .Select(customer => customer.CustomerNo)
            .ToHashSetAsync(cancellationToken);

        var missingCustomers = CustomerSeeds
            .Where(seed => !existingCustomerNumbers.Contains(seed.CustomerNo))
            .Select(CreateCustomer)
            .ToList();

        if (missingCustomers.Count == 0)
        {
            return;
        }

        await _dbContext.Customers.AddRangeAsync(missingCustomers, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Inserts missing accounts and repairs a missing seeded holder relationship when necessary.
    private async Task InsertMissingAccountsAndHoldersAsync(
        IReadOnlyDictionary<string, Customer> customersByNumber,
        IReadOnlyDictionary<string, AccountType> accountTypesByCode,
        CancellationToken cancellationToken)
    {
        var accountNumbers = AccountSeeds.Select(seed => seed.AccountNo).ToArray();
        var existingAccountsByNumber = await _dbContext.Accounts
            .Include(account => account.AccountHolders)
            .Where(account => accountNumbers.Contains(account.AccountNo))
            .ToDictionaryAsync(account => account.AccountNo, cancellationToken);

        foreach (var seed in AccountSeeds)
        {
            var customer = customersByNumber[seed.CustomerNo];

            if (!existingAccountsByNumber.TryGetValue(seed.AccountNo, out var account))
            {
                account = CreateAccount(seed, accountTypesByCode[seed.AccountTypeCode].Id);
                account.AccountHolders.Add(CreateAccountHolder(customer.Id));
                await _dbContext.Accounts.AddAsync(account, cancellationToken);
                continue;
            }

            if (account.AccountHolders.All(holder => holder.CustomerId != customer.Id))
            {
                account.AccountHolders.Add(CreateAccountHolder(customer.Id));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Maps a deterministic customer definition into a verified active customer entity.
    private static Customer CreateCustomer(CustomerSeed seed)
    {
        return new Customer
        {
            CustomerNo = seed.CustomerNo,
            CustomerType = CustomerType.Citizen,
            FullName = seed.FullName,
            DateOfBirth = seed.DateOfBirth,
            Nationality = "Myanmar",
            NrcNumber = seed.NrcNumber,
            Phone = seed.Phone,
            Occupation = "Test Customer",
            AddressLine1 = seed.AddressLine1,
            City = seed.City,
            Country = "Myanmar",
            Email = seed.Email,
            RiskLevel = RiskLevel.Low,
            KycStatus = KycStatus.Verified,
            Status = ActiveStatus,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };
    }

    // Maps a deterministic account definition into an active individual account entity.
    private static Account CreateAccount(AccountSeed seed, long accountTypeId)
    {
        return new Account
        {
            AccountNo = seed.AccountNo,
            AccountTypeId = accountTypeId,
            Status = AccountStatus.Active,
            OpenedAt = SeededAt,
            ActiveAt = SeededAt,
            AvailableBalance = seed.OpeningBalance,
            LedgerBalance = seed.OpeningBalance,
            LastActivityAt = SeededAt,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };
    }

    // Creates the sole primary holder relationship for an individual account.
    private static AccountHolder CreateAccountHolder(long customerId)
    {
        return new AccountHolder
        {
            CustomerId = customerId,
            OwnershipType = OwnershipType.Individual,
            OwnershipPercentage = AccountConstants.FullOwnershipPercentage,
            IsPrimary = true,
            Status = ActiveStatus,
            CreatedAt = SeededAt
        };
    }

    private sealed record CustomerSeed(
        string CustomerNo,
        string FullName,
        DateOnly DateOfBirth,
        string NrcNumber,
        string Phone,
        string Email,
        string AddressLine1,
        string City);

    private sealed record AccountSeed(
        string AccountNo,
        string CustomerNo,
        string AccountTypeCode,
        decimal OpeningBalance);
}
