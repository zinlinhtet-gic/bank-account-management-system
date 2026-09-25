using bams.server.Data;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Controllers;

[ApiController]
[Route("api/dev/audit-test-data")]
public sealed class AuditTestDataController : ControllerBase
{
    private const string TestCashGlCode = "TEST-CASH";
    private const string TestDepositGlCode = "TEST-DEPOSIT";
    private const string ActiveStatus = "Active";

    private const decimal BalancedDebitAmount = 1000m;
    private const decimal BalancedCreditAmount = 1000m;
    private const decimal UnbalancedCreditAmount = 900m;

    private static readonly DateOnly BalancedAuditDate =
        new(2026, 9, 25);

    private static readonly DateOnly UnbalancedAuditDate =
        new(2026, 9, 26);

    private readonly ApplicationDbContext _dbContext;

    public AuditTestDataController(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Creates the minimum GL accounts required for audit test transactions.
    [HttpPost("gl-accounts")]
    public async Task<IActionResult> CreateAuditGlAccountsAsync(
        CancellationToken cancellationToken)
    {
        var existingCodes = await _dbContext.GlAccounts
            .AsNoTracking()
            .Where(account =>
                account.Code == TestCashGlCode ||
                account.Code == TestDepositGlCode)
            .Select(account => account.Code)
            .ToListAsync(cancellationToken);

        // Create the debit-side test GL account when it does not already exist.
        if (!existingCodes.Contains(TestCashGlCode))
        {
            _dbContext.GlAccounts.Add(
                new GlAccount
                {
                    Code = TestCashGlCode,
                    Name = "Audit Test Cash",
                    AccountClass = GlAccountClass.Asset,
                    Status = ActiveStatus
                });
        }

        // Create the credit-side test GL account when it does not already exist.
        if (!existingCodes.Contains(TestDepositGlCode))
        {
            _dbContext.GlAccounts.Add(
                new GlAccount
                {
                    Code = TestDepositGlCode,
                    Name = "Audit Test Deposit Liability",
                    AccountClass = GlAccountClass.Liability,
                    Status = ActiveStatus
                });
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        var accounts = await _dbContext.GlAccounts
            .AsNoTracking()
            .Where(account =>
                account.Code == TestCashGlCode ||
                account.Code == TestDepositGlCode)
            .OrderBy(account => account.Id)
            .Select(account => new
            {
                account.Id,
                account.Code,
                account.Name,
                account.AccountClass
            })
            .ToListAsync(cancellationToken);

        return Ok(accounts);
    }

    // Creates one balanced transaction with matching debit and credit entries.
    [HttpPost("balanced")]
    public async Task<IActionResult> CreateBalancedTestDataAsync(
        CancellationToken cancellationToken)
    {
        var userId =
            await GetExistingUserIdAsync(
                cancellationToken);

        if (userId is null)
        {
            return BadRequest(
                "No user exists. Create a user before inserting audit test data.");
        }

        var glAccountIds =
            await GetRequiredAuditGlAccountIdsAsync(
                cancellationToken);

        if (glAccountIds is null)
        {
            return BadRequest(
                "Create the audit GL accounts first using POST /api/dev/audit-test-data/gl-accounts.");
        }

        await using var dbTransaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var transaction = new Transaction
        {
            TransactionNo =
                CreateTestTransactionNumber("B"),

            TransactionType =
                TransactionType.CashDeposit,

            TransactionStatus =
                TransactionStatus.Completed,

            InitiatedBy = userId.Value,

            Amount = BalancedDebitAmount,
            FeeAmount = 0m,

            TransactionAt =
                BalancedAuditDate.ToDateTime(
                    new TimeOnly(10, 0),
                    DateTimeKind.Utc),

            PostedAt =
                BalancedAuditDate.ToDateTime(
                    new TimeOnly(10, 5),
                    DateTimeKind.Utc),

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Transactions.Add(transaction);

        // Save first so the generated transaction ID can be used by the entries.
        await _dbContext.SaveChangesAsync(
            cancellationToken);

        var debitEntry = new TransactionEntry
        {
            TransactionId = transaction.Id,
            GlAccountId = glAccountIds.Value.DebitGlAccountId,
            EntryType = EntryType.Debit,
            Amount = BalancedDebitAmount,
            PostingDate = BalancedAuditDate,
            CreatedAt = DateTime.UtcNow
        };

        var creditEntry = new TransactionEntry
        {
            TransactionId = transaction.Id,
            GlAccountId = glAccountIds.Value.CreditGlAccountId,
            EntryType = EntryType.Credit,
            Amount = BalancedCreditAmount,
            PostingDate = BalancedAuditDate,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TransactionEntries.AddRange(
            debitEntry,
            creditEntry);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await dbTransaction.CommitAsync(
            cancellationToken);

        return Ok(new
        {
            auditDate = BalancedAuditDate,
            transactionId = transaction.Id,
            transactionNo = transaction.TransactionNo,
            debitGlAccountId = debitEntry.GlAccountId,
            creditGlAccountId = creditEntry.GlAccountId,
            debitAmount = debitEntry.Amount,
            creditAmount = creditEntry.Amount
        });
    }

    // Creates one intentionally unbalanced transaction for mismatch testing.
    [HttpPost("unbalanced")]
    public async Task<IActionResult> CreateUnbalancedTestDataAsync(
        CancellationToken cancellationToken)
    {
        var userId =
            await GetExistingUserIdAsync(
                cancellationToken);

        if (userId is null)
        {
            return BadRequest(
                "No user exists. Create a user before inserting audit test data.");
        }

        var glAccountIds =
            await GetRequiredAuditGlAccountIdsAsync(
                cancellationToken);

        if (glAccountIds is null)
        {
            return BadRequest(
                "Create the audit GL accounts first using POST /api/dev/audit-test-data/gl-accounts.");
        }

        await using var dbTransaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var transaction = new Transaction
        {
            TransactionNo =
                CreateTestTransactionNumber("U"),

            TransactionType =
                TransactionType.CashDeposit,

            TransactionStatus =
                TransactionStatus.Completed,

            InitiatedBy = userId.Value,

            Amount = BalancedDebitAmount,
            FeeAmount = 0m,

            TransactionAt =
                UnbalancedAuditDate.ToDateTime(
                    new TimeOnly(11, 0),
                    DateTimeKind.Utc),

            PostedAt =
                UnbalancedAuditDate.ToDateTime(
                    new TimeOnly(11, 5),
                    DateTimeKind.Utc),

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Transactions.Add(transaction);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        var debitEntry = new TransactionEntry
        {
            TransactionId = transaction.Id,
            GlAccountId = glAccountIds.Value.DebitGlAccountId,
            EntryType = EntryType.Debit,
            Amount = BalancedDebitAmount,
            PostingDate = UnbalancedAuditDate,
            CreatedAt = DateTime.UtcNow
        };

        var creditEntry = new TransactionEntry
        {
            TransactionId = transaction.Id,
            GlAccountId = glAccountIds.Value.CreditGlAccountId,
            EntryType = EntryType.Credit,

            // Intentionally different from the debit amount.
            Amount = UnbalancedCreditAmount,

            PostingDate = UnbalancedAuditDate,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TransactionEntries.AddRange(
            debitEntry,
            creditEntry);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await dbTransaction.CommitAsync(
            cancellationToken);

        return Ok(new
        {
            auditDate = UnbalancedAuditDate,
            transactionId = transaction.Id,
            transactionNo = transaction.TransactionNo,
            debitGlAccountId = debitEntry.GlAccountId,
            creditGlAccountId = creditEntry.GlAccountId,
            debitAmount = debitEntry.Amount,
            creditAmount = creditEntry.Amount
        });
    }

    // Returns an existing user ID required by the transaction foreign key.
    private async Task<long?> GetExistingUserIdAsync(
        CancellationToken cancellationToken)
    {
        var userId = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => (long?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return userId;
    }

    // Retrieves the two dedicated GL accounts used by audit test transactions.
    private async Task<(long DebitGlAccountId, long CreditGlAccountId)?>
        GetRequiredAuditGlAccountIdsAsync(
            CancellationToken cancellationToken)
    {
        var accounts = await _dbContext.GlAccounts
            .AsNoTracking()
            .Where(account =>
                account.Code == TestCashGlCode ||
                account.Code == TestDepositGlCode)
            .Select(account => new
            {
                account.Id,
                account.Code
            })
            .ToListAsync(cancellationToken);

        var debitAccount = accounts
            .SingleOrDefault(account =>
                account.Code == TestCashGlCode);

        var creditAccount = accounts
            .SingleOrDefault(account =>
                account.Code == TestDepositGlCode);

        if (debitAccount is null ||
            creditAccount is null)
        {
            return null;
        }

        return (
            debitAccount.Id,
            creditAccount.Id);
    }

    // Generates a short unique transaction number that fits the database limit.
    private static string CreateTestTransactionNumber(
        string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}"[..10];
    }
}