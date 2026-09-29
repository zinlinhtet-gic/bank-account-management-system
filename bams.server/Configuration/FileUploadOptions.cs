namespace bams.server.Configuration;

public sealed class FileUploadOptions
{
    public const string SectionName = "FileUploads";
    public const long DefaultMaximumRequestSizeBytes = 60 * 1024 * 1024;
    public string RootPath { get; set; } = "App_Data/AccountDocuments";
    public long MaximumFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public long MaximumRequestSizeBytes { get; set; } = DefaultMaximumRequestSizeBytes;
}
