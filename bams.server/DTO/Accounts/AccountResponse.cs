using bams.server.Models;

namespace bams.server.DTO.Accounts;

public sealed record AccountResponse(
    long Id,
    string AccountNumber,
    string Name,
    AccountType Type,
    AccountStatus Status,
    decimal Balance,
    DateTime CreatedAtUtc);
