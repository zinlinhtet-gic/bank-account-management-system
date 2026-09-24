using bams.server.DTO.Users;
using bams.server.Models.Security;

namespace bams.server.Mapping;

public static class UserMappings
{
    // Converts a User entity (with UserRoles.Role loaded) into the detailed API response contract.
    // A user has one role in practice; login and permissions also read the first one.
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse(
            user.Id,
            user.Username,
            user.FullName,
            user.Email,
            user.Phone,
            user.UserRoles.FirstOrDefault()?.Role?.Code ?? string.Empty,
            user.Status,
            user.LastLoginAt,
            user.MustChangePassword,
            user.CreatedAt,
            user.UpdatedAt);
    }
}
