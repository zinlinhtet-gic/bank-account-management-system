using System.Globalization;
using bams.desktop.Commands;
using bams.desktop.DTOs.Configuration;
using bams.desktop.Exceptions;
using bams.desktop.Models.Configuration;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// <summary>
/// The create / edit bank policy (account type) form shown in a modal. Closes itself with <c>true</c> after a
/// successful save; <see cref="SavedBankPolicy"/> holds the result.
/// </summary>
public sealed class BankPolicyFormViewModel : ViewModelBase, IDialogViewModel
{
    public static readonly IReadOnlyList<string> Statuses = ["Active", "Inactive"];

    /// <summary>Bindable alias of <see cref="Statuses"/> for the status drop-down.</summary>
    public IReadOnlyList<string> StatusOptions => Statuses;

    public IReadOnlyList<AccountTypeCategoryOption> CategoryOptions => AccountTypeCategoryOption.All;

    private readonly IBankPolicyService _bankPolicyService;
    private readonly BankPolicyResponse? _existingPolicy;

    private string _codeText = string.Empty;
    private string _nameText = string.Empty;
    private AccountTypeCategoryOption? _selectedCategory;
    private string _minimumOpeningBalanceText = string.Empty;
    private string _minimumMaintainedBalanceText = string.Empty;
    private string _dailyTransactionLimitText = string.Empty;
    private string _monthlyTransactionLimitText = string.Empty;
    private bool _allowWithdrawal = true;
    private bool _allowTransfer = true;
    private bool _allowPartialWithdrawal = true;
    private bool _allowCitizen = true;
    private bool _allowForeigner = true;
    private string _citizenRequiredReferText = "0";
    private string _foreignRequiredReferText = "0";
    private string? _selectedStatus;

    private string _codeError = string.Empty;
    private string _nameError = string.Empty;
    private string _categoryError = string.Empty;
    private string _minimumOpeningBalanceError = string.Empty;
    private string _minimumMaintainedBalanceError = string.Empty;
    private string _dailyTransactionLimitError = string.Empty;
    private string _monthlyTransactionLimitError = string.Empty;
    private string _citizenRequiredReferError = string.Empty;
    private string _foreignRequiredReferError = string.Empty;
    private string _statusError = string.Empty;
    private string _formError = string.Empty;
    private bool _isBusy;

    /// <summary>
    /// Creates the form. Pass <paramref name="existingPolicy"/> to edit that policy; null creates a new one.
    /// </summary>
    public BankPolicyFormViewModel(
        IBankPolicyService bankPolicyService,
        BankPolicyResponse? existingPolicy)
    {
        _bankPolicyService = bankPolicyService;
        _existingPolicy = existingPolicy;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false), _ => CanCancel);

        if (existingPolicy is not null)
        {
            FillFromExistingPolicy(existingPolicy);
        }
    }

    public event Action<bool>? CloseRequested;

    // A stray click outside must not throw away what was typed.
    public bool CanCloseOnBackdropClick => false;

    public bool CanCancel => !IsBusy;

    public bool IsEditMode => _existingPolicy is not null;

    public string Title => IsEditMode ? "Edit bank policy" : "Create bank policy";

    public string Subtitle => IsEditMode
        ? "Update this account type's terms and limits."
        : "Add a new account type and its terms.";

    public string SaveText => IsEditMode ? "Save changes" : "Create policy";

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// <summary>The saved policy, set just before the dialog closes with success.</summary>
    public BankPolicyResponse? SavedBankPolicy { get; private set; }

    public string CodeText
    {
        get => _codeText;
        set
        {
            if (SetProperty(ref _codeText, value))
            {
                CodeError = string.Empty;
            }
        }
    }

    public string NameText
    {
        get => _nameText;
        set
        {
            if (SetProperty(ref _nameText, value))
            {
                NameError = string.Empty;
            }
        }
    }

    public AccountTypeCategoryOption? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                CategoryError = string.Empty;
            }
        }
    }

    public string MinimumOpeningBalanceText
    {
        get => _minimumOpeningBalanceText;
        set
        {
            if (SetProperty(ref _minimumOpeningBalanceText, value))
            {
                MinimumOpeningBalanceError = string.Empty;
            }
        }
    }

    public string MinimumMaintainedBalanceText
    {
        get => _minimumMaintainedBalanceText;
        set
        {
            if (SetProperty(ref _minimumMaintainedBalanceText, value))
            {
                MinimumMaintainedBalanceError = string.Empty;
            }
        }
    }

    public string DailyTransactionLimitText
    {
        get => _dailyTransactionLimitText;
        set
        {
            if (SetProperty(ref _dailyTransactionLimitText, value))
            {
                DailyTransactionLimitError = string.Empty;
            }
        }
    }

    public string MonthlyTransactionLimitText
    {
        get => _monthlyTransactionLimitText;
        set
        {
            if (SetProperty(ref _monthlyTransactionLimitText, value))
            {
                MonthlyTransactionLimitError = string.Empty;
            }
        }
    }

    public bool AllowWithdrawal
    {
        get => _allowWithdrawal;
        set => SetProperty(ref _allowWithdrawal, value);
    }

    public bool AllowTransfer
    {
        get => _allowTransfer;
        set => SetProperty(ref _allowTransfer, value);
    }

    public bool AllowPartialWithdrawal
    {
        get => _allowPartialWithdrawal;
        set => SetProperty(ref _allowPartialWithdrawal, value);
    }

    public bool AllowCitizen
    {
        get => _allowCitizen;
        set => SetProperty(ref _allowCitizen, value);
    }

    public bool AllowForeigner
    {
        get => _allowForeigner;
        set => SetProperty(ref _allowForeigner, value);
    }

    public string CitizenRequiredReferText
    {
        get => _citizenRequiredReferText;
        set
        {
            if (SetProperty(ref _citizenRequiredReferText, value))
            {
                CitizenRequiredReferError = string.Empty;
            }
        }
    }

    public string ForeignRequiredReferText
    {
        get => _foreignRequiredReferText;
        set
        {
            if (SetProperty(ref _foreignRequiredReferText, value))
            {
                ForeignRequiredReferError = string.Empty;
            }
        }
    }

    public string? SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
            {
                StatusError = string.Empty;
            }
        }
    }

    public string CodeError
    {
        get => _codeError;
        private set
        {
            if (SetProperty(ref _codeError, value))
            {
                OnPropertyChanged(nameof(HasCodeError));
            }
        }
    }

    public bool HasCodeError => !string.IsNullOrEmpty(CodeError);

    public string NameError
    {
        get => _nameError;
        private set
        {
            if (SetProperty(ref _nameError, value))
            {
                OnPropertyChanged(nameof(HasNameError));
            }
        }
    }

    public bool HasNameError => !string.IsNullOrEmpty(NameError);

    public string CategoryError
    {
        get => _categoryError;
        private set
        {
            if (SetProperty(ref _categoryError, value))
            {
                OnPropertyChanged(nameof(HasCategoryError));
            }
        }
    }

    public bool HasCategoryError => !string.IsNullOrEmpty(CategoryError);

    public string MinimumOpeningBalanceError
    {
        get => _minimumOpeningBalanceError;
        private set
        {
            if (SetProperty(ref _minimumOpeningBalanceError, value))
            {
                OnPropertyChanged(nameof(HasMinimumOpeningBalanceError));
            }
        }
    }

    public bool HasMinimumOpeningBalanceError => !string.IsNullOrEmpty(MinimumOpeningBalanceError);

    public string MinimumMaintainedBalanceError
    {
        get => _minimumMaintainedBalanceError;
        private set
        {
            if (SetProperty(ref _minimumMaintainedBalanceError, value))
            {
                OnPropertyChanged(nameof(HasMinimumMaintainedBalanceError));
            }
        }
    }

    public bool HasMinimumMaintainedBalanceError => !string.IsNullOrEmpty(MinimumMaintainedBalanceError);

    public string DailyTransactionLimitError
    {
        get => _dailyTransactionLimitError;
        private set
        {
            if (SetProperty(ref _dailyTransactionLimitError, value))
            {
                OnPropertyChanged(nameof(HasDailyTransactionLimitError));
            }
        }
    }

    public bool HasDailyTransactionLimitError => !string.IsNullOrEmpty(DailyTransactionLimitError);

    public string MonthlyTransactionLimitError
    {
        get => _monthlyTransactionLimitError;
        private set
        {
            if (SetProperty(ref _monthlyTransactionLimitError, value))
            {
                OnPropertyChanged(nameof(HasMonthlyTransactionLimitError));
            }
        }
    }

    public bool HasMonthlyTransactionLimitError => !string.IsNullOrEmpty(MonthlyTransactionLimitError);

    public string CitizenRequiredReferError
    {
        get => _citizenRequiredReferError;
        private set
        {
            if (SetProperty(ref _citizenRequiredReferError, value))
            {
                OnPropertyChanged(nameof(HasCitizenRequiredReferError));
            }
        }
    }

    public bool HasCitizenRequiredReferError => !string.IsNullOrEmpty(CitizenRequiredReferError);

    public string ForeignRequiredReferError
    {
        get => _foreignRequiredReferError;
        private set
        {
            if (SetProperty(ref _foreignRequiredReferError, value))
            {
                OnPropertyChanged(nameof(HasForeignRequiredReferError));
            }
        }
    }

    public bool HasForeignRequiredReferError => !string.IsNullOrEmpty(ForeignRequiredReferError);

    public string StatusError
    {
        get => _statusError;
        private set
        {
            if (SetProperty(ref _statusError, value))
            {
                OnPropertyChanged(nameof(HasStatusError));
            }
        }
    }

    public bool HasStatusError => !string.IsNullOrEmpty(StatusError);

    /// <summary>Error that belongs to no single field.</summary>
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
                OnPropertyChanged(nameof(CanCancel));
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    private void FillFromExistingPolicy(BankPolicyResponse policy)
    {
        _codeText = policy.Code;
        _nameText = policy.Name;
        _selectedCategory = CategoryOptions.FirstOrDefault(option => option.Value == policy.Category);
        _minimumOpeningBalanceText = policy.MinimumOpeningBalance.ToString(CultureInfo.InvariantCulture);
        _minimumMaintainedBalanceText = policy.MinimumMaintainedBalance.ToString(CultureInfo.InvariantCulture);
        _dailyTransactionLimitText = policy.DailyTransactionLimit?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _monthlyTransactionLimitText = policy.MonthlyTransactionLimit?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _allowWithdrawal = policy.AllowWithdrawal;
        _allowTransfer = policy.AllowTransfer;
        _allowPartialWithdrawal = policy.AllowPartialWithdrawal;
        _allowCitizen = policy.AllowCitizen;
        _allowForeigner = policy.AllowForeigner;
        _citizenRequiredReferText = policy.CitizenRequiredRefer.ToString(CultureInfo.InvariantCulture);
        _foreignRequiredReferText = policy.ForeignRequiredRefer.ToString(CultureInfo.InvariantCulture);
        _selectedStatus = Statuses.FirstOrDefault(status => status == policy.Status) ?? policy.Status;
    }

    // Validates locally, then creates or updates the policy.
    private async Task SaveAsync()
    {
        FormError = string.Empty;

        if (!TryValidateFields(out var input))
        {
            return;
        }

        try
        {
            IsBusy = true;

            SavedBankPolicy = IsEditMode
                ? await _bankPolicyService.UpdateBankPolicyAsync(
                    _existingPolicy!.Id,
                    new UpdateBankPolicyRequest(
                        input.Code, input.Name, input.Category, input.MinimumOpeningBalance,
                        input.MinimumMaintainedBalance, input.DailyTransactionLimit, input.MonthlyTransactionLimit,
                        input.AllowWithdrawal, input.AllowTransfer, input.AllowPartialWithdrawal,
                        input.AllowCitizen, input.AllowForeigner, input.CitizenRequiredRefer,
                        input.ForeignRequiredRefer, input.Status),
                    CancellationToken.None)
                : await _bankPolicyService.CreateBankPolicyAsync(
                    new CreateBankPolicyRequest(
                        input.Code, input.Name, input.Category, input.MinimumOpeningBalance,
                        input.MinimumMaintainedBalance, input.DailyTransactionLimit, input.MonthlyTransactionLimit,
                        input.AllowWithdrawal, input.AllowTransfer, input.AllowPartialWithdrawal,
                        input.AllowCitizen, input.AllowForeigner, input.CitizenRequiredRefer,
                        input.ForeignRequiredRefer, input.Status),
                    CancellationToken.None);
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

        CloseRequested?.Invoke(true);
    }

    // Shows every field error at once so the user can fix them in one go; returns the parsed input when valid.
    private bool TryValidateFields(out PolicyInput input)
    {
        var code = CodeText.Trim();
        var name = NameText.Trim();

        CodeError = code.Length == 0
            ? "Please enter a code."
            : code.Length > 40
                ? MessageCatalog.GetMessage(MessageCode.FieldTooLong)
                : string.Empty;

        NameError = name.Length == 0
            ? "Please enter a name."
            : name.Length > 150
                ? MessageCatalog.GetMessage(MessageCode.FieldTooLong)
                : string.Empty;

        CategoryError = SelectedCategory is null
            ? "Please choose a category."
            : string.Empty;

        decimal minimumOpeningBalance = 0;
        if (!decimal.TryParse(MinimumOpeningBalanceText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out minimumOpeningBalance) || minimumOpeningBalance < 0)
        {
            MinimumOpeningBalanceError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
        }
        else
        {
            MinimumOpeningBalanceError = string.Empty;
        }

        decimal minimumMaintainedBalance = 0;
        if (!decimal.TryParse(MinimumMaintainedBalanceText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out minimumMaintainedBalance) || minimumMaintainedBalance < 0)
        {
            MinimumMaintainedBalanceError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
        }
        else if (minimumMaintainedBalance > minimumOpeningBalance)
        {
            MinimumMaintainedBalanceError = MessageCatalog.GetMessage(MessageCode.BankPolicyBalanceRangeInvalid);
        }
        else
        {
            MinimumMaintainedBalanceError = string.Empty;
        }

        decimal? dailyTransactionLimit = null;
        DailyTransactionLimitError = string.Empty;
        if (DailyTransactionLimitText.Trim().Length > 0)
        {
            if (!decimal.TryParse(DailyTransactionLimitText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedDaily) || parsedDaily < 0)
            {
                DailyTransactionLimitError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
            }
            else
            {
                dailyTransactionLimit = parsedDaily;
            }
        }

        decimal? monthlyTransactionLimit = null;
        MonthlyTransactionLimitError = string.Empty;
        if (MonthlyTransactionLimitText.Trim().Length > 0)
        {
            if (!decimal.TryParse(MonthlyTransactionLimitText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedMonthly) || parsedMonthly < 0)
            {
                MonthlyTransactionLimitError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
            }
            else
            {
                monthlyTransactionLimit = parsedMonthly;
            }
        }

        if (!int.TryParse(CitizenRequiredReferText.Trim(), out var citizenRequiredRefer) || citizenRequiredRefer < 0)
        {
            CitizenRequiredReferError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
        }
        else
        {
            CitizenRequiredReferError = string.Empty;
        }

        if (!int.TryParse(ForeignRequiredReferText.Trim(), out var foreignRequiredRefer) || foreignRequiredRefer < 0)
        {
            ForeignRequiredReferError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
        }
        else
        {
            ForeignRequiredReferError = string.Empty;
        }

        StatusError = SelectedStatus is null
            ? "Please choose a status."
            : string.Empty;

        var isValid = !HasCodeError && !HasNameError && !HasCategoryError && !HasMinimumOpeningBalanceError
            && !HasMinimumMaintainedBalanceError && !HasDailyTransactionLimitError && !HasMonthlyTransactionLimitError
            && !HasCitizenRequiredReferError && !HasForeignRequiredReferError && !HasStatusError;

        input = isValid
            ? new PolicyInput(
                code,
                name,
                SelectedCategory!.Value,
                minimumOpeningBalance,
                minimumMaintainedBalance,
                dailyTransactionLimit,
                monthlyTransactionLimit,
                AllowWithdrawal,
                AllowTransfer,
                AllowPartialWithdrawal,
                AllowCitizen,
                AllowForeigner,
                citizenRequiredRefer,
                foreignRequiredRefer,
                SelectedStatus!)
            : default;

        return isValid;
    }

    // Puts a server rejection next to the field it is about; anything else goes to the banner.
    private void ShowServerError(AppException exception)
    {
        switch (exception.Code)
        {
            case MessageCode.AccountTypeCodeAlreadyExists:
                CodeError = exception.Message;
                break;
            default:
                FormError = exception.Message;
                break;
        }
    }

    // Normalized, parsed input ready to send to the server.
    private readonly record struct PolicyInput(
        string Code,
        string Name,
        AccountTypeCategory Category,
        decimal MinimumOpeningBalance,
        decimal MinimumMaintainedBalance,
        decimal? DailyTransactionLimit,
        decimal? MonthlyTransactionLimit,
        bool AllowWithdrawal,
        bool AllowTransfer,
        bool AllowPartialWithdrawal,
        bool AllowCitizen,
        bool AllowForeigner,
        int CitizenRequiredRefer,
        int ForeignRequiredRefer,
        string Status);
}
