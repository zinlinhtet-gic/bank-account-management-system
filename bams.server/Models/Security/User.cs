namespace bams.server.Models.Security;

public sealed class User
{
    public long Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    public OnlineStatus OnlineStatus { get; set; } = OnlineStatus.Inactive;

    public DateTime? LastLoginAt { get; set; }

    // Last login or desktop heartbeat (UTC). With OnlineStatus it decides whether the user is shown as online.
    public DateTime? LastSeenAt { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
