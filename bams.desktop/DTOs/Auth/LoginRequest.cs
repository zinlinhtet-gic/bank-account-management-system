namespace bams.desktop.DTOs.Auth;

public sealed record LoginRequest(
    string Username,
    string Password);