using System.Security.Cryptography;
using System.Text;

namespace bams.server.Utils.Security;

/// <summary>
/// The single place that hashes and verifies staff passwords (login, change password, user management, seeding).
/// </summary>
/// <remarks>
/// Uses unsalted SHA-256 to stay compatible with the passwords already stored. Moving to a salted, slow hash
/// (e.g. ASP.NET Core Identity's PasswordHasher) only needs changes in this class plus a re-hash on next login.
/// </remarks>
public static class PasswordHasher
{
    /// <summary>
    /// Returns the Base64 SHA-256 hash stored in <c>Users.PasswordHash</c>.
    /// </summary>
    public static string HashPassword(string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));

        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Checks a plain-text password against a stored hash.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash)
    {
        return HashPassword(password) == storedHash;
    }
}
