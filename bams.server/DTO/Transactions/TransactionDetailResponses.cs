using bams.server.Models.Transactions;

namespace bams.server.DTO.Transactions;

/// <summary>
/// One row of the transaction list.
/// </summary>
public sealed record TransactionSummaryResponse(
    long Id,
    string TransactionNo,
    TransactionType TransactionType,
    TransactionStatus TransactionStatus,
    decimal Amount,
    DateTime TransactionAt,
    string? Description,
    string? ReferenceNo,
    long InitiatedBy,
    string InitiatedByUserName,
    string InitiatedByFullName);

/// <summary>
/// Complete read-only transaction information used for transaction history
/// and auditor investigation.
/// </summary>
public sealed record TransactionDetailResponse(
    long Id,
    string TransactionNo,
    TransactionType TransactionType,
    TransactionStatus TransactionStatus,
    decimal Amount,
    decimal FeeAmount,
    DateTime TransactionAt,
    string? Description,
    string? ReferenceNo,
    long InitiatedBy,
    string InitiatedByUsername,
    string InitiatedByFullName,
    long? AuthorizedBy,
    string? AuthorizedByUsername,
    string? AuthorizedByFullName,
    DateTime? AuthorizedAt,
    long? PostedBy,
    string? PostedByUsername,
    string? PostedByFullName,
    DateTime? PostedAt,
    long? ReversalOfTransactionId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AccountEntryResponse> AccountEntries,
    IReadOnlyList<TransactionAccountingEntryResponse> AccountingEntries,
    IReadOnlyList<TransactionAuditLogResponse> AuditLogs,
    NrcTransferDetailResponse? NrcTransfer,
    InterbankTransferDetailResponse? InterbankTransfer);

/// <summary>
/// A debit or credit posted to a customer account by a transaction.
/// Includes balances before and after the posting for audit review.
/// </summary>
public sealed record AccountEntryResponse(
    long AccountId,
    string AccountNo,
    EntryType EntryType,
    decimal Amount,
    decimal LedgerBalanceBefore,
    decimal LedgerBalanceAfter,
    decimal AvailableBalanceBefore,
    decimal AvailableBalanceAfter,
    DateOnly ValueDate,
    DateOnly PostingDate,
    string? Description,
    string? ReferenceNo);

/// <summary>
/// NRC transfer detail. The pickup code is never returned here.
/// </summary>
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

/// <summary>
/// Interbank transfer detail with the payment gateway state.
/// </summary>
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

/// <summary>
/// A branch of this bank where NRC transfers can be collected.
/// </summary>
public sealed record BranchResponse(
    long Id,
    string Code,
    string Name,
    string? City);

/// <summary>
/// A bank an interbank transfer can be sent to.
/// </summary>
public sealed record OtherBankResponse(
    long Id,
    string BankCode,
    string BankName);

/// <summary>
/// One line of an account statement.
/// </summary>
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
