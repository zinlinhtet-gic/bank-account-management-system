namespace bams.server.Models;

/// <summary>
/// Identifies entities protected by an application-managed optimistic concurrency token.
/// </summary>
public interface IConcurrencyTracked
{
    long Version { get; set; }
}
