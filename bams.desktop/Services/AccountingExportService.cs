using System.IO;
using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Utils;

namespace bams.desktop.Services;

/// <summary>
/// Accounting Excel export. The backend export route is NOT implemented yet, so the download step is a
/// placeholder; the temp-file and relocation flow around it is final.
/// </summary>
public sealed class AccountingExportService : IAccountingExportService
{
    private readonly ApiClient _apiClient;

    public AccountingExportService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // Downloads to a temp file, then moves it to the user's chosen path; the temp file never outlives the call.
    public async Task ExportToExcelAsync(AccountingExportRequest request, CancellationToken cancellationToken)
    {
        var temporaryPath = BuildTemporaryFilePath();
        try
        {
            await DownloadExportToTemporaryFileAsync(request, temporaryPath, cancellationToken);
            MoveDownloadedFileToDestination(temporaryPath, request.DestinationPath);
        }
        finally
        {
            DeleteTemporaryFileIfPresent(temporaryPath);
        }
    }

    // Unique name in the system temp folder so concurrent exports never collide.
    private static string BuildTemporaryFilePath()
    {
        var fileName = AccountingConstants.ExportTempFilePrefix + Guid.NewGuid().ToString("N") + AccountingConstants.ExcelFileExtension;
        return Path.Combine(Path.GetTempPath(), fileName);
    }

    // PLACEHOLDER: no backend endpoint exists yet, so this always reports the export as unavailable.
    // When the backend ships, replace the throw with a call such as:
    //   await _apiClient.DownloadFileAsync(ApiConstants.AccountingExportEndpoint + "?" + <query built from request>, temporaryPath, cancellationToken);
    // ApiClient.DownloadFileAsync already streams the response body into the file.
    private Task DownloadExportToTemporaryFileAsync(
        AccountingExportRequest request,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        _ = _apiClient;
        throw new FileExportException(MessageCode.AccountingExportUnavailable);
    }

    // Places the finished file at the user's chosen path, replacing any file there (the Save dialog already asked).
    private static void MoveDownloadedFileToDestination(string temporaryPath, string destinationPath)
    {
        try
        {
            var destinationFolder = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            try
            {
                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            catch (IOException) when (File.Exists(temporaryPath))
            {
                // Moving can fail across drives; copying keeps the user's export and the temp file is deleted afterwards.
                File.Copy(temporaryPath, destinationPath, overwrite: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new FileExportException(MessageCode.AccountingExportSaveFailed, exception);
        }
    }

    // Removes the temp download; a failure here must not hide the real outcome of the export.
    private static void DeleteTemporaryFileIfPresent(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The OS clears the temp folder eventually; nothing useful the user can do.
        }
    }
}
