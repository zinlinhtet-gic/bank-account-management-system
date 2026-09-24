namespace bams.desktop.Models.Configuration;

/// Represents one correspondent bank row for display on the Other Banks list page (view only).
public sealed class OtherBankRowModel
{
    public string BankCode { get; init; } = string.Empty;
    public string BankName { get; init; } = string.Empty;
    public string SwiftCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
