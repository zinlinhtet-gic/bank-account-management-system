namespace bams.desktop.DTOs.Accounts;

public sealed record UpdateAccountStatusCommand(string? Reason, long Version);
