namespace bams.desktop.DTOs.Accounts;

public sealed record AccountSummaryResponse(
    long Id,
    string AccountNo,
    string AccountTypeCode,
    string Status,
    decimal AvailableBalance,
    IReadOnlyList<AccountCustomerResponse> Customers,
    bool AllowDeposit,
    bool AllowWithdrawal,
    bool AllowTransfer);
