using System.Globalization;
using bams.desktop.Constants;

namespace bams.desktop.Utils;

/// <summary>
/// Formats and parses monetary text using the application's fixed comma-and-period notation.
/// </summary>
public static class MoneyText
{
    private static readonly CultureInfo MoneyCulture = CultureInfo.InvariantCulture;

    /// <summary>Formats a monetary value with comma group separators and exactly two decimals.</summary>
    public static string Format(decimal amount) => amount.ToString(DisplayFormats.Amount, MoneyCulture);

    /// <summary>
    /// Converts an in-progress money value to grouped text without adding decimal zeroes.
    /// Existing commas are always discarded and the whole part is regrouped every three digits,
    /// so grouping stays correct however many digits are typed, deleted, or pasted.
    /// Invalid text is returned unchanged so the owning form can report its existing validation error.
    /// </summary>
    public static string FormatWhileEditing(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Stale commas from the previous edit (e.g. "1,2345") are dropped and recomputed below.
        var ungrouped = text.Replace(",", string.Empty, StringComparison.Ordinal);
        var hasMinus = ungrouped.StartsWith("-", StringComparison.Ordinal);
        var unsigned = hasMinus ? ungrouped[1..] : ungrouped;
        var decimalIndex = unsigned.IndexOf('.');

        if (decimalIndex != unsigned.LastIndexOf('.') ||
            !unsigned.All(character => char.IsDigit(character) || character == '.'))
        {
            return text;
        }

        var wholePart = decimalIndex < 0 ? unsigned : unsigned[..decimalIndex];
        var fractionalPart = decimalIndex < 0 ? null : unsigned[(decimalIndex + 1)..];
        var groupedWholePart = GroupWholePart(wholePart);
        var prefix = hasMinus ? "-" : string.Empty;

        return fractionalPart is null
            ? prefix + groupedWholePart
            : prefix + groupedWholePart + "." + fractionalPart;
    }

    /// <summary>Parses grouped or ungrouped money text using the application's fixed notation.</summary>
    public static bool TryParse(string? text, out decimal amount)
    {
        amount = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(
            normalized,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            MoneyCulture,
            out amount);
    }

    private static string GroupWholePart(string wholePart)
    {
        if (wholePart.Length <= 3)
        {
            return wholePart;
        }

        var firstGroupLength = wholePart.Length % 3;
        if (firstGroupLength == 0)
        {
            firstGroupLength = 3;
        }

        var builder = new System.Text.StringBuilder(wholePart.Length + wholePart.Length / 3);
        builder.Append(wholePart[..firstGroupLength]);
        for (var index = firstGroupLength; index < wholePart.Length; index += 3)
        {
            builder.Append(',');
            builder.Append(wholePart, index, 3);
        }

        return builder.ToString();
    }
}
