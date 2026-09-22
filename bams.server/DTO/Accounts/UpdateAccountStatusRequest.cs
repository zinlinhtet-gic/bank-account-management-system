using bams.server.Models.Accounts.Enums;

namespace bams.server.DTO.Accounts;

/// <summary>
/// Describes an account status change and its optional audit reason.
/// </summary>
public sealed record UpdateAccountStatusRequest(
    AccountStatus Status,
    string? Reason);
