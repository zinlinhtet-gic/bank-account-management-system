using System.Globalization;

namespace bams.desktop.Api;

/// <summary>
/// Builds URL query strings for list filters, e.g. <c>?search=ann&amp;page=2</c>. Empty values are left out and
/// every value is URL-encoded.
/// </summary>
public sealed class QueryString
{
    // Round-trip format keeps the offset, so the server compares the exact instant the user picked.
    private const string DateFormat = "o";

    private readonly List<string> _parameters = [];

    /// <summary>Adds name=value when the value is not empty.</summary>
    public QueryString Add(string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }

        return this;
    }

    /// <summary>Adds an instant in round-trip format when it is set.</summary>
    public QueryString Add(string name, DateTimeOffset? value)
    {
        return Add(name, value?.ToString(DateFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>Adds a number when it is set.</summary>
    public QueryString Add(string name, int? value)
    {
        return Add(name, value?.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Returns "?a=1&amp;b=2", or an empty string when nothing was added.</summary>
    public override string ToString()
    {
        return _parameters.Count == 0 ? string.Empty : "?" + string.Join("&", _parameters);
    }
}
