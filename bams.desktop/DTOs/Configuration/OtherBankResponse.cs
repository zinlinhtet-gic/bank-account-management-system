namespace bams.desktop.DTOs.Configuration;

public sealed record OtherBankResponse(
    long Id,
    string BankCode,
    string BankName,
    string? SwiftCode,
    string Status);
