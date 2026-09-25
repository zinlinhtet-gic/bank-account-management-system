namespace bams.server.DTO.Transactions;

public sealed record DepositRequest(
    long AccountId,
    decimal Amount,
    string? Description,
    string? ReferenceNo);

public sealed record WithdrawalRequest(
    long AccountId,
    decimal Amount,
    string? Description,
    string? ReferenceNo);

public sealed record InternalTransferRequest(
    long SourceAccountId,
    long DestinationAccountId,
    decimal Amount,
    string? Description,
    string? ReferenceNo);

public sealed record InterbankTransferRequest(
    long SourceAccountId,
    long OtherBankId,
    string DestinationAccountNo,
    string BeneficiaryName,
    decimal Amount,
    string? Description,
    string? ReferenceNo);

/// <summary>
/// Creates an NRC transfer. The sender pays from <paramref name="SourceAccountId"/>, or in cash when it is null.
/// <paramref name="DeliveryType"/> is "Branch" (collected at <paramref name="PickupBranchId"/>) or "OtherBank"
/// (paid out by <paramref name="PickupOtherBankId"/>). Neither sender nor receiver needs an account.
/// </summary>
public sealed record NrcTransferRequest(
    long? SourceAccountId,
    string SenderName,
    string SenderNrc,
    string? SenderPhone,
    string ReceiverName,
    string ReceiverNrc,
    string? ReceiverPhone,
    string DeliveryType,
    long? PickupBranchId,
    long? PickupOtherBankId,
    decimal Amount,
    string? Description,
    string? ReferenceNo);

/// <summary>
/// Pays out an NRC transfer at one of our branches: in cash, or into <paramref name="DestinationAccountId"/> when the
/// receiver has an account with us.
/// </summary>
public sealed record NrcPickupRequest(
    long TransactionId,
    string PickupCode,
    long? DestinationAccountId = null);

/// <summary>
/// Records that the other bank paid out an NRC transfer to the receiver.
/// </summary>
public sealed record NrcPayoutRequest(
    string? PayoutReference);

/// <summary>
/// Cancels a pending NRC transfer and refunds the sender. The reason is stored on the refund transaction.
/// </summary>
public sealed record NrcCancelRequest(
    string? Reason);

/// <summary>
/// Records that the payment gateway settled an interbank transfer.
/// </summary>
public sealed record InterbankSettlementRequest(
    string? GatewayReference,
    string? SettlementReference);

/// <summary>
/// Records that the payment gateway rejected an interbank transfer; the sender is refunded.
/// </summary>
public sealed record InterbankFailureRequest(
    string? GatewayReference,
    string? Reason);
