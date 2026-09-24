namespace bams.server.DTO.Auth;

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);