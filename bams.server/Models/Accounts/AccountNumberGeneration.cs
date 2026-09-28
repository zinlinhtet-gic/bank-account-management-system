namespace bams.server.Models.Accounts;

public sealed class AccountNumberGeneration
{
    public long Id { get; set; }

    public long AccountTypeId { get; set; }

    public string GenerationPeriod { get; set; } = string.Empty;

    public int LastSequenceNumber { get; set; }
}
