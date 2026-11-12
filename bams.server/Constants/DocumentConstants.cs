namespace bams.server.Constants;

public static class DocumentConstants
{
    public const string PendingVerificationStatus = "PendingVerification";
    public const int DocumentNumberMaximumLength = 64;
    public const int OriginalFileNameMaximumLength = 255;
    public const int FileReferenceMaximumLength = 300;
    public const int ContentTypeMaximumLength = 100;
    public const int StatusMaximumLength = 20;

    // Single private storage root (relative to the content root) shared by customer and account documents.
    public const string UploadRootFolderName = "uploads";
    public const string AccountDocumentFolderName = "accounts";
    public const int FileSignatureLength = 8;
    public const int FileCopyBufferSizeBytes = 64 * 1024;

    public static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png"
        };
}
