using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using bams.desktop.ViewModels;
using bams.desktop.DTOs.Accounts;

namespace bams.desktop.ViewModels.Pages;

/// <summary>Holds the selected file and optional image preview for one required account document.</summary>
public sealed class AccountDocumentInputViewModel : ViewModelBase
{
    private string? _filePath;
    private ImageSource? _previewImage;

    public AccountDocumentInputViewModel(string documentType, ReusableCustomerDocumentResponse? source = null)
    {
        DocumentType = documentType;
        Source = source;
    }

    public ReusableCustomerDocumentResponse? Source { get; }
    public long? SourceCustomerDocumentId => Source?.Id;
    public bool HasExistingDocument => Source is not null;
    public bool NeedsFileUpload => !HasExistingDocument;
    public bool CanChooseFile => NeedsFileUpload && !IsPhoto;
    public bool CanChoosePhoto => NeedsFileUpload && IsPhoto;
    public bool IsSatisfied => HasExistingDocument || (HasFile && File.Exists(FilePath));

    public string DocumentType { get; }
    public string DocumentLabel => DocumentType switch
    {
        "Nrc" => "NRC",
        "HouseholdRegistration" => "Household Registration",
        "ProofOfAddress" => "Proof of Address",
        "TaxDocument" => "Tax Document",
        "SourceOfFunds" => "Source of Funds",
        "Passport" => "Passport",
        "Visa" => "Visa",
        "Photo" => "Photo",
        _ => DocumentType
    };
    public bool IsPhoto => string.Equals(DocumentType, "Photo", StringComparison.OrdinalIgnoreCase);
    public string? FilePath
    {
        get => _filePath;
        private set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(HasFile));
                OnPropertyChanged(nameof(IsSatisfied));
            }
        }
    }

    public string FileName => Source is not null
        ? $"Already uploaded — will be copied · {Source.HolderName}" + (string.IsNullOrWhiteSpace(Source.DocumentNumber) ? "" : $" · {Source.DocumentNumber}")
        : string.IsNullOrWhiteSpace(FilePath) ? "No file selected" : Path.GetFileName(FilePath);
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);
    public ImageSource? PreviewImage { get => _previewImage; private set => SetProperty(ref _previewImage, value); }

    /// <summary>Updates this required-document input after the user chooses a file.</summary>
    public void SetFilePath(string filePath)
    {
        FilePath = filePath;
        PreviewImage = IsPhoto ? LoadPhotoPreview(filePath) : null;
    }

    // Decodes and releases the file handle immediately so the selected image can be previewed safely.
    private static ImageSource? LoadPhotoPreview(string filePath)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 240;
            image.UriSource = new Uri(filePath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
