namespace bams.server.DTO.Auth;

public sealed record LoginRequest(
    string Username,
    string Password);