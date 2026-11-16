using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

/// <summary>Exports accounting data to an Excel file saved where the user chose.</summary>
public interface IAccountingExportService
{
    /// <summary>
    /// Downloads the Excel export from the server and moves it to <see cref="AccountingExportRequest.DestinationPath"/>.
    /// The server only returns the file; saving it at the chosen location is the client's job.
    /// </summary>
    /// <exception cref="Exceptions.FileExportException">Export unavailable, or the file could not be saved.</exception>
    /// <exception cref="Exceptions.AppException">The server rejected the request or could not be reached.</exception>
    Task ExportToExcelAsync(AccountingExportRequest request, CancellationToken cancellationToken);
}
