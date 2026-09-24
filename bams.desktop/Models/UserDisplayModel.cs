using bams.desktop.Constants;
using bams.desktop.DTOs.Users;
using bams.desktop.Utils;

namespace bams.desktop.Models;

/// <summary>
/// One row of the User Management table, shaped for binding.
/// </summary>
public sealed class UserDisplayModel
{
    public long Id { get; init; }

    public string Username { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string RoleCode { get; init; } = string.Empty;

    public string RoleName => RoleCodes.ToDisplayName(RoleCode);

    /// <summary>Local creation time.</summary>
    public DateTime CreatedAt { get; init; }

    public string CreatedAtText => CreatedAt.ToString(DisplayFormats.Date);

    /// <summary>True for the signed-in user's own row (shows a "You" tag).</summary>
    public bool IsCurrentUser { get; init; }

    /// <summary>
    /// Builds a row from the server list item.
    /// </summary>
    public static UserDisplayModel FromResponse(UserSummaryResponse user, long? currentUserId)
    {
        return new UserDisplayModel
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            RoleCode = user.Role,
            CreatedAt = DateTimeDisplay.ToLocal(user.CreatedAt),
            IsCurrentUser = user.Id == currentUserId
        };
    }
}
