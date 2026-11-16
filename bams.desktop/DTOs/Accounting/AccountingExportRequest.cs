namespace bams.desktop.DTOs.Accounting;

/// <summary>
/// What the user asked to export to Excel: an optional GL account class (null means "All Account"),
/// an inclusive period of whole months, and where the finished file must be saved on this computer.
/// </summary>
/// <param name="AccountClass">GL account class to export, or null for every class.</param>
/// <param name="StartYear">Year of the first month of the period.</param>
/// <param name="StartMonth">First month of the period (1-12).</param>
/// <param name="EndYear">Year of the last month of the period.</param>
/// <param name="EndMonth">Last month of the period (1-12).</param>
/// <param name="DestinationPath">Local path chosen by the user; the server never sees it.</param>
public sealed record AccountingExportRequest(
    GlAccountClass? AccountClass,
    int StartYear,
    int StartMonth,
    int EndYear,
    int EndMonth,
    string DestinationPath
);
