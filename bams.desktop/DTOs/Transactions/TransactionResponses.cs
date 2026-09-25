namespace bams.desktop.DTOs.Transactions;

// Response payloads of the transaction endpoints (server: DTO/Transactions/*Response*.cs). Timestamps are UTC.

/// <summary>
/// Result of a posting. <see cref="PickupCode"/> is set only in the response that created an NRC transfer.
/// </summary>
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

/// <summary>One row of <c>GET api/transactions</c>.</summary>
public sealed record TransactionSummaryResponse(
    long Id,
    string TransactionNo,
    TransactionType TransactionType,
    TransactionStatus TransactionStatus,
    decimal Amount,
    DateTime TransactionAt,
    string? Description,
    string? ReferenceNo);

/// <summary>Full detail from <c>GET api/transactions/{id}</c>.</summary>
public sealed record TransactionDetailResponse(
    long Id,
    string TransactionNo,
    TransactionType TransactionType,
    TransactionStatus TransactionStatus,
    decimal Amount,
    decimal FeeAmount,
    DateTime TransactionAt,
    DateTime? PostedAt,
    string? Description,
    string? ReferenceNo,
    long InitiatedBy,
    long? PostedBy,
    long? ReversalOfTransactionId,
    IReadOnlyList<AccountEntryResponse> AccountEntries,
    NrcTransferDetailResponse? NrcTransfer,
    InterbankTransferDetailResponse? InterbankTransfer);

/// <summary>A debit or credit a transaction posted to a customer account.</summary>
public sealed record AccountEntryResponse(
    long AccountId,
    string AccountNo,
    EntryType EntryType,
    decimal Amount,
    decimal LedgerBalanceAfter,
    decimal AvailableBalanceAfter,
    DateOnly PostingDate);

/// <summary>NRC transfer detail; the pickup code is never included.</summary>
public sealed record NrcTransferDetailResponse(
    string SenderName,
    string SenderNrc,
    string? SenderPhone,
    string ReceiverName,
    string ReceiverNrc,
    string? ReceiverPhone,
    string DeliveryType,
    string PickupLocation,
    bool IsPaidInCash,
    long? DestinationAccountId,
    string Status,
    DateTime? PickupExpiresAt,
    DateTime? PickedUpAt,
    int FailedPickupAttempts);

/// <summary>Interbank transfer detail with the payment gateway state.</summary>
public sealed record InterbankTransferDetailResponse(
    long OtherBankId,
    string OtherBankName,
    string DestinationAccountNo,
    string BeneficiaryName,
    GatewayStatus GatewayStatus,
    string? GatewayReference,
    string? SettlementReference,
    DateTime? RequestAt,
    DateTime? ResponseAt);

/// <summary>One line of <c>GET api/transactions/accounts/{accountId}/statement</c>, newest first.</summary>
public sealed record AccountStatementLineResponse(
    long TransactionId,
    string TransactionNo,
    TransactionType TransactionType,
    EntryType EntryType,
    decimal Amount,
    decimal LedgerBalanceAfter,
    decimal AvailableBalanceAfter,
    DateOnly PostingDate,
    DateTime CreatedAt,
    string? Description,
    string? ReferenceNo);

/// <summary>A branch where NRC transfers are collected (<c>GET api/transactions/branches</c>).</summary>
public sealed record BranchResponse(
    long Id,
    string Code,
    string Name,
    string? City);

/// <summary>A bank an interbank transfer can be sent to (<c>GET api/transactions/other-banks</c>).</summary>
public sealed record OtherBankResponse(
    long Id,
    string BankCode,
    string BankName);
