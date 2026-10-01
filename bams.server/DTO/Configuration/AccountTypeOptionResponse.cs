namespace bams.server.DTO.Configuration;

/// <summary>
/// An account type in a drop-down (the interest rate form's account type picker).
/// </summary>
public sealed record AccountTypeOptionResponse(
    long Id,
    string Code,
    string Name);
