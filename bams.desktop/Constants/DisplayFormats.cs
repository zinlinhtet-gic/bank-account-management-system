namespace bams.desktop.Constants;

/// <summary>
/// Date and time formats used on screen (always in the user's local time).
/// </summary>
public static class DisplayFormats
{
    public const string Date = "dd MMM yyyy";
    public const string DateTime = "dd MMM yyyy, HH:mm";

    // Shown instead of an empty optional value.
    public const string EmptyValue = "—";

    // Money: thousands separators and always two decimals, e.g. "1,204,550.00".
    public const string Amount = "#,##0.00";
    public const string CurrencyCode = "MMK";
}
