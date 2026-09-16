namespace bams.server.Models.Accounting;

public sealed class GlAccount
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public GlAccountClass AccountClass { get; set; }

    public long? ParentId { get; set; }

    public GlAccount? Parent { get; set; }

    public string Status { get; set; } = string.Empty;
}
