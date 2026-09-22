using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Auth;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace bams.server.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AuthenticationService(
        ApplicationDbContext dbContext,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token if credentials are valid.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user is null)
        {
            throw new ValidationException(MessageCode.InvalidCredentials);
        }

        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new ValidationException(MessageCode.InvalidCredentials);
        }

        // Check if this is first-time login
        var isFirstTimeLogin = user.LastLoginAt == null;
        var requiresPasswordChange = user.MustChangePassword || isFirstTimeLogin;

        // Update last login time
        user.LastLoginAt = DateTime.UtcNow;
        user.OnlineStatus = OnlineStatus.Active;
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var role = user.UserRoles.FirstOrDefault()?.Role?.Code ?? string.Empty;
        var token = GenerateJwtToken(user, role);

        return new LoginResponse(
            token,
            user.Username,
            user.FullName,
            role.ToLower(),
            DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("Jwt:ExpiryMinutes", 60)),
            requiresPasswordChange);
    }

    /// <summary>
    /// Generates a JWT token for the authenticated user.
    /// </summary>
    private string GenerateJwtToken(User user, string role)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not found");
        var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not found");
        var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not found");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FullName),
            new(ClaimTypes.Role, role.ToLower())
        };

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var signingKey = new SymmetricSecurityKey(keyBytes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("Jwt:ExpiryMinutes", 60)),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Verifies the provided password against the stored hash.
    /// </summary>
    private bool VerifyPassword(string password, string storedHash)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        var computedHash = Convert.ToBase64String(hash);
        return computedHash == storedHash;
    }

    /// <summary>
    /// Gets the permissions for a user based on their roles.
    /// </summary>
    public async Task<PermissionsResponse> GetUserPermissionsAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(MessageCode.AccountNotFound);
        }

        var role = user.UserRoles.FirstOrDefault()?.Role?.Code ?? string.Empty;

        // Get all permissions for the user's roles
        var userRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
        var permissionIds = await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => userRoleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);

        var permissions = await _dbContext.Permissions
            .AsNoTracking()
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);

        return new PermissionsResponse(
            user.Id,
            user.Username,
            role.ToLower(),
            permissions);
    }

    /// <summary>
    /// Changes the user's password.
    /// </summary>
    public async Task<ChangePasswordResponse> ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(MessageCode.AccountNotFound);
        }

        // Verify current password
        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new ValidationException(MessageCode.InvalidCredentials);
        }

        // Hash new password
        var newPasswordHash = HashPassword(request.NewPassword);

        // Update password and clear the must-change flag
        user.PasswordHash = newPasswordHash;
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ChangePasswordResponse(
            true,
            "Password changed successfully");
    }

    /// <summary>
    /// Hashes a password using SHA256.
    /// </summary>
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}