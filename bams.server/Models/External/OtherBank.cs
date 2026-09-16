namespace bams.server.Models.External;

public sealed class OtherBank
{
    public long Id { get; set; }

    public string BankCode { get; set; } = string.Empty;

    public string BankName { get; set; } = string.Empty;

    public string? SwiftCode { get; set; }

    public string Status { get; set; } = string.Empty;
}
