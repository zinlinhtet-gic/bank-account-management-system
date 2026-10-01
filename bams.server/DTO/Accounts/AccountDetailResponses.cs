using bams.server.Models.Accounts.Enums;
using bams.server.Models.Customers;
using bams.server.Models.Transactions;

namespace bams.server.DTO.Accounts;

public sealed record CustomerLookupResponse(long Id, string CustomerNo, string FullName, DateOnly DateOfBirth, string? NrcNumber, string? Phone, string? Email, string Status, bams.server.Models.Customers.CustomerType CustomerType);
public sealed record AccountOpeningOptionsResponse(IReadOnlyList<bams.server.DTO.Products.AccountTypeResponse> AccountTypes, IReadOnlyList<AccountTypeRequiredDocumentResponse> RequiredDocuments, IReadOnlyList<OwnedAccountOptionResponse> OwnedAccounts);
public sealed record AccountTypeRequiredDocumentResponse(long AccountTypeId, DocumentType DocumentType);
public sealed record OwnedAccountOptionResponse(long Id, string AccountNo, string AccountTypeCode, AccountStatus Status, string AccountTypeName);
public sealed record AccountTransactionDetailResponse(long Id, string TransactionNo, TransactionType TransactionType, TransactionStatus TransactionStatus, EntryType EntryType, decimal Amount, decimal LedgerBalanceAfter, decimal AvailableBalanceAfter, DateOnly ValueDate, DateOnly PostingDate, string? Description, string? ReferenceNo, DateTime CreatedAt);
public sealed record AccountStatusHistoryResponse(long Id, AccountStatus OldStatus, AccountStatus NewStatus, string? Reason, string ChangedBy, DateTime ChangedAt);
public sealed record InterestAccrualResponse(long Id, DateOnly PeriodStart, DateOnly PeriodEnd, decimal CalculationBalance, decimal AnnualRate, decimal CalculatedAmount, string Status, DateTime CalculatedAt, DateTime? PostedAt);
