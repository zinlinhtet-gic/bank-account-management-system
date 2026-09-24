namespace bams.server.DTO.Auth;

public sealed record ChangePasswordResponse(
    bool Success,
    string Message);