namespace bams.desktop.DTOs.Auth;

public sealed record RefreshTokenResponse(
    string Token,
    DateTime Expiration);
