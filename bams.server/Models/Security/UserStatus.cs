namespace bams.server.Models.Security;

/// <summary>
/// Controls whether a staff user may sign in and call protected endpoints.
/// </summary>
public enum UserStatus
{
    Active = 1,
    Disabled = 2
}
