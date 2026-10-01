using System.Windows.Media;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Customers;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Customers;

/// <summary>
/// Read-only detail card for one customer, opened by clicking a table row's "View" action.
/// Closes with <c>false</c> on Close (there is no edit path yet, unlike the equivalent User dialog).
/// </summary>
public sealed class CustomerDetailsViewModel : ViewModelBase, IDialogViewModel
{
    /// <summary>
    /// <paramref name="photo"/> is the customer's Photo document, already downloaded and decoded by the
    /// caller (see CustomerListViewModel.TryLoadPhotoAsync); null shows the default icon instead.
    /// </summary>
    public CustomerDetailsViewModel(CustomerResponse customer, ImageSource? photo, ICustomerService customerService, IDialogService dialogService)
    {
        Customer = customer;
        Photo = photo;
        Documents = customer.Documents
            .Select(document => new CustomerDocumentRowViewModel(customer.Id, document, customerService, dialogService))
            .ToList();

        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
    }

    public event Action<bool>? CloseRequested;

    // Nothing to lose on a detail card, so clicking outside closes it.
    public bool CanCloseOnBackdropClick => true;

    public bool CanCancel => true;

    public CustomerResponse Customer { get; }

    /// <summary>The customer's Photo document, or null to show the default icon.</summary>
    public ImageSource? Photo { get; }

    public bool HasPhoto => Photo is not null;

    public IReadOnlyList<CustomerDocumentRowViewModel> Documents { get; }

    public bool HasDocuments => Documents.Count > 0;

    public RelayCommand CloseCommand { get; }

    public string CustomerTypeText => Customer.CustomerType.ToString();

    public string DateOfBirthText => Customer.DateOfBirth.ToString(DisplayFormats.Date);


    public string NrcNumberText => TextOrEmpty(Customer.NrcNumber);

    public string PassportNumberText => TextOrEmpty(Customer.PassportNumber);

    public string NationalityText => TextOrEmpty(Customer.Nationality);

    public string PhoneText => TextOrEmpty(Customer.Phone);

    public string EmailText => TextOrEmpty(Customer.Email);

    public string OccupationText => TextOrEmpty(Customer.Occupation);

    /// <summary>Every supplied address part joined with ", ", or the empty-value placeholder.</summary>
    public string AddressText
    {
        get
        {
            var parts = new[] { Customer.AddressLine1, Customer.AddressLine2, Customer.City, Customer.State, Customer.PostalCode, Customer.Country }
                .Where(part => !string.IsNullOrWhiteSpace(part));

            var address = string.Join(", ", parts);

            return address.Length == 0 ? DisplayFormats.EmptyValue : address;
        }
    }

    public string KycStatusText => Customer.KycStatus.ToString();

    public string RiskLevelText => Customer.RiskLevel.ToString();

    public string StatusText => Customer.Status;

    public bool IsActive => Customer.Status == "Active";

    public string CreatedText => DateTimeDisplay.ToLocal(Customer.CreatedAt).ToString(DisplayFormats.Date);


    private static string TextOrEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DisplayFormats.EmptyValue : value;
    }
}
