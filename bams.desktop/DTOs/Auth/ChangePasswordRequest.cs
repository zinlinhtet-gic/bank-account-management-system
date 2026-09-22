namespace bams.desktop.DTOs.Auth;

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);