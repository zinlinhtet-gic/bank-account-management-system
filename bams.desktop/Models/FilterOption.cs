namespace bams.desktop.Models;

/// <summary>
/// An entry of a filter drop-down over an enum, e.g. transaction type or status.
/// </summary>
/// <param name="Value">The value sent to the server; null for the "All …" entry.</param>
/// <param name="DisplayName">Label shown in the drop-down.</param>
public sealed record FilterOption<T>(T? Value, string DisplayName) where T : struct, Enum
{
    // ComboBox shows ToString() when no template is set.
    public override string ToString() => DisplayName;
}
