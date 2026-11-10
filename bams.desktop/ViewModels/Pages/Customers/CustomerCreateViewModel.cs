using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
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
    private readonly INrcLookupClientService _nrcLookupService;

    // Matches CustomerFieldRules.NrcNumberRegexPattern's shape, but with capture groups so an existing
    // NrcNumber (edit mode) can be decomposed back into the four combo/text selections below.
    private static readonly Regex NrcNumberPartsPattern = new(@"^(\d{2})/([A-Z]{3,6})\(([NEP])\)(\d{6})$");

    private CustomerTypeOption _selectedCustomerType = CustomerTypeOption.All[0];
    private string _fullName = string.Empty;
    private DateTime? _dateOfBirth;
    private string _nrcNumber = string.Empty;
    private NrcStateResponse? _selectedNrcState;
    private NrcTownshipResponse? _selectedNrcTownship;
    private NrcTypeOption? _selectedNrcType;
    private string _nrcSerialNumber = string.Empty;
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

    private string _emailError = string.Empty;
    private string _nationalityError = string.Empty;
    private string _phoneError = string.Empty;
    private string _addressLine1Error = string.Empty;
    private string _cityError = string.Empty;
    private string _stateError = string.Empty;
    private string _countryError = string.Empty;
    private string _formError = string.Empty;

    private bool _isBusy;
    private long? _editingCustomerId;

    public CustomerCreateViewModel(ICustomerService customerService, IDialogService dialogService, INrcLookupClientService nrcLookupService)
    {
        _customerService = customerService;
        _dialogService = dialogService;
        _nrcLookupService = nrcLookupService;

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

    /// <summary>True after <see cref="LoadForEditAsync"/>; Save then updates the customer instead of creating one.</summary>
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

    // The single string actually sent to and validated by the server; built from the four selections
    // below rather than typed directly (see RecomposeNrcNumber).
    public string NrcNumber
    {
        get => _nrcNumber;
        set
        {
            if (SetProperty(ref _nrcNumber, value))
            {
                NrcNumberError = string.Empty;
                OnPropertyChanged(nameof(HasComposedNrcNumber));
            }
        }
    }

    public bool HasComposedNrcNumber => !string.IsNullOrEmpty(NrcNumber);

    public ObservableCollection<NrcStateResponse> NrcStates { get; } = [];

    public ObservableCollection<NrcTownshipResponse> NrcTownships { get; } = [];

    public IReadOnlyList<NrcTypeOption> NrcTypeOptions => NrcTypeOption.All;

    public int NrcSerialNumberLength => CustomerFieldRules.NrcSerialNumberLength;

    public NrcStateResponse? SelectedNrcState
    {
        get => _selectedNrcState;
        set
        {
            if (SetProperty(ref _selectedNrcState, value))
            {
                // A different state invalidates whatever township was selected for the previous one.
                SelectedNrcTownship = null;
                NrcTownships.Clear();
                RecomposeNrcNumber();
                _ = LoadNrcTownshipsForSelectedStateAsync();
            }
        }
    }

    public NrcTownshipResponse? SelectedNrcTownship
    {
        get => _selectedNrcTownship;
        set
        {
            if (SetProperty(ref _selectedNrcTownship, value))
            {
                RecomposeNrcNumber();
            }
        }
    }

    public NrcTypeOption? SelectedNrcType
    {
        get => _selectedNrcType;
        set
        {
            if (SetProperty(ref _selectedNrcType, value))
            {
                RecomposeNrcNumber();
            }
        }
    }

    public string NrcSerialNumber
    {
        get => _nrcSerialNumber;
        set
        {
            if (SetProperty(ref _nrcSerialNumber, value))
            {
                RecomposeNrcNumber();
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
        set
        {
            if (SetProperty(ref _email, value))
            {
                EmailError = string.Empty;
            }
        }
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


    public string EmailError
    {
        get => _emailError;
        private set
        {
            if (SetProperty(ref _emailError, value))
            {
                OnPropertyChanged(nameof(HasEmailError));
            }
        }
    }

    public bool HasEmailError => !string.IsNullOrEmpty(EmailError);
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

    private void ClearValidationErrors()
    {
        FullNameError = string.Empty;
        DateOfBirthError = string.Empty;
        NrcNumberError = string.Empty;
        PassportNumberError = string.Empty;

        NationalityError = string.Empty;
        EmailError = string.Empty;
        PhoneError = string.Empty;

        AddressLine1Error = string.Empty;
        CityError = string.Empty;
        StateError = string.Empty;
        CountryError = string.Empty;

        FormError = string.Empty;
    }
    public void Reset()
    {
        _editingCustomerId = null;

        SelectedCustomerType = CustomerTypeOptions[0];
        FullName = string.Empty;
        DateOfBirth = null;
        NrcNumber = string.Empty;
        SelectedNrcState = null;
        SelectedNrcTownship = null;
        SelectedNrcType = null;
        NrcSerialNumber = string.Empty;
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
        ClearValidationErrors();

        foreach (var document in AllDocuments)
        {
            document.Reset();
        }

        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    /// <summary>Fills every field from an existing customer, so Save then updates it instead of creating one.
    /// Also loads the NRC state list and, for a citizen, best-effort decomposes the stored NrcNumber back
    /// into the State/Township/Type/Serial selections (see TryRestoreNrcSelectionAsync).</summary>
    public async Task LoadForEditAsync(CustomerResponse customer)
    {
        _editingCustomerId = customer.Id;

        SelectedCustomerType = CustomerTypeOptions.First(option => option.Value == customer.CustomerType);
        FullName = customer.FullName;
        DateOfBirth = customer.DateOfBirth.ToDateTime(TimeOnly.MinValue);
        SelectedNrcState = null;
        SelectedNrcTownship = null;
        SelectedNrcType = null;
        NrcSerialNumber = string.Empty;
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

        await LoadNrcStatesAsync();
        if (IsCitizen)
        {
            await TryRestoreNrcSelectionAsync(customer.NrcNumber);
        }
    }

    /// <summary>Loads the NRC state/region reference list for the NRC-number builder. Called once when
    /// the form opens (both create and edit); a failure is shown in the form banner rather than left silent.</summary>
    public async Task LoadNrcStatesAsync()
    {
        try
        {
            var states = await _nrcLookupService.GetStatesAsync(CancellationToken.None);
            NrcStates.Clear();
            foreach (var state in states)
            {
                NrcStates.Add(state);
            }
        }
        catch (AppException exception)
        {
            FormError = exception.Message;
        }
    }

    // Reloads the township list whenever the selected state changes; fire-and-forget from the
    // SelectedNrcState setter (a ComboBox selection, not an awaitable user action), matching the
    // "async void is intentional: an event handler" pattern already used elsewhere in this codebase.
    // Never throws: LoadNrcTownshipsAsync already turns a failure into FormError.
    private async Task LoadNrcTownshipsForSelectedStateAsync()
    {
        if (SelectedNrcState is not null)
        {
            await LoadNrcTownshipsAsync(SelectedNrcState.StateCode);
        }
    }

    private async Task LoadNrcTownshipsAsync(string stateCode)
    {
        try
        {
            var townships = await _nrcLookupService.GetTownshipsAsync(stateCode, CancellationToken.None);
            NrcTownships.Clear();
            foreach (var township in townships)
            {
                NrcTownships.Add(township);
            }
        }
        catch (AppException exception)
        {
            FormError = exception.Message;
        }
    }

    // Edit mode: best-effort reconstruction of the structured NRC fields from the single stored string,
    // e.g. "9/AHMAYA(N)123456". Silently leaves the combo boxes empty when the value doesn't match the
    // standard format, or the matching state/township can no longer be found; NrcNumber itself (already
    // set from the customer record) is unaffected either way, so Save still sees the original value
    // unless the user changes a selection.
    private async Task TryRestoreNrcSelectionAsync(string? nrcNumber)
    {
        if (string.IsNullOrWhiteSpace(nrcNumber))
        {
            return;
        }

        var match = NrcNumberPartsPattern.Match(nrcNumber.Trim());
        if (!match.Success)
        {
            return;
        }

        var state = NrcStates.FirstOrDefault(candidate => candidate.StateCode.PadLeft(2, '0') == match.Groups[1].Value);
        if (state is null)
        {
            return;
        }

        // Set the backing fields directly (not the property setters) so this restoration does not
        // itself trigger RecomposeNrcNumber or a second, redundant township reload.
        _selectedNrcState = state;
        OnPropertyChanged(nameof(SelectedNrcState));

        await LoadNrcTownshipsAsync(state.StateCode);

        _selectedNrcTownship = NrcTownships.FirstOrDefault(township => township.TownshipCode == match.Groups[2].Value);
        OnPropertyChanged(nameof(SelectedNrcTownship));

        _selectedNrcType = NrcTypeOptions.FirstOrDefault(type => type.Code == match.Groups[3].Value);
        OnPropertyChanged(nameof(SelectedNrcType));

        _nrcSerialNumber = match.Groups[4].Value;
        OnPropertyChanged(nameof(NrcSerialNumber));
    }

    // Rebuilds the single NrcNumber string (the field actually sent to and validated by the server) from
    // the four structured selections, e.g. State "9" + Township "AHMAYA" + Type "N" + "123456" becomes
    // "09/AHMAYA(N)123456". Left blank while any part is still missing.
    private void RecomposeNrcNumber()
    {
        if (SelectedNrcState is null || SelectedNrcTownship is null || SelectedNrcType is null
            || NrcSerialNumber.Length != CustomerFieldRules.NrcSerialNumberLength)
        {
            NrcNumber = string.Empty;
            return;
        }

        NrcNumber = $"{SelectedNrcState.StateCode.PadLeft(2, '0')}/{SelectedNrcTownship.TownshipCode}({SelectedNrcType.Code}){NrcSerialNumber}";
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

        var nrc = NrcNumber.Trim();

        NrcNumberError = !IsCitizen
        ? string.Empty
        : string.IsNullOrWhiteSpace(nrc)
            ? MessageCatalog.GetMessage(MessageCode.NrcNumberRequired)
            : !Regex.IsMatch(nrc, CustomerFieldRules.NrcNumberRegexPattern)
                ? MessageCatalog.GetMessage(MessageCode.NrcNumberInvalid)
                : string.Empty;


        var passport = PassportNumber.Trim();
        PassportNumberError = !IsForeigner
        ? string.Empty
        : string.IsNullOrWhiteSpace(passport)
            ? MessageCatalog.GetMessage(MessageCode.PassportNumberRequired)
            : !Regex.IsMatch(passport, CustomerFieldRules.PassportNumberRegexPattern)
                ? MessageCatalog.GetMessage(MessageCode.PassportNumberInvalid)
                : string.Empty;

        var email = Email.Trim();

        EmailError = !string.IsNullOrWhiteSpace(email)
        && !Regex.IsMatch(email, CustomerFieldRules.CustomerEmailRegexPattern)
            ? "Please enter a valid email address."
            : string.Empty;

        NationalityError = Nationality.Trim().Length == 0 ? "Please enter the nationality." : string.Empty;
        // PhoneError = Phone.Trim().Length == 0 ? "Please enter a phone number." : string.Empty 
        //     : !Regex.IsMatch(Phone.Trim(), CustomerFieldRules.PhoneNumberRegexPattern)
        //         ? "Please enter a valid phone number."
        //         : string.Empty;

        PhoneError = !string.IsNullOrWhiteSpace(Phone.Trim())
        && !Regex.IsMatch(Phone.Trim(), CustomerFieldRules.PhoneNumberRegexPattern)
            ? "Please enter a valid phone number."
            : string.Empty;
        AddressLine1Error = AddressLine1.Trim().Length == 0 ? "Please enter the address." : string.Empty;
        CityError = City.Trim().Length == 0 ? "Please enter the city." : string.Empty;
        StateError = State.Trim().Length == 0 ? "Please enter the state or region." : string.Empty;
        CountryError = Country.Trim().Length == 0 ? "Please enter the country." : string.Empty;

        // Runs Validate() on every document (not just the required ones) so a stale error clears
        // when a document stops being required; '&' (not '&&') deliberately evaluates every one.
        var documentsValid = AllDocuments.Aggregate(true, (valid, document) => document.Validate() & valid);

        return !HasFullNameError && !HasDateOfBirthError && !HasNrcNumberError && !HasPassportNumberError
            && !HasNationalityError && !HasPhoneError && !HasEmailError && !HasAddressLine1Error && !HasCityError
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
            case MessageCode.NrcNumberInvalid:
                NrcNumberError = exception.Message;
                break;
            case MessageCode.PassportNumberRequired:
            case MessageCode.PassportNumberInvalid:
                PassportNumberError = exception.Message;
                break;
            default:
                FormError = exception.Message;
                break;
        }
    }
}
