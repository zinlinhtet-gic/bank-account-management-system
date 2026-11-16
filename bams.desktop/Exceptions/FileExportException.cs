using bams.desktop.Utils;

namespace bams.desktop.Exceptions;

/// <summary>
/// Raised when an export cannot be completed on the client: the export is not available yet,
/// or the downloaded file could not be moved to the location the user chose.
/// </summary>
public sealed class FileExportException : AppException
{
    // Creates an export failure whose text comes from the client message catalog.
    public FileExportException(MessageCode code)
        : base(code)
    {
    }

    // Keeps the underlying file-system failure for diagnostics while showing the catalog message.
    public FileExportException(MessageCode code, Exception innerException)
        : base(code, innerException)
    {
    }
}
