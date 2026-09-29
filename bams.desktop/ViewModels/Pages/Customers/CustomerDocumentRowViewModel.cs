using System.IO;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Customers;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Customers;

/// <summary>
/// One document row on the customer details card: its type, number, dates, and a Download command
/// (shows a "Save As" picker, then streams the file from the server). Shows "Not attached" instead
/// of a Download button when the document has no file.
/// </summary>
public sealed class CustomerDocumentRowViewModel : ViewModelBase
{
    private readonly long _customerId;
    private readonly CustomerDocumentResponse _document;
    private readonly ICustomerService _customerService;
    private readonly IDialogService _dialogService;

    private bool _isDownloading;
    private string _errorMessage = string.Empty;

    public CustomerDocumentRowViewModel(
        long customerId,
        CustomerDocumentResponse document,
        ICustomerService customerService,
        IDialogService dialogService)
    {
        _customerId = customerId;
        _document = document;
        _customerService = customerService;
        _dialogService = dialogService;

        DownloadCommand = new AsyncRelayCommand(DownloadAsync, () => HasFile && !IsDownloading);
    }

    public string DisplayName => DocumentTypeDisplay.ToDisplayName(_document.DocumentType);

    public string DocumentNumberText => string.IsNullOrWhiteSpace(_document.DocumentNumber)
        ? DisplayFormats.EmptyValue
        : _document.DocumentNumber;

    public string IssuedText => _document.IssuedDate is null
        ? DisplayFormats.EmptyValue
        : _document.IssuedDate.Value.ToString(DisplayFormats.Date);

    public string ExpiryText => _document.ExpiryDate is null
        ? DisplayFormats.EmptyValue
        : _document.ExpiryDate.Value.ToString(DisplayFormats.Date);

    public bool IsVerified => _document.VerifiedAt is not null;

    /// <summary>Whether a file was ever uploaded for this document.</summary>
    public bool HasFile => !string.IsNullOrWhiteSpace(_document.FileReference);

    public AsyncRelayCommand DownloadCommand { get; }

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                DownloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // Asks where to save, then streams the file from the server.
    private async Task DownloadAsync()
    {
        ErrorMessage = string.Empty;

        var extension = string.IsNullOrEmpty(_document.FileReference) ? string.Empty : Path.GetExtension(_document.FileReference);
        var suggestedFileName = $"{DisplayName}{extension}";

        var destinationPath = _dialogService.PickSaveFile($"Save {DisplayName}", suggestedFileName, "All files|*.*");
        if (destinationPath is null)
        {
            return;
        }

        try
        {
            IsDownloading = true;
            await _customerService.DownloadCustomerDocumentAsync(_customerId, _document.Id, destinationPath, CancellationToken.None);
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsDownloading = false;
        }
    }
}
