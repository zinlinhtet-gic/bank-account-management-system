using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Customers;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Customers;

/// <summary>
/// The create-customer form: customer information plus the 8 document cards. Confirms with the user,
/// then creates the customer; <see cref="Completed"/> tells the host page to return to the list.
/// </summary>
public sealed class CustomerCreateViewModel : ViewModelBase
{
    private readonly ICustomerService _customerService;
    private readonly IDialogService _dialogService;

    private CustomerTypeOption _selectedCustomerType = CustomerTypeOption.All[0];
    private string _fullName = string.Empty;
    private DateTime? _dateOfBirth;
    private string _nrcNumber = string.Empty;
    private string _passportNumber = string.Empty;
    private string _nationality = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private string _occupation = string.Empty;
    private string _addressLine1 = string.Empty;
    private string _addressLine2 = string.Empty;
    private string _city = string.Empty;
    private string _state = string.Empty;
    private string _postalCode = string.Empty;
    private string _country = string.Empty;

    private string _fullNameError = string.Empty;
    private string _dateOfBirthError = string.Empty;
    private string _nrcNumberError = string.Empty;
    private string _passportNumberError = string.Empty;
    private string _nationalityError = string.Empty;
    private string _phoneError = string.Empty;
    private string _addressLine1Error = string.Empty;
    private string _cityError = string.Empty;
    private string _stateError = string.Empty;
    private string _countryError = string.Empty;
    private string _formError = string.Empty;

    private bool _isBusy;
    private long? _editingCustomerId;

    public CustomerCreateViewModel(ICustomerService customerService, IDialogService dialogService)
    {
        _customerService = customerService;
        _dialogService = dialogService;

        // Only the identity document matching the selected customer type starts out required;
        // SelectedCustomerType keeps the two in sync as it changes.
        NrcDocument = new DocumentFormViewModel(DocumentType.Nrc, "NRC", dialogService, isRequired: true);
        PassportDocument = new DocumentFormViewModel(DocumentType.Passport, "Passport", dialogService, isRequired: false);
        VisaDocument = new DocumentFormViewModel(DocumentType.Visa, "Visa", dialogService, isRequired: false);
        HouseholdRegistrationDocument = new DocumentFormViewModel(DocumentType.HouseholdRegistration, "Household Registration", dialogService, isRequired: true);
        ProofOfAddressDocument = new DocumentFormViewModel(DocumentType.ProofOfAddress, "Proof of Address", dialogService, isRequired: false);
        TaxDocumentDocument = new DocumentFormViewModel(DocumentType.TaxDocument, "Tax Document", dialogService, isRequired: false);
        SourceOfFundsDocument = new DocumentFormViewModel(DocumentType.SourceOfFunds, "Source of Funds", dialogService, isRequired: false);
        PhotoDocument = new DocumentFormViewModel(DocumentType.Photo, "Photo", dialogService, isRequired: true);

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => Completed?.Invoke(this, EventArgs.Empty), _ => !IsBusy);
    }

    /// <summary>Raised after Cancel, or after a successful Save; the host page returns to the customer list.</summary>
    public event EventHandler? Completed;

    /// <summary>True after <see cref="LoadForEdit"/>; Save then updates the customer instead of creating one.</summary>
    public bool IsEditMode => _editingCustomerId is not null;

    public string Title => IsEditMode ? "Edit Customer" : "Create New Customer";

    public string SaveButtonText => IsEditMode ? "Save Changes" : "Save Customer";

    public IReadOnlyList<CustomerTypeOption> CustomerTypeOptions => CustomerTypeOption.All;

    public CustomerTypeOption SelectedCustomerType
    {
        get => _selectedCustomerType;
        set
        {
            if (value is not null && SetProperty(ref _selectedCustomerType, value))
            {
                OnPropertyChanged(nameof(IsCitizen));
                OnPropertyChanged(nameof(IsForeigner));

                // Only the identity document that applies to the new type is required from here on.
                NrcDocument.IsRequired = IsCitizen;
                PassportDocument.IsRequired = IsForeigner;
            }
        }
    }

    public bool IsCitizen => SelectedCustomerType.Value == CustomerType.Citizen;

    public bool IsForeigner => !IsCitizen;

    public string FullName
    {
        get => _fullName;
        set
        {
            if (SetProperty(ref _fullName, value))
            {
                FullNameError = string.Empty;
            }
        }
    }

    public DateTime? DateOfBirth
    {
        get => _dateOfBirth;
        set
        {
            if (SetProperty(ref _dateOfBirth, value))
            {
                DateOfBirthError = string.Empty;
            }
        }
    }

    public string NrcNumber
    {
        get => _nrcNumber;
        set
        {
            if (SetProperty(ref _nrcNumber, value))
            {
                NrcNumberError = string.Empty;
            }
        }
    }

    public string PassportNumber
    {
        get => _passportNumber;
        set
        {
            if (SetProperty(ref _passportNumber, value))
            {
                PassportNumberError = string.Empty;
            }
        }
    }

    public string Nationality
    {
        get => _nationality;
        set
        {
            if (SetProperty(ref _nationality, value))
            {
                NationalityError = string.Empty;
            }
        }
    }

    public string Phone
    {
        get => _phone;
        set
        {
            if (SetProperty(ref _phone, value))
            {
                PhoneError = string.Empty;
            }
        }
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Occupation
    {
        get => _occupation;
        set => SetProperty(ref _occupation, value);
    }

    public string AddressLine1
    {
        get => _addressLine1;
        set
        {
            if (SetProperty(ref _addressLine1, value))
            {
                AddressLine1Error = string.Empty;
            }
        }
    }

    public string AddressLine2
    {
        get => _addressLine2;
        set => SetProperty(ref _addressLine2, value);
    }

    public string City
    {
        get => _city;
        set
        {
            if (SetProperty(ref _city, value))
            {
                CityError = string.Empty;
            }
        }
    }

    public string State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                StateError = string.Empty;
            }
        }
    }

    public string PostalCode
    {
        get => _postalCode;
        set => SetProperty(ref _postalCode, value);
    }

    public string Country
    {
        get => _country;
        set
        {
            if (SetProperty(ref _country, value))
            {
                CountryError = string.Empty;
            }
        }
    }

    public DocumentFormViewModel NrcDocument { get; }

    public DocumentFormViewModel PassportDocument { get; }

    public DocumentFormViewModel VisaDocument { get; }

    public DocumentFormViewModel HouseholdRegistrationDocument { get; }

    public DocumentFormViewModel ProofOfAddressDocument { get; }

    public DocumentFormViewModel TaxDocumentDocument { get; }

    public DocumentFormViewModel SourceOfFundsDocument { get; }

    public DocumentFormViewModel PhotoDocument { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public string FullNameError
    {
        get => _fullNameError;
        private set
        {
            if (SetProperty(ref _fullNameError, value))
            {
                OnPropertyChanged(nameof(HasFullNameError));
            }
        }
    }

    public bool HasFullNameError => !string.IsNullOrEmpty(FullNameError);

    public string DateOfBirthError
    {
        get => _dateOfBirthError;
        private set
        {
            if (SetProperty(ref _dateOfBirthError, value))
            {
                OnPropertyChanged(nameof(HasDateOfBirthError));
            }
        }
    }

    public bool HasDateOfBirthError => !string.IsNullOrEmpty(DateOfBirthError);

    public string NrcNumberError
    {
        get => _nrcNumberError;
        private set
        {
            if (SetProperty(ref _nrcNumberError, value))
            {
                OnPropertyChanged(nameof(HasNrcNumberError));
            }
        }
    }

    public bool HasNrcNumberError => !string.IsNullOrEmpty(NrcNumberError);

    public string PassportNumberError
    {
        get => _passportNumberError;
        private set
        {
            if (SetProperty(ref _passportNumberError, value))
            {
                OnPropertyChanged(nameof(HasPassportNumberError));
            }
        }
    }

    public bool HasPassportNumberError => !string.IsNullOrEmpty(PassportNumberError);

    public string NationalityError
    {
        get => _nationalityError;
        private set
        {
            if (SetProperty(ref _nationalityError, value))
            {
                OnPropertyChanged(nameof(HasNationalityError));
            }
        }
    }

    public bool HasNationalityError => !string.IsNullOrEmpty(NationalityError);

    public string PhoneError
    {
        get => _phoneError;
        private set
        {
            if (SetProperty(ref _phoneError, value))
            {
                OnPropertyChanged(nameof(HasPhoneError));
            }
        }
    }

    public bool HasPhoneError => !string.IsNullOrEmpty(PhoneError);

    public string AddressLine1Error
    {
        get => _addressLine1Error;
        private set
        {
            if (SetProperty(ref _addressLine1Error, value))
            {
                OnPropertyChanged(nameof(HasAddressLine1Error));
            }
        }
    }

    public bool HasAddressLine1Error => !string.IsNullOrEmpty(AddressLine1Error);

    public string CityError
    {
        get => _cityError;
        private set
        {
            if (SetProperty(ref _cityError, value))
            {
                OnPropertyChanged(nameof(HasCityError));
            }
        }
    }

    public bool HasCityError => !string.IsNullOrEmpty(CityError);

    public string StateError
    {
        get => _stateError;
        private set
        {
            if (SetProperty(ref _stateError, value))
            {
                OnPropertyChanged(nameof(HasStateError));
            }
        }
    }

    public bool HasStateError => !string.IsNullOrEmpty(StateError);

    public string CountryError
    {
        get => _countryError;
        private set
        {
            if (SetProperty(ref _countryError, value))
            {
                OnPropertyChanged(nameof(HasCountryError));
            }
        }
    }

    public bool HasCountryError => !string.IsNullOrEmpty(CountryError);

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

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                SaveCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    /// <summary>Resets every field back to its default, so the form starts empty the next time it opens
    /// in create mode.</summary>
    public void Reset()
    {
        _editingCustomerId = null;

        SelectedCustomerType = CustomerTypeOptions[0];
        FullName = string.Empty;
        DateOfBirth = null;
        NrcNumber = string.Empty;
        PassportNumber = string.Empty;
        Nationality = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Occupation = string.Empty;
        AddressLine1 = string.Empty;
        AddressLine2 = string.Empty;
        City = string.Empty;
        State = string.Empty;
        PostalCode = string.Empty;
        Country = string.Empty;
        FormError = string.Empty;

        foreach (var document in AllDocuments)
        {
            document.Reset();
        }

        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    /// <summary>Fills every field from an existing customer, so Save then updates it instead of creating one.</summary>
    public void LoadForEdit(CustomerResponse customer)
    {
        _editingCustomerId = customer.Id;

        SelectedCustomerType = CustomerTypeOptions.First(option => option.Value == customer.CustomerType);
        FullName = customer.FullName;
        DateOfBirth = customer.DateOfBirth.ToDateTime(TimeOnly.MinValue);
        NrcNumber = customer.NrcNumber ?? string.Empty;
        PassportNumber = customer.PassportNumber ?? string.Empty;
        Nationality = customer.Nationality ?? string.Empty;
        Phone = customer.Phone ?? string.Empty;
        Email = customer.Email ?? string.Empty;
        Occupation = customer.Occupation ?? string.Empty;
        AddressLine1 = customer.AddressLine1 ?? string.Empty;
        AddressLine2 = customer.AddressLine2 ?? string.Empty;
        City = customer.City ?? string.Empty;
        State = customer.State ?? string.Empty;
        PostalCode = customer.PostalCode ?? string.Empty;
        Country = customer.Country ?? string.Empty;
        FormError = string.Empty;

        foreach (var document in AllDocuments)
        {
            var existing = customer.Documents.FirstOrDefault(response => response.DocumentType == document.DocumentType);
            if (existing is not null)
            {
                document.LoadExisting(existing);
            }
        }

        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    // Validates locally, confirms with the user, then creates or updates the customer.
    private async Task SaveAsync()
    {
        FormError = string.Empty;

        if (!ValidateFields())
        {
            return;
        }

        var confirmed = IsEditMode
            ? _dialogService.Confirm(new ConfirmDialogOptions(
                Title: "Save changes to this customer?",
                Message: $"{FullName.Trim()}'s record will be updated.",
                ConfirmText: "Save changes"))
            : _dialogService.Confirm(new ConfirmDialogOptions(
                Title: "Create this customer?",
                Message: $"A new {SelectedCustomerType.DisplayName.ToLowerInvariant()} customer record for {FullName.Trim()} will be created.",
                ConfirmText: "Create customer"));

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;

            if (IsEditMode)
            {
                await _customerService.UpdateCustomerAsync(_editingCustomerId!.Value, BuildUpdateRequest(), CancellationToken.None);
            }
            else
            {
                await _customerService.CreateCustomerAsync(BuildRequest(), CancellationToken.None);
            }
        }
        catch (AppException exception)
        {
            ShowServerError(exception);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        Completed?.Invoke(this, EventArgs.Empty);
    }

    // Shows every field error at once so the user can fix them in one go; returns true when all are valid.
    private bool ValidateFields()
    {
        FullNameError = FullName.Trim().Length == 0
            ? MessageCatalog.GetMessage(MessageCode.CustomerFullNameRequired)
            : FullName.Trim().Length > CustomerFieldRules.FullNameMaximumLength
                ? MessageCatalog.GetMessage(MessageCode.FieldTooLong)
                : string.Empty;

        DateOfBirthError = ValidateDateOfBirth();

        NrcNumberError = IsCitizen && NrcNumber.Trim().Length == 0
            ? MessageCatalog.GetMessage(MessageCode.NrcNumberRequired)
            : string.Empty;

        PassportNumberError = IsForeigner && PassportNumber.Trim().Length == 0
            ? MessageCatalog.GetMessage(MessageCode.PassportNumberRequired)
            : string.Empty;

        NationalityError = Nationality.Trim().Length == 0 ? "Please enter the nationality." : string.Empty;
        PhoneError = Phone.Trim().Length == 0 ? "Please enter a phone number." : string.Empty;
        AddressLine1Error = AddressLine1.Trim().Length == 0 ? "Please enter the address." : string.Empty;
        CityError = City.Trim().Length == 0 ? "Please enter the city." : string.Empty;
        StateError = State.Trim().Length == 0 ? "Please enter the state or region." : string.Empty;
        CountryError = Country.Trim().Length == 0 ? "Please enter the country." : string.Empty;

        // Runs Validate() on every document (not just the required ones) so a stale error clears
        // when a document stops being required; '&' (not '&&') deliberately evaluates every one.
        var documentsValid = AllDocuments.Aggregate(true, (valid, document) => document.Validate() & valid);

        return !HasFullNameError && !HasDateOfBirthError && !HasNrcNumberError && !HasPassportNumberError
            && !HasNationalityError && !HasPhoneError && !HasAddressLine1Error && !HasCityError
            && !HasStateError && !HasCountryError && documentsValid;
    }

    // Future date and below-minimum-age both count as invalid, distinctly worded.
    private string ValidateDateOfBirth()
    {
        if (DateOfBirth is null)
        {
            return "Please choose a date of birth.";
        }

        var dateOfBirth = DateOfBirth.Value.Date;
        if (dateOfBirth > DateTime.Today)
        {
            return MessageCatalog.GetMessage(MessageCode.CustomerDateOfBirthInvalid);
        }

        var age = DateTime.Today.Year - dateOfBirth.Year;
        if (dateOfBirth > DateTime.Today.AddYears(-age))
        {
            age--;
        }

        return age < CustomerFieldRules.MinimumAgeYears
            ? MessageCatalog.GetMessage(MessageCode.CustomerBelowMinimumAge)
            : string.Empty;
    }

    private IEnumerable<DocumentFormViewModel> AllDocuments =>
    [
        NrcDocument, PassportDocument, VisaDocument, HouseholdRegistrationDocument,
        ProofOfAddressDocument, TaxDocumentDocument, SourceOfFundsDocument, PhotoDocument
    ];

    private CreateCustomerRequest BuildRequest()
    {
        var documents = AllDocuments
            .Where(document => document.FilePath is not null)
            .Select(document => document.ToRequest())
            .ToList();

        return new CreateCustomerRequest(
            SelectedCustomerType.Value,
            FullName.Trim(),
            DateOnly.FromDateTime(DateOfBirth!.Value.Date),
            Trimmed(Nationality),
            Trimmed(NrcNumber),
            Trimmed(PassportNumber),
            Trimmed(Phone),
            Trimmed(Occupation),
            Trimmed(AddressLine1),
            Trimmed(AddressLine2),
            Trimmed(City),
            Trimmed(State),
            Trimmed(PostalCode),
            Trimmed(Country),
            Trimmed(Email),
            documents.Count == 0 ? null : documents);
    }

    private UpdateCustomerRequest BuildUpdateRequest()
    {
        // Only a document that already exists, or that just had a new file chosen, is worth sending;
        // a still-empty optional document is simply left out (nothing to add or edit).
        var documents = AllDocuments
            .Where(document => document.ExistingDocumentId is not null || document.FilePath is not null)
            .Select(document => document.ToUpdateRequest())
            .ToList();

        return new UpdateCustomerRequest(
            SelectedCustomerType.Value,
            FullName.Trim(),
            DateOnly.FromDateTime(DateOfBirth!.Value.Date),
            Trimmed(Nationality),
            Trimmed(NrcNumber),
            Trimmed(PassportNumber),
            Trimmed(Phone),
            Trimmed(Occupation),
            Trimmed(AddressLine1),
            Trimmed(AddressLine2),
            Trimmed(City),
            Trimmed(State),
            Trimmed(PostalCode),
            Trimmed(Country),
            Trimmed(Email),
            documents.Count == 0 ? null : documents);
    }

    private static string? Trimmed(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // Puts a server rejection next to the field it is about; anything else goes to the banner.
    private void ShowServerError(AppException exception)
    {
        switch (exception.Code)
        {
            case MessageCode.CustomerFullNameRequired:
                FullNameError = exception.Message;
                break;
            case MessageCode.CustomerDateOfBirthInvalid:
            case MessageCode.CustomerBelowMinimumAge:
                DateOfBirthError = exception.Message;
                break;
            case MessageCode.NrcNumberRequired:
                NrcNumberError = exception.Message;
                break;
            case MessageCode.PassportNumberRequired:
                PassportNumberError = exception.Message;
                break;
            default:
                FormError = exception.Message;
                break;
        }
    }
}
