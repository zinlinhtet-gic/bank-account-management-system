using bams.server.Models.Transactions;

namespace bams.server.DTO.Transactions;

public sealed record TransactionResponse(
    long Id,
    string TransactionNo,
    TransactionType TransactionType,
    TransactionStatus TransactionStatus,
    decimal Amount,
    decimal FeeAmount,
    DateTime TransactionAt,
    string? ReferenceNo,
    long? SourceAccountId,
    long? DestinationAccountId,
    string? PickupCode);
