using bams.server.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

public static class AccountTypeSeeder
{
    public static async Task SeedAsync(ApplicationDbContext dbContext)
    {
        if (await dbContext.AccountTypes.AnyAsync())
        {
            return;
        }

        var accountTypes = new List<AccountType>
        {
            new()
            {
                Code = "SAV",
                Name = "Savings Account",
                Category = "Savings",
                MinimumOpeningBalance = 10000,
                MinimumMaintainedBalance = 5000,
                DailyTransactionLimit = 1000000,
                MonthlyTransactionLimit = 10000000,
                AllowWithdrawal = true,
                AllowTransfer = true,
                AllowPartialWithdrawal = true,
                Status = "Active"
            },
            new()
            {
                Code = "CUR",
                Name = "Current Account",
                Category = "Current",
                MinimumOpeningBalance = 50000,
                MinimumMaintainedBalance = 20000,
                DailyTransactionLimit = 5000000,
                MonthlyTransactionLimit = 50000000,
                AllowWithdrawal = true,
                AllowTransfer = true,
                AllowPartialWithdrawal = true,
                Status = "Active"
            },
            new()
            {
                Code = "FD",
                Name = "Fixed Deposit",
                Category = "Fixed Deposit",
                MinimumOpeningBalance = 100000,
                MinimumMaintainedBalance = 100000,
                DailyTransactionLimit = null,
                MonthlyTransactionLimit = null,
                AllowWithdrawal = false,
                AllowTransfer = false,
                AllowPartialWithdrawal = false,
                Status = "Active"
            }
        };

        await dbContext.AccountTypes.AddRangeAsync(accountTypes);
        await dbContext.SaveChangesAsync();
    }
}
