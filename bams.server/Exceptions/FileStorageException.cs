using bams.server.Messages;

namespace bams.server.Exceptions;

public sealed class FileStorageException : AppException
{
    // Creates a safe infrastructure exception for file-storage failures.
    public FileStorageException(MessageCode code, Exception innerException)
        : base(code, MessageCatalog.GetMessage(code), innerException)
    {
    }
}
