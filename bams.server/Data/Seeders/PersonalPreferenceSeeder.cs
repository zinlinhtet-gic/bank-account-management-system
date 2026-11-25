using bams.server.Constants;
using bams.server.Services;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Data.Seeders;

/// <summary>
/// Adds missing preference rows for users created outside UserService (seeded users on a fresh database).
/// The migration already backfills users that existed when the table was created; this covers the rest and is
/// safe to run on every start because it only adds rows that are missing.
/// </summary>
public static class PersonalPreferenceSeeder
{
    /// <summary>
    /// Creates a row for every user and setting pair that has none. Only manager users start enabled.
    /// </summary>
    public static async Task SeedMissingPreferencesAsync(ApplicationDbContext dbContext)
    {
        List<long> settingIds = await dbContext.Settings.AsNoTracking().Select(setting => setting.Id).ToListAsync();

        var users = await dbContext.Users
            .AsNoTracking()
            .Select(user => new
            {
                user.Id,
                IsManager = user.UserRoles.Any(userRole => userRole.Role!.Code == SecurityConstants.ManagerRole)
            })
            .ToListAsync();

        HashSet<(long UserId, long SettingId)> existing = (await dbContext.PersonalPreferences
            .AsNoTracking()
            .Select(preference => new { preference.UserId, preference.SettingId })
            .ToListAsync())
            .Select(preference => (preference.UserId, preference.SettingId))
            .ToHashSet();

        DateTime now = DateTime.UtcNow;
        foreach (var user in users)
        {
            foreach (long settingId in settingIds)
            {
                if (existing.Contains((user.Id, settingId)))
                {
                    continue;
                }

                var preference = PersonalPreferenceDefaults.CreateFor(settingId, user.IsManager, now);
                preference.UserId = user.Id;
                dbContext.PersonalPreferences.Add(preference);
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
