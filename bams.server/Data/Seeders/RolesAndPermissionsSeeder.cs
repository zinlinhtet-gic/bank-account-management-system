using bams.server.Constants;
using bams.server.Models.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace bams.server.Data.Seeders;

/// <summary>
/// Seeds initial security data: roles, permissions, role-permission mappings, and default users.
/// </summary>
public static class RolesAndPermissionsSeeder
{
    /// <summary>
    /// Seeds all security data if not already present.
    /// </summary>
    public static async Task SeedSecurityDataAsync(ApplicationDbContext dbContext)
    {
        // Clear existing security data to ensure clean seed
        await ClearSecurityDataAsync(dbContext);

        await SeedPermissionsAsync(dbContext);
        await SeedRolesAsync(dbContext);
        await SeedRolePermissionsAsync(dbContext);
        await SeedUsersAsync(dbContext);
    }

    /// <summary>
    /// Clears existing security data to ensure clean seeding.
    /// </summary>
    private static async Task ClearSecurityDataAsync(ApplicationDbContext dbContext)
    {
        // Clear in the correct order to respect foreign key constraints
        await dbContext.UserRoles.ExecuteDeleteAsync();
        await dbContext.RolePermissions.ExecuteDeleteAsync();
        await dbContext.Users.ExecuteDeleteAsync();
        await dbContext.Roles.ExecuteDeleteAsync();
        await dbContext.Permissions.ExecuteDeleteAsync();
    }

    /// <summary>
    /// Seeds all permissions defined in SecurityConstants.
    /// </summary>
    private static async Task SeedPermissionsAsync(ApplicationDbContext dbContext)
    {
        var permissions = new List<Permission>
        {
            new Permission { Code = SecurityConstants.UserManagement, Name = "user management" },
            new Permission { Code = SecurityConstants.CustomerManagement, Name = "customer management" },
            new Permission { Code = SecurityConstants.CustomerKyc, Name = "customer kyc" },
            new Permission { Code = SecurityConstants.Accounting, Name = "accounting" },
            new Permission { Code = SecurityConstants.Configuration, Name = "configuration" },
            new Permission { Code = SecurityConstants.Operation, Name = "operation" },
            new Permission { Code = SecurityConstants.AccountManagement, Name = "account management" },
            new Permission { Code = SecurityConstants.Transactions, Name = "transactions" },
            new Permission { Code = SecurityConstants.TransactionHistory, Name = "transaction history" },
            new Permission { Code = SecurityConstants.Audit, Name = "audit" },
            new Permission { Code = SecurityConstants.CustomerList, Name = "customer list" }
        };

        await dbContext.Permissions.AddRangeAsync(permissions);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the three system roles: Manager, Officer, Auditor.
    /// </summary>
    private static async Task SeedRolesAsync(ApplicationDbContext dbContext)
    {
        var roles = new List<Role>
        {
            new Role { Code = SecurityConstants.ManagerRole, Name = "manager" },
            new Role { Code = SecurityConstants.OfficerRole, Name = "officer" },
            new Role { Code = SecurityConstants.AuditorRole, Name = "auditor" }
        };

        await dbContext.Roles.AddRangeAsync(roles);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds role-permission mappings based on role responsibilities.
    /// </summary>
    private static async Task SeedRolePermissionsAsync(ApplicationDbContext dbContext)
    {
        var roles = await dbContext.Roles.ToListAsync();
        var permissions = await dbContext.Permissions.ToListAsync();

        var managerRole = roles.First(r => r.Code == SecurityConstants.ManagerRole);
        var officerRole = roles.First(r => r.Code == SecurityConstants.OfficerRole);
        var auditorRole = roles.First(r => r.Code == SecurityConstants.AuditorRole);

        var rolePermissions = new List<RolePermission>();

        // Manager: user_management, cus_management, cus_kyc, accounting, configuration, operation
        var managerPermissionCodes = new[]
        {
            SecurityConstants.UserManagement,
            SecurityConstants.CustomerManagement,
            SecurityConstants.CustomerKyc,
            SecurityConstants.Accounting,
            SecurityConstants.Configuration,
            SecurityConstants.Operation
        };

        foreach (var permissionCode in managerPermissionCodes)
        {
            var permission = permissions.First(p => p.Code == permissionCode);
            rolePermissions.Add(new RolePermission
            {
                RoleId = managerRole.Id,
                PermissionId = permission.Id
            });
        }

        // Officer: cus_management, acc_management, transactions
        var officerPermissionCodes = new[]
        {
            SecurityConstants.CustomerManagement,
            SecurityConstants.AccountManagement,
            SecurityConstants.Transactions
        };

        foreach (var permissionCode in officerPermissionCodes)
        {
            var permission = permissions.First(p => p.Code == permissionCode);
            rolePermissions.Add(new RolePermission
            {
                RoleId = officerRole.Id,
                PermissionId = permission.Id
            });
        }

        // Auditor: cus_list, accounting, transaction_history, audit
        var auditorPermissionCodes = new[]
        {
            SecurityConstants.CustomerList,
            SecurityConstants.Accounting,
            SecurityConstants.TransactionHistory,
            SecurityConstants.Audit
        };

        foreach (var permissionCode in auditorPermissionCodes)
        {
            var permission = permissions.First(p => p.Code == permissionCode);
            rolePermissions.Add(new RolePermission
            {
                RoleId = auditorRole.Id,
                PermissionId = permission.Id
            });
        }

        await dbContext.RolePermissions.AddRangeAsync(rolePermissions);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds default users for each role with known credentials for testing.
    /// </summary>
    private static async Task SeedUsersAsync(ApplicationDbContext dbContext)
    {
        var roles = await dbContext.Roles.ToListAsync();
        var managerRole = roles.First(r => r.Code == SecurityConstants.ManagerRole);
        var officerRole = roles.First(r => r.Code == SecurityConstants.OfficerRole);
        var auditorRole = roles.First(r => r.Code == SecurityConstants.AuditorRole);

        var now = DateTime.UtcNow;

        var users = new List<User>
        {
            // Manager user - Password: Manager123!
            new User
            {
                Username = "manager",
                Email = "manager@bams.local",
                PasswordHash = HashPassword("Manager123!"),
                FullName = "system manager",
                Phone = "555-0100",
                OnlineStatus = OnlineStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now
            },
            // Officer user - Password: Officer123!
            new User
            {
                Username = "officer",
                Email = "officer@bams.local",
                PasswordHash = HashPassword("Officer123!"),
                FullName = "bank officer",
                Phone = "555-0101",
                OnlineStatus = OnlineStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now
            },
            // Auditor user - Password: Auditor123!
            new User
            {
                Username = "auditor",
                Email = "auditor@bams.local",
                PasswordHash = HashPassword("Auditor123!"),
                FullName = "system auditor",
                Phone = "555-0102",
                OnlineStatus = OnlineStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        await dbContext.Users.AddRangeAsync(users);
        await dbContext.SaveChangesAsync();

        // Assign roles to users
        var userRoles = new List<UserRole>
        {
            new UserRole { UserId = users[0].Id, RoleId = managerRole.Id }, // Manager
            new UserRole { UserId = users[1].Id, RoleId = officerRole.Id }, // Officer
            new UserRole { UserId = users[2].Id, RoleId = auditorRole.Id }  // Auditor
        };

        await dbContext.UserRoles.AddRangeAsync(userRoles);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Simple password hashing for initial seed data.
    /// Note: In production, use ASP.NET Core Identity's password hasher.
    /// </summary>
    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}