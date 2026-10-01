namespace bams.server.DTO.Accounts;

public sealed record CreateAccountRequest(
    long AccountTypeId,
    decimal OpeningBalance);
