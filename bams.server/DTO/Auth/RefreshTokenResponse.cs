namespace bams.server.DTO.Auth;

/// <summary>
/// A new token for the signed-in user, issued while they are still using the app (sliding expiration).
/// </summary>
public sealed record RefreshTokenResponse(
    string Token,
    DateTime Expiration);
