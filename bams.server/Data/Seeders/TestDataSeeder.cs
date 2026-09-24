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
            "Mandalay"),
        new(
            "CUST-TEST-003",
            "Thandar Hlaing Test",
            new DateOnly(1988, 2, 11),
            "5/AAA(N)135791",
            "09333333333",
            "thandar.hlaing.test@example.com",
            "3 Test Street",
            "Naypyidaw"),
        new(
            "CUST-TEST-004",
            "Kyaw Zin Test",
            new DateOnly(1992, 7, 8),
            "6/BBB(N)246802",
            "09444444444",
            "kyaw.zin.test@example.com",
            "4 Test Street",
            "Bago"),
        new(
            "CUST-TEST-005",
            "Hnin Pwint Test",
            new DateOnly(1996, 11, 23),
            "7/CCC(N)357913",
            "09555555555",
            "hnin.pwint.test@example.com",
            "5 Test Street",
            "Taunggyi"),
        new(
            "CUST-TEST-006",
            "Min Khant Test",
            new DateOnly(1985, 4, 17),
            "8/DDD(N)468024",
            "09666666666",
            "min.khant.test@example.com",
            "6 Test Street",
            "Mawlamyine"),
        new(
            "CUST-TEST-007",
            "Ei Ei Phyu Test",
            new DateOnly(1999, 1, 30),
            "9/EEE(N)579135",
            "09777777777",
            "ei.ei.phyu.test@example.com",
            "7 Test Street",
            "Pathein")
    ];

    private static readonly AccountSeed[] AccountSeeds =
    [
        new("0199000000010001", "CUST-TEST-001", "CURRENT", 100_000m),
        new("0299000000010002", "CUST-TEST-001", "NORMAL_SAVING", 200_000m),
        new("0399000000010003", "CUST-TEST-001", "SPECIAL_SAVING", 300_000m),
        new("0199000000020001", "CUST-TEST-002", "CURRENT", 150_000m),
        new("0299000000020002", "CUST-TEST-002", "NORMAL_SAVING", 250_000m),
        new("0399000000020003", "CUST-TEST-002", "SPECIAL_SAVING", 350_000m),
        new("0199000000030001", "CUST-TEST-003", "CURRENT", 175_000m),
        new("0299000000030002", "CUST-TEST-003", "NORMAL_SAVING", 275_000m),
        new("0199000000040001", "CUST-TEST-004", "CURRENT", 200_000m),
        new("0299000000040002", "CUST-TEST-004", "NORMAL_SAVING", 300_000m),
        new("0199000000050001", "CUST-TEST-005", "CURRENT", 225_000m),
        new("0299000000050002", "CUST-TEST-005", "NORMAL_SAVING", 325_000m),
        new("0199000000060001", "CUST-TEST-006", "CURRENT", 250_000m),
        new("0299000000060002", "CUST-TEST-006", "NORMAL_SAVING", 350_000m),
        new("0199000000070001", "CUST-TEST-007", "CURRENT", 275_000m),
        new("0299000000070002", "CUST-TEST-007", "NORMAL_SAVING", 375_000m)
    ];

    private readonly ApplicationDbContext _dbContext;

    public TestDataSeeder(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Idempotently creates configured development customers and their test accounts.
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
