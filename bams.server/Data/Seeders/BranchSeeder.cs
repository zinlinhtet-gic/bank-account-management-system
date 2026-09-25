using bams.server.Constants;
using bams.server.Models.Organization;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

/// <summary>
/// Seeds a starting set of branches, only when the table is empty. Real branches are expected to be maintained
/// through configuration later; this only makes NRC pickup usable on a fresh database.
/// </summary>
public static class BranchSeeder
{
    private static readonly (string Code, string Name, string City)[] InitialBranches =
    [
        ("YGN-HO", "Yangon Head Office", "Yangon"),
        ("YGN-SC", "Sanchaung Branch", "Yangon"),
        ("MDY-01", "Mandalay Branch", "Mandalay"),
        ("NPT-01", "Naypyitaw Branch", "Naypyitaw"),
        ("BGO-01", "Bago Branch", "Bago")
    ];

    /// <summary>
    /// Adds the initial branches when there are none yet.
    /// </summary>
    public static async Task SeedBranchesAsync(ApplicationDbContext dbContext)
    {
        if (await dbContext.Branches.AnyAsync())
        {
            return;
        }

        await dbContext.Branches.AddRangeAsync(InitialBranches.Select(branch => new Branch
        {
            Code = branch.Code,
            Name = branch.Name,
            City = branch.City,
            Status = BranchConstants.ActiveStatus
        }));
        await dbContext.SaveChangesAsync();
    }
}
