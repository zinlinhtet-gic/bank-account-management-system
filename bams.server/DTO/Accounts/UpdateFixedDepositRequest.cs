using bams.server.Models.Accounts.Enums;

namespace bams.server.DTO.Accounts;

public sealed record UpdateFixedDepositRequest(
    RenewalInstruction? RenewalInstruction,
    long? PayoutAccountId,
    decimal? CurrentPrincipal,
    bool? CalculateFromCurrent,
    FixedDepositStatus? Status);
