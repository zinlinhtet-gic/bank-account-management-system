namespace bams.desktop.DTOs.Auth;

public sealed record ChangePasswordResponse(
    bool Success,
    string Message);