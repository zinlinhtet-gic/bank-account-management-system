namespace bams.server.Models.Organization;

/// <summary>
/// A branch of this bank, e.g. where the receiver of an NRC transfer collects the money.
/// </summary>
public sealed class Branch
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? City { get; set; }

    public string Status { get; set; } = string.Empty;
}
