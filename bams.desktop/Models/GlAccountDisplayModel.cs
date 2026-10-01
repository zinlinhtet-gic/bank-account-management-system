using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Models;

/// <summary>
/// Presentation model for a General Ledger row.
/// Keeps display-specific formatting out of the API DTO.
/// </summary>
public sealed class GlAccountDisplayModel
{
    public long Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public GlAccountClass AccountClass { get; init; }

    public long? ParentId { get; init; }

    public string ParentDisplay { get; init; } = "-";

    public string Status { get; init; } = string.Empty;
}