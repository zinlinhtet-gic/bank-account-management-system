using bams.server.Constants;
using bams.server.Models.Accounting;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

/// <summary>
/// Seeds the general-ledger accounts that transaction posting writes to (codes in <see cref="AccountingConstants"/>).
/// Only missing codes are added, so this is safe to run on every startup and on databases that already have data.
/// </summary>
public static class ChartOfAccountsSeeder
{
    private static readonly (string Code, string Name, GlAccountClass AccountClass)[] RequiredGlAccounts =
    [
        (AccountingConstants.CashOnHandGlCode, "Cash on Hand", GlAccountClass.Asset),
        (AccountingConstants.DueFromOtherBanksGlCode, "Due from Other Banks", GlAccountClass.Asset),
        (AccountingConstants.CustomerDepositsGlCode, "Customer Deposits", GlAccountClass.Liability),
        (AccountingConstants.InterbankClearingGlCode, "Interbank Clearing", GlAccountClass.Liability),
        (AccountingConstants.NrcTransfersPayableGlCode, "NRC Transfers Payable", GlAccountClass.Liability)
    ];

    /// <summary>
    /// Adds any required general-ledger account that does not exist yet.
    /// </summary>
    public static async Task SeedGlAccountsAsync(ApplicationDbContext dbContext)
    {
        var existingCodes = await dbContext.GlAccounts
            .Select(account => account.Code)
            .ToListAsync();

        var missingAccounts = RequiredGlAccounts
            .Where(required => !existingCodes.Contains(required.Code))
            .Select(required => new GlAccount
            {
                Code = required.Code,
                Name = required.Name,
                AccountClass = required.AccountClass,
                Status = AccountingConstants.ActiveGlAccountStatus
            })
            .ToList();

        if (missingAccounts.Count == 0)
        {
            return;
        }

        await dbContext.GlAccounts.AddRangeAsync(missingAccounts);
        await dbContext.SaveChangesAsync();
    }
}
