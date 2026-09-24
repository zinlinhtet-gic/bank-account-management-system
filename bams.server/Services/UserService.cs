using System.Text.RegularExpressions;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Users;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using bams.server.Utils.Security;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>
/// Staff user management: list, detail, create, update, password reset and soft delete.
/// Access is limited to <c>user_management</c> by the controller; the rules below apply on top of that.
/// </summary>
public sealed class UserService : IUserService
{
    private static readonly Regex UsernameRegex = new(UserConstants.UsernamePattern, RegexOptions.CultureInvariant);
    private static readonly Regex EmailRegex = new(UserConstants.EmailPattern, RegexOptions.CultureInvariant);
    private static readonly Regex PhoneRegex = new(UserConstants.PhonePattern, RegexOptions.CultureInvariant);

    private readonly ApplicationDbContext _dbContext;

    public UserService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lists users that are not deleted, filtered by the query and sorted by username.
    /// </summary>
    public async Task<IReadOnlyList<UserSummaryResponse>> GetUsersAsync(
        UserListQuery query,
        CancellationToken cancellationToken)
    {
        // An inverted range can only return nothing, so tell the caller instead.
        if (query.CreatedFrom is not null
            && query.CreatedBefore is not null
            && query.CreatedFrom >= query.CreatedBefore)
        {
            throw new ValidationException(MessageCode.InvalidDateRange);
        }

        var users = _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Status != UserStatus.Deleted);

        // Search: part of the username, full name or email (MySQL collation makes this case-insensitive).
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(user =>
                user.Username.Contains(term)
                || user.FullName.Contains(term)
                || user.Email.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var roleCode = NormalizeRoleCode(query.Role);
            users = users.Where(user => user.UserRoles.Any(userRole => userRole.Role!.Code == roleCode));
        }

        // CreatedAt is stored in UTC, so compare against the UTC instant of each bound.
        if (query.CreatedFrom is not null)
        {
            var createdFromUtc = query.CreatedFrom.Value.UtcDateTime;
            users = users.Where(user => user.CreatedAt >= createdFromUtc);
        }

        if (query.CreatedBefore is not null)
        {
            var createdBeforeUtc = query.CreatedBefore.Value.UtcDateTime;
            users = users.Where(user => user.CreatedAt < createdBeforeUtc);
        }

        return await users
            .OrderBy(user => user.Username)
            .Select(user => new UserSummaryResponse(
                user.Id,
                user.Username,
                user.FullName,
                user.Email,
                user.UserRoles.Select(userRole => userRole.Role!.Code).FirstOrDefault() ?? string.Empty,
                user.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets one user that is not deleted, or throws UserNotFound.
    /// </summary>
    public async Task<UserResponse> GetUserByIdAsync(long id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(user => user.Id == id && user.Status != UserStatus.Deleted, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(MessageCode.UserNotFound);
        }

        return user.ToResponse();
    }

    /// <summary>
    /// Lists the roles that have a default password (the ones User Management may assign), in seed order.
    /// </summary>
    public async Task<IReadOnlyList<RoleResponse>> GetAssignableRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Id)
            .ToListAsync(cancellationToken);

        return roles
            .Where(role => UserConstants.DefaultPasswordsByRole.ContainsKey(role.Code))
            .Select(role => new RoleResponse(role.Code, role.Name, UserConstants.DefaultPasswordsByRole[role.Code]))
            .ToList();
    }

    /// <summary>
    /// Validates the request, rejects duplicates, and creates an active user with the role's default password.
    /// </summary>
    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var input = NormalizeAndValidateInput(request.FullName, request.Username, request.Email, request.Phone, request.Role);
        var role = await GetAssignableRoleAsync(input.RoleCode, cancellationToken);

        await EnsureUsernameAndEmailAreUniqueAsync(input, excludedUserId: null, cancellationToken);

        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = input.Username,
            Email = input.Email,
            FullName = input.FullName,
            Phone = input.Phone,
            PasswordHash = PasswordHasher.HashPassword(UserConstants.DefaultPasswordsByRole[role.Code]),
            Status = UserStatus.Active,
            OnlineStatus = OnlineStatus.Inactive,
            // The manager knows the default password, so the new user must replace it at the first login.
            MustChangePassword = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.UserRoles.Add(new UserRole { Role = role });

        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return user.ToResponse();
    }

    /// <summary>
    /// Validates the request, rejects duplicates, keeps at least one active manager, and saves the changes.
    /// </summary>
    public async Task<UserResponse> UpdateUserAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await GetEditableUserAsync(id, cancellationToken);
        var input = NormalizeAndValidateInput(request.FullName, request.Username, request.Email, request.Phone, request.Role);
        var role = await GetAssignableRoleAsync(input.RoleCode, cancellationToken);

        await EnsureUsernameAndEmailAreUniqueAsync(input, excludedUserId: user.Id, cancellationToken);

        var currentRoleCode = user.UserRoles.FirstOrDefault()?.Role?.Code;
        var isRoleChanged = currentRoleCode != role.Code;

        // Business rule: taking the manager role away from the last active manager would lock everyone out of User Management.
        if (isRoleChanged && currentRoleCode == SecurityConstants.ManagerRole)
        {
            await EnsureIsNotLastActiveManagerAsync(user, cancellationToken);
        }

        user.FullName = input.FullName;
        user.Username = input.Username;
        user.Email = input.Email;
        user.Phone = input.Phone;
        user.UpdatedAt = DateTime.UtcNow;

        // Replace the role only when it changed; re-adding the same (UserId, RoleId) key would conflict.
        if (isRoleChanged)
        {
            user.UserRoles.Clear();
            user.UserRoles.Add(new UserRole { Role = role });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return user.ToResponse();
    }

    /// <summary>
    /// Sets the user's password back to their role's default and forces a change at the next login.
    /// </summary>
    public async Task<UserResponse> ResetPasswordAsync(long id, CancellationToken cancellationToken)
    {
        var user = await GetEditableUserAsync(id, cancellationToken);
        var roleCode = user.UserRoles.FirstOrDefault()?.Role?.Code ?? string.Empty;

        // A user whose role has no default password cannot be reset through this screen.
        if (!UserConstants.DefaultPasswordsByRole.TryGetValue(roleCode, out var defaultPassword))
        {
            throw new ValidationException(MessageCode.InvalidRole);
        }

        user.PasswordHash = PasswordHasher.HashPassword(defaultPassword);
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return user.ToResponse();
    }

    /// <summary>
    /// Soft-deletes a user. Self-delete is allowed, except for the last active manager.
    /// </summary>
    public async Task DeleteUserAsync(long id, CancellationToken cancellationToken)
    {
        var user = await GetEditableUserAsync(id, cancellationToken);

        // Business rule: the bank must always keep one active manager who can manage users.
        if (user.UserRoles.Any(userRole => userRole.Role?.Code == SecurityConstants.ManagerRole))
        {
            await EnsureIsNotLastActiveManagerAsync(user, cancellationToken);
        }

        // Soft delete: keep the row for history and audit; login and every endpoint now refuse this user.
        user.Status = UserStatus.Deleted;
        user.OnlineStatus = OnlineStatus.Inactive;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Loads a tracked, non-deleted user with their role for a change, or throws UserNotFound.
    private async Task<User> GetEditableUserAsync(long id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(user => user.Id == id && user.Status != UserStatus.Deleted, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(MessageCode.UserNotFound);
        }

        return user;
    }

    // Returns the role for the code, or throws InvalidRole when it does not exist or has no default password.
    private async Task<Role> GetAssignableRoleAsync(string roleCode, CancellationToken cancellationToken)
    {
        if (!UserConstants.DefaultPasswordsByRole.ContainsKey(roleCode))
        {
            throw new ValidationException(MessageCode.InvalidRole);
        }

        var role = await _dbContext.Roles.FirstOrDefaultAsync(role => role.Code == roleCode, cancellationToken);

        return role ?? throw new ValidationException(MessageCode.InvalidRole);
    }

    // Rejects a username or email already used by another user. Deleted users still count: their username
    // and email stay reserved so history and audit records keep pointing at one person.
    private async Task EnsureUsernameAndEmailAreUniqueAsync(
        UserInput input,
        long? excludedUserId,
        CancellationToken cancellationToken)
    {
        var otherUsers = _dbContext.Users.AsQueryable();

        // On update the user may keep their own username and email.
        if (excludedUserId is not null)
        {
            var userId = excludedUserId.Value;
            otherUsers = otherUsers.Where(user => user.Id != userId);
        }

        if (await otherUsers.AnyAsync(user => user.Username == input.Username, cancellationToken))
        {
            throw new ConflictException(MessageCode.UsernameAlreadyExists);
        }

        if (await otherUsers.AnyAsync(user => user.Email == input.Email, cancellationToken))
        {
            throw new ConflictException(MessageCode.EmailAlreadyExists);
        }
    }

    // Throws LastManagerCannotBeRemoved when no other active manager exists besides this user.
    private async Task EnsureIsNotLastActiveManagerAsync(User user, CancellationToken cancellationToken)
    {
        var hasAnotherActiveManager = await _dbContext.Users.AnyAsync(
            other => other.Id != user.Id
                && other.Status == UserStatus.Active
                && other.UserRoles.Any(userRole => userRole.Role!.Code == SecurityConstants.ManagerRole),
            cancellationToken);

        if (!hasAnotherActiveManager)
        {
            throw new BusinessRuleException(MessageCode.LastManagerCannotBeRemoved);
        }
    }

    // Trims the fields, then applies the request validation shared by create and update.
    private static UserInput NormalizeAndValidateInput(
        string? fullName,
        string? username,
        string? email,
        string? phone,
        string? role)
    {
        // Collapse repeated spaces so "Aung   Aung" and "Aung Aung" are stored the same way.
        var normalizedFullName = string.Join(' ', (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var input = new UserInput(
            normalizedFullName,
            (username ?? string.Empty).Trim(),
            (email ?? string.Empty).Trim(),
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            NormalizeRoleCode(role));

        // Required fields (phone is optional).
        if (input.FullName.Length == 0 || input.Username.Length == 0 || input.Email.Length == 0 || input.RoleCode.Length == 0)
        {
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        }

        // Column limits (username and phone lengths are also enforced by their patterns below).
        if (input.FullName.Length > UserConstants.FullNameMaximumLength
            || input.Email.Length > UserConstants.EmailMaximumLength)
        {
            throw new ValidationException(MessageCode.FieldTooLong);
        }

        if (!UsernameRegex.IsMatch(input.Username))
        {
            throw new ValidationException(MessageCode.InvalidUsernameFormat);
        }

        if (!EmailRegex.IsMatch(input.Email))
        {
            throw new ValidationException(MessageCode.InvalidEmailFormat);
        }

        if (input.Phone is not null && !PhoneRegex.IsMatch(input.Phone))
        {
            throw new ValidationException(MessageCode.InvalidPhoneFormat);
        }

        return input;
    }

    // Role codes are stored in lowercase (see SecurityConstants).
    private static string NormalizeRoleCode(string? role)
    {
        return (role ?? string.Empty).Trim().ToLowerInvariant();
    }

    // Normalized create/update input.
    private sealed record UserInput(string FullName, string Username, string Email, string? Phone, string RoleCode);
}
