namespace bams.server.Constants;

/// <summary>
/// Staff user invariants used by the user configuration, validation and password resets.
/// </summary>
public static class UserConstants
{
    // Column lengths (also used by Data/Configurations/SecurityConfigurations.cs).
    public const int UsernameMaximumLength = 64;
    public const int EmailMaximumLength = 256;
    public const int FullNameMaximumLength = 150;
    public const int PhoneMaximumLength = 32;

    // Letters, digits, dot, underscore and hyphen; no spaces, because the username is typed at login.
    public const string UsernamePattern = @"^[A-Za-z0-9._-]{3,64}$";

    // Simple shape check (name@domain.tld); the address itself is not verified.
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    // Digits with optional +, spaces, hyphens and brackets, e.g. "+95 9 123 456 789" or "555-0100".
    public const string PhonePattern = @"^[0-9+\-\s()]{6,32}$";

    /// <summary>
    /// A signed-in user counts as online while their last login or heartbeat is newer than this.
    /// The desktop sends a heartbeat every 15 seconds (<c>PresenceConstants.HeartbeatInterval</c>), so this allows
    /// two missed heartbeats before showing offline (e.g. after the app was closed without logging out or the PC
    /// lost its connection). Change both sides together.
    /// </summary>
    public static readonly TimeSpan OnlinePresenceTimeout = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Password given to a new user and after a password reset. The user must change it at the next login.
    /// Every role that can be assigned in User Management needs an entry here.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultPasswordsByRole =
        new Dictionary<string, string>
        {
            [SecurityConstants.ManagerRole] = "Manager123!",
            [SecurityConstants.OfficerRole] = "Officer123!",
            [SecurityConstants.AuditorRole] = "Auditor123!"
        };
}
