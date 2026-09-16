namespace bams.server.Models.Security;

public sealed class UserRole
{
    public long UserId { get; set; }

    public User? User { get; set; }

    public long RoleId { get; set; }

    public Role? Role { get; set; }
}
