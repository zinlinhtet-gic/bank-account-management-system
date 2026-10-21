namespace bams.desktop.Constants;

/// <summary>
/// Sizing rules for data tables (see <c>Behaviors/DataGridAutoFitColumns</c>).
/// </summary>
public static class TableConstants
{
    /// <summary>
    /// Widest a text column grows to fit its content. Longer text ends with "…" and shows in full on hover,
    /// so one long description cannot push the rest of the table off screen.
    /// </summary>
    public const double MaxAutoTextColumnWidth = 320;
}
