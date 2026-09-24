namespace bams.server.Models.Security;

/// <summary>
/// Controls whether a staff user may sign in and call protected endpoints.
/// </summary>
public enum UserStatus
{
    Active = 1,
    Disabled = 2,

    // Soft-deleted by User Management: the row is kept for history but the user can never sign in again.
    Deleted = 3
}
