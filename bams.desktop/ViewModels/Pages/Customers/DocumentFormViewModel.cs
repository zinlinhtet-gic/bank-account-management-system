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

    /// <summary>Set when editing an existing customer: this document's id, or null for a brand-new one.</summary>
    public long? ExistingDocumentId { get; private set; }

    /// <summary>Set when editing: whether the existing document already has a file on the server.</summary>
    public bool HasExistingFile { get; private set; }

    /// <summary>A newly chosen file, or (when editing) one already on the server.</summary>
    public bool HasFile => FilePath is not null || HasExistingFile;

    /// <summary>Chosen file name; "Replace File" when a file already exists but was not replaced; else a prompt.</summary>
    public string FileButtonText
    {
        get
        {
            if (FilePath is not null)
            {
                return Path.GetFileName(FilePath);
            }

            return HasExistingFile ? "Replace File" : "Choose File";
        }
    }

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

    /// <summary>Sets <see cref="FileError"/> when required and no file (new or existing) is on record.</summary>
    public bool Validate()
    {
        FileError = IsRequired && !HasFile ? "Please attach this document." : string.Empty;

        return !HasFileError;
    }

    /// <summary>Clears this card back to a brand-new, empty document, for the create form.</summary>
    public void Reset()
    {
        ExistingDocumentId = null;
        HasExistingFile = false;
        DocumentNumber = null;
        IssuedDate = null;
        ExpiryDate = null;
        FilePath = null;
    }

    /// <summary>Fills this card from an existing customer's document, for the edit form.</summary>
    public void LoadExisting(CustomerDocumentResponse document)
    {
        ExistingDocumentId = document.Id;
        HasExistingFile = !string.IsNullOrWhiteSpace(document.FileReference);
        DocumentNumber = document.DocumentNumber;
        IssuedDate = document.IssuedDate is null ? null : document.IssuedDate.Value.ToDateTime(TimeOnly.MinValue);
        ExpiryDate = document.ExpiryDate is null ? null : document.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue);

        OnPropertyChanged(nameof(FileButtonText));
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

    /// <summary>
    /// Builds the update request entry for this document; only called for documents that either
    /// already exist (<see cref="ExistingDocumentId"/> set) or just had a new file chosen.
    /// </summary>
    public UpdateCustomerDocumentRequest ToUpdateRequest()
    {
        return new UpdateCustomerDocumentRequest(
            ExistingDocumentId,
            DocumentType,
            string.IsNullOrWhiteSpace(DocumentNumber) ? null : DocumentNumber.Trim(),
            FilePath,
            IssuedDate is null ? null : DateOnly.FromDateTime(IssuedDate.Value.Date),
            ExpiryDate is null ? null : DateOnly.FromDateTime(ExpiryDate.Value.Date));
    }
}
