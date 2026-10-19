namespace bams.desktop.DTOs.Accounting;

public sealed record SubmitBankExpenseRequest(long SessionId, decimal Amount, string Payee, string Purpose,
    string? ReceiptFileName, string? ReceiptContentType, string? ReceiptBase64, string? MissingReceiptReason);
public sealed record ReviewBankExpenseRequest(bool Approve, string Note);
public sealed record BankExpenseResponse(long Id, long SessionId, DateOnly BusinessDate, decimal Amount,
    string Payee, string Purpose, string Status, string? ReceiptFileName, string? MissingReceiptReason,
    long RequestedBy, DateTime RequestedAtUtc, long? ReviewedBy, DateTime? ReviewedAtUtc,
    string? ReviewNote, long? TransactionId, long? ExceptionId)
{
    public DateOnly? PostingDate { get; init; }
}

