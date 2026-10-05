using bams.server.Models.External;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

public static class OtherBankSeeder
{
    public static async Task SeedAsync(ApplicationDbContext dbContext)
    {
        if (await dbContext.OtherBanks.AnyAsync())
        {
            return;
        }

        var otherBanks = new List<OtherBank>
        {
            new()
            {
                BankCode = "KBZ",
                BankName = "Kanbawza Bank",
                SwiftCode = "KBZBMMMY",
                Status = "Active"
            },
            new()
            {
                BankCode = "AYA",
                BankName = "Ayeyarwady Bank",
                SwiftCode = "AYABMMMY",
                Status = "Active"
            },
            new()
            {
                BankCode = "CB",
                BankName = "Co-operative Bank",
                SwiftCode = "CBOKMMMY",
                Status = "Active"
            }
        };

        await dbContext.OtherBanks.AddRangeAsync(otherBanks);
        await dbContext.SaveChangesAsync();
    }
}
