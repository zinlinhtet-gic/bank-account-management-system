using System.Windows.Media;
using bams.desktop.Commands;
using bams.desktop.DTOs.Customers;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Customers;

/// <summary>
/// KYC review dialog. Reuses <see cref="CustomerDetailsViewModel"/> for the read-only customer info,
/// photo and document list, and adds the Verify/Reject decision on top. Closes with <c>true</c> after
/// a decision is recorded, so the caller (the customer list) knows to refresh the row's KYC badge.
/// </summary>
public sealed class CustomerKycReviewViewModel : ViewModelBase, IDialogViewModel
{
    private readonly ICustomerService _customerService;
    private readonly IDialogService _dialogService;
    private readonly long _customerId;

    private bool _isBusy;
    private string _formError = string.Empty;

    public CustomerKycReviewViewModel(CustomerResponse customer, ImageSource? photo, ICustomerService customerService, IDialogService dialogService)
    {
        _customerService = customerService;
        _dialogService = dialogService;
        _customerId = customer.Id;

        Details = new CustomerDetailsViewModel(customer, photo, customerService, dialogService);

        VerifyCommand = new AsyncRelayCommand(() => DecideAsync(KycStatus.Verified), () => !IsBusy);
        RejectCommand = new AsyncRelayCommand(() => DecideAsync(KycStatus.Rejected), () => !IsBusy);
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(false), _ => !IsBusy);
    }

    public event Action<bool>? CloseRequested;

    public bool CanCloseOnBackdropClick => !IsBusy;

    public bool CanCancel => !IsBusy;

    /// <summary>The customer's read-only info, photo and documents (same component as the View dialog).</summary>
    public CustomerDetailsViewModel Details { get; }

    public AsyncRelayCommand VerifyCommand { get; }

    public AsyncRelayCommand RejectCommand { get; }

    public RelayCommand CloseCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                VerifyCommand.RaiseCanExecuteChanged();
                RejectCommand.RaiseCanExecuteChanged();
                CloseCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Error that belongs to no single field (server conflict, network...).</summary>
    public string FormError
    {
        get => _formError;
        private set
        {
            if (SetProperty(ref _formError, value))
            {
                OnPropertyChanged(nameof(HasFormError));
            }
        }
    }

    public bool HasFormError => !string.IsNullOrEmpty(FormError);

    // Confirms, then records the decision; closes with true on success so the caller refreshes the list.
    private async Task DecideAsync(KycStatus decision)
    {
        FormError = string.Empty;

        var confirmed = decision == KycStatus.Verified
            ? _dialogService.Confirm(new ConfirmDialogOptions(
                Title: "Verify this customer?",
                Message: $"{Details.Customer.FullName} and all of their uploaded documents will be marked Verified.",
                ConfirmText: "Verify"))
            : _dialogService.Confirm(new ConfirmDialogOptions(
                Title: "Reject this customer?",
                Message: $"{Details.Customer.FullName}'s KYC status will be set to Rejected.",
                ConfirmText: "Reject",
                IsDestructive: true));

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;

            await _customerService.ReviewCustomerKycAsync(_customerId, new ReviewCustomerKycRequest(decision), CancellationToken.None);
        }
        catch (AppException exception)
        {
            FormError = exception.Message;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        CloseRequested?.Invoke(true);
    }
}
