using System.IO;
using bams.desktop.Commands;
using bams.desktop.DTOs.Customers;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Customers;

/// <summary>
/// One document card on the create-customer form (document number, issued/expiry dates, and a file).
/// Reused for all 8 document types; <see cref="IsRequired"/> controls whether a missing file is an error.
/// </summary>
public sealed class DocumentFormViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;

    private string? _filePath;
    private string _fileError = string.Empty;
    private bool _isRequired;

    public DocumentFormViewModel(DocumentType documentType, string displayName, IDialogService dialogService, bool isRequired)
    {
        DocumentType = documentType;
        DisplayName = displayName;
        _dialogService = dialogService;
        _isRequired = isRequired;

        ChooseFileCommand = new RelayCommand(_ => ChooseFile());
    }

    public DocumentType DocumentType { get; }

    public string DisplayName { get; }

    /// <summary>
    /// Whether this document must have a file before Save can proceed. Changes at runtime for the
    /// NRC/Passport pair, since only the one matching the selected customer type is required.
    /// </summary>
    public bool IsRequired
    {
        get => _isRequired;
        set
        {
            if (SetProperty(ref _isRequired, value) && !value)
            {
                // No longer required (customer type changed): drop any stale "please attach this" error.
                FileError = string.Empty;
            }
        }
    }

    public RelayCommand ChooseFileCommand { get; }

    /// <summary>Free-text document number, e.g. the NRC/passport number printed on the document.</summary>
    public string? DocumentNumber { get; set; }

    public DateTime? IssuedDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? FilePath
    {
        get => _filePath;
        private set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(FileButtonText));
                FileError = string.Empty;
            }
        }
    }

    /// <summary>Chosen file name, or a placeholder prompt when none is chosen yet.</summary>
    public string FileButtonText => FilePath is null ? "Choose File" : Path.GetFileName(FilePath);

    public string FileError
    {
        get => _fileError;
        private set
        {
            if (SetProperty(ref _fileError, value))
            {
                OnPropertyChanged(nameof(HasFileError));
            }
        }
    }

    public bool HasFileError => !string.IsNullOrEmpty(FileError);

    // Opens the native file picker; a cancelled pick leaves the previous choice untouched.
    private void ChooseFile()
    {
        var path = _dialogService.PickFile($"Choose {DisplayName}", "Documents|*.pdf;*.jpg;*.jpeg;*.png|All files|*.*");
        if (path is not null)
        {
            FilePath = path;
        }
    }

    /// <summary>Sets <see cref="FileError"/> when required and no file was chosen; returns whether valid.</summary>
    public bool Validate()
    {
        FileError = IsRequired && FilePath is null ? "Please attach this document." : string.Empty;

        return !HasFileError;
    }

    /// <summary>Builds the request entry for this document; only called for documents that have a file.</summary>
    public CreateCustomerDocumentRequest ToRequest()
    {
        return new CreateCustomerDocumentRequest(
            DocumentType,
            string.IsNullOrWhiteSpace(DocumentNumber) ? null : DocumentNumber.Trim(),
            FilePath,
            IssuedDate is null ? null : DateOnly.FromDateTime(IssuedDate.Value.Date),
            ExpiryDate is null ? null : DateOnly.FromDateTime(ExpiryDate.Value.Date));
    }
}
