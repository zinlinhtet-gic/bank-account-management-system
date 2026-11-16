namespace bams.desktop.Constants;

/// <summary>Fixed values used by the Accounting pages.</summary>
public static class AccountingConstants
{
    public const int FirstMonthOfYear = 1;
    public const int LastMonthOfYear = 12;

    // Earliest year offered by the export period pickers; accounting data cannot predate the system.
    public const int EarliestExportYear = 2020;
    // How many years after the current one the export period pickers still offer.
    public const int ExportYearsAheadOfCurrent = 1;

    public const string ExcelFileExtension = ".xlsx";
    public const string ExcelFileFilter = "Excel workbook|*.xlsx";
    public const string ExportTempFilePrefix = "bams-accounting-export-";
    public const string ExportDialogTitle = "Save accounting export";
    public const string ExportFileNamePrefix = "accounting-export";
    public const string AllAccountsLabel = "All Account";
}
