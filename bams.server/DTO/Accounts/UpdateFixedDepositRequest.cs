using bams.server.Models.Accounts.Enums;

namespace bams.server.DTO.Accounts;

public sealed record UpdateFixedDepositRequest(
    RenewalInstruction? RenewalInstruction,
    long? PayoutAccountId,
    long Version);
