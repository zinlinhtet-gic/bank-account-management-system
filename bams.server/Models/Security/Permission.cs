namespace bams.server.Models.Security;

public sealed class Permission
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
