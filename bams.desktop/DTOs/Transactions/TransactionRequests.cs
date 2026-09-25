namespace bams.desktop.DTOs.Transactions;

// Request bodies of the transaction endpoints (server: DTO/Transactions/TransactionRequests.cs).

/// <summary>Body of <c>POST api/transactions/deposit</c>.</summary>
public sealed record DepositRequest(
    long AccountId,
    decimal Amount,
    string? Description,
    string? ReferenceNo);

/// <summary>Body of <c>POST api/transactions/withdrawal</c>.</summary>
public sealed record WithdrawalRequest(
    long AccountId,
    decimal Amount,
    string? Description,
    string? ReferenceNo);

/// <summary>Body of <c>POST api/transactions/transfer/internal</c>.</summary>
public sealed record InternalTransferRequest(
    long SourceAccountId,
    long DestinationAccountId,
    decimal Amount,
    string? Description,
    string? ReferenceNo);

/// <summary>Body of <c>POST api/transactions/transfer/interbank</c>.</summary>
public sealed record InterbankTransferRequest(
    long SourceAccountId,
    long OtherBankId,
    string DestinationAccountNo,
    string BeneficiaryName,
    decimal Amount,
    string? Description,
    string? ReferenceNo);

/// <summary>
/// Body of <c>POST api/transactions/transfer/nrc</c>. <paramref name="SourceAccountId"/> is null when the sender pays
/// in cash. <paramref name="DeliveryType"/> is <c>TransactionFieldRules.NrcDeliveryAtBranch</c> (with
/// <paramref name="PickupBranchId"/>) or <c>NrcDeliveryAtOtherBank</c> (with <paramref name="PickupOtherBankId"/>).
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
/// Body of <c>POST api/transactions/nrc-pickup</c>: paid out in cash, or into <paramref name="DestinationAccountId"/>.
/// </summary>
public sealed record NrcPickupRequest(
    long TransactionId,
    string PickupCode,
    long? DestinationAccountId);

/// <summary>Body of <c>POST api/transactions/transfer/nrc/{id}/paid-out</c> (the other bank paid the receiver).</summary>
public sealed record NrcPayoutRequest(
    string? PayoutReference);

/// <summary>Body of <c>POST api/transactions/transfer/nrc/{id}/cancel</c>.</summary>
public sealed record NrcCancelRequest(
    string? Reason);

/// <summary>Body of <c>POST api/transactions/transfer/interbank/{id}/complete</c>.</summary>
public sealed record InterbankSettlementRequest(
    string? GatewayReference,
    string? SettlementReference);

/// <summary>Body of <c>POST api/transactions/transfer/interbank/{id}/fail</c>.</summary>
public sealed record InterbankFailureRequest(
    string? GatewayReference,
    string? Reason);
