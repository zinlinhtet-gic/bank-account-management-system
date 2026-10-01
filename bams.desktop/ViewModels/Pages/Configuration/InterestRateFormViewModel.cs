using System.Globalization;
using bams.desktop.Commands;
using bams.desktop.DTOs.Configuration;
using bams.desktop.Exceptions;
using bams.desktop.Models.Configuration;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Configuration;


/// The create / edit interest rate rule form shown in a modal.
public sealed class InterestRateFormViewModel : ViewModelBase, IDialogViewModel
{
    public static readonly IReadOnlyList<string> Statuses = ["Active", "Inactive"];

    public IReadOnlyList<string> StatusOptions => Statuses;

    private readonly IInterestRateService _interestRateService;
    private readonly InterestRateResponse? _existingRate;

    private AccountTypeOption? _selectedAccountType;
    private string _termDaysText = string.Empty;
    private string _termMonthsText = string.Empty;
    private string _balanceMinText = string.Empty;
    private string _balanceMaxText = string.Empty;
    private string _annualRateText = string.Empty;
    private string _earlyWithdrawalRateText = string.Empty;
    private DateTime? _effectiveFrom;
    private DateTime? _effectiveTo;
    private string? _selectedStatus;

    private string _accountTypeError = string.Empty;
    private string _annualRateError = string.Empty;
    private string _earlyWithdrawalRateError = string.Empty;
    private string _balanceMinError = string.Empty;
    private string _balanceMaxError = string.Empty;
    private string _effectiveFromError = string.Empty;
    private string _effectiveToError = string.Empty;
    private string _statusError = string.Empty;
    private string _formError = string.Empty;
    private bool _isBusy;

    /// Creates the form.
    public InterestRateFormViewModel(
        IInterestRateService interestRateService,
        IReadOnlyList<AccountTypeOption> accountTypes,
        InterestRateResponse? existingRate)
    {
        _interestRateService = interestRateService;
        _existingRate = existingRate;
        AccountTypes = accountTypes;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false), _ => CanCancel);

        if (existingRate is not null)
        {
            FillFromExistingRate(existingRate);
        }
    }

    public event Action<bool>? CloseRequested;

    // A stray click outside must not throw away what was typed.
    public bool CanCloseOnBackdropClick => false;

    public bool CanCancel => !IsBusy;

    public bool IsEditMode => _existingRate is not null;

    public string Title => IsEditMode ? "Edit interest rate rule" : "Create interest rate rule";

    public string Subtitle => IsEditMode
        ? "Update the terms and rate for this rule."
        : "Add a new interest rate rule for an account type.";

    public string SaveText => IsEditMode ? "Save changes" : "Create rule";

    public IReadOnlyList<AccountTypeOption> AccountTypes { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// The saved rule, set just before the dialog closes with success.
    public InterestRateResponse? SavedInterestRate { get; private set; }

    public AccountTypeOption? SelectedAccountType
    {
        get => _selectedAccountType;
        set
        {
            if (SetProperty(ref _selectedAccountType, value))
            {
                AccountTypeError = string.Empty;
            }
        }
    }

    public string TermDaysText
    {
        get => _termDaysText;
        set => SetProperty(ref _termDaysText, value);
    }

    public string TermMonthsText
    {
        get => _termMonthsText;
        set => SetProperty(ref _termMonthsText, value);
    }

    public string BalanceMinText
    {
        get => _balanceMinText;
        set
        {
            if (SetProperty(ref _balanceMinText, value))
            {
                BalanceMinError = string.Empty;
            }
        }
    }

    public string BalanceMaxText
    {
        get => _balanceMaxText;
        set
        {
            if (SetProperty(ref _balanceMaxText, value))
            {
                BalanceMaxError = string.Empty;
            }
        }
    }

    public string AnnualRateText
    {
        get => _annualRateText;
        set
        {
            if (SetProperty(ref _annualRateText, value))
            {
                AnnualRateError = string.Empty;
            }
        }
    }

    public string EarlyWithdrawalRateText
    {
        get => _earlyWithdrawalRateText;
        set
        {
            if (SetProperty(ref _earlyWithdrawalRateText, value))
            {
                EarlyWithdrawalRateError = string.Empty;
            }
        }
    }

    public DateTime? EffectiveFrom
    {
        get => _effectiveFrom;
        set
        {
            if (SetProperty(ref _effectiveFrom, value))
            {
                EffectiveFromError = string.Empty;
            }
        }
    }

    public DateTime? EffectiveTo
    {
        get => _effectiveTo;
        set
        {
            if (SetProperty(ref _effectiveTo, value))
            {
                EffectiveToError = string.Empty;
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

    public string AccountTypeError
    {
        get => _accountTypeError;
        private set
        {
            if (SetProperty(ref _accountTypeError, value))
            {
                OnPropertyChanged(nameof(HasAccountTypeError));
            }
        }
    }

    public bool HasAccountTypeError => !string.IsNullOrEmpty(AccountTypeError);

    public string AnnualRateError
    {
        get => _annualRateError;
        private set
        {
            if (SetProperty(ref _annualRateError, value))
            {
                OnPropertyChanged(nameof(HasAnnualRateError));
            }
        }
    }

    public bool HasAnnualRateError => !string.IsNullOrEmpty(AnnualRateError);

    public string EarlyWithdrawalRateError
    {
        get => _earlyWithdrawalRateError;
        private set
        {
            if (SetProperty(ref _earlyWithdrawalRateError, value))
            {
                OnPropertyChanged(nameof(HasEarlyWithdrawalRateError));
            }
        }
    }

    public bool HasEarlyWithdrawalRateError => !string.IsNullOrEmpty(EarlyWithdrawalRateError);

    public string BalanceMinError
    {
        get => _balanceMinError;
        private set
        {
            if (SetProperty(ref _balanceMinError, value))
            {
                OnPropertyChanged(nameof(HasBalanceMinError));
            }
        }
    }

    public bool HasBalanceMinError => !string.IsNullOrEmpty(BalanceMinError);

    public string BalanceMaxError
    {
        get => _balanceMaxError;
        private set
        {
            if (SetProperty(ref _balanceMaxError, value))
            {
                OnPropertyChanged(nameof(HasBalanceMaxError));
            }
        }
    }

    public bool HasBalanceMaxError => !string.IsNullOrEmpty(BalanceMaxError);

    public string EffectiveFromError
    {
        get => _effectiveFromError;
        private set
        {
            if (SetProperty(ref _effectiveFromError, value))
            {
                OnPropertyChanged(nameof(HasEffectiveFromError));
            }
        }
    }

    public bool HasEffectiveFromError => !string.IsNullOrEmpty(EffectiveFromError);

    public string EffectiveToError
    {
        get => _effectiveToError;
        private set
        {
            if (SetProperty(ref _effectiveToError, value))
            {
                OnPropertyChanged(nameof(HasEffectiveToError));
            }
        }
    }

    public bool HasEffectiveToError => !string.IsNullOrEmpty(EffectiveToError);

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

    private void FillFromExistingRate(InterestRateResponse rate)
    {
        _selectedAccountType = AccountTypes.FirstOrDefault(type => type.Id == rate.AccountTypeId);
        _termDaysText = rate.TermDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _termMonthsText = rate.TermMonths?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _balanceMinText = rate.BalanceMin?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _balanceMaxText = rate.BalanceMax?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _annualRateText = rate.AnnualRate.ToString(CultureInfo.InvariantCulture);
        _earlyWithdrawalRateText = rate.EarlyWithdrawalRate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _effectiveFrom = rate.EffectiveFrom.ToDateTime(TimeOnly.MinValue);
        _effectiveTo = rate.EffectiveTo?.ToDateTime(TimeOnly.MinValue);
        _selectedStatus = Statuses.FirstOrDefault(status => status == rate.Status) ?? rate.Status;
    }

    // Validates locally, then creates or updates the rule.
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

            SavedInterestRate = IsEditMode
                ? await _interestRateService.UpdateInterestRateAsync(
                    _existingRate!.Id,
                    new UpdateInterestRateRequest(
                        input.AccountTypeId, input.TermDays, input.TermMonths,
                        input.BalanceMin, input.BalanceMax, input.AnnualRate,
                        input.EarlyWithdrawalRate, input.EffectiveFrom, input.EffectiveTo, input.Status),
                    CancellationToken.None)
                : await _interestRateService.CreateInterestRateAsync(
                    new CreateInterestRateRequest(
                        input.AccountTypeId, input.TermDays, input.TermMonths,
                        input.BalanceMin, input.BalanceMax, input.AnnualRate,
                        input.EarlyWithdrawalRate, input.EffectiveFrom, input.EffectiveTo, input.Status),
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
    private bool TryValidateFields(out RuleInput input)
    {
        AccountTypeError = SelectedAccountType is null
            ? "Please choose an account type."
            : string.Empty;

        int? termDays = TermDaysText.Trim().Length > 0 && int.TryParse(TermDaysText.Trim(), out var parsedTermDays)
            ? parsedTermDays
            : null;

        int? termMonths = TermMonthsText.Trim().Length > 0 && int.TryParse(TermMonthsText.Trim(), out var parsedTermMonths)
            ? parsedTermMonths
            : null;

        decimal? balanceMin = null;
        BalanceMinError = string.Empty;
        if (BalanceMinText.Trim().Length > 0)
        {
            if (!decimal.TryParse(BalanceMinText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedMin) || parsedMin < 0)
            {
                BalanceMinError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
            }
            else
            {
                balanceMin = parsedMin;
            }
        }

        decimal? balanceMax = null;
        BalanceMaxError = string.Empty;
        if (BalanceMaxText.Trim().Length > 0)
        {
            if (!decimal.TryParse(BalanceMaxText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedMax) || parsedMax < 0)
            {
                BalanceMaxError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
            }
            else
            {
                balanceMax = parsedMax;
            }
        }

        if (BalanceMaxError.Length == 0 && balanceMin is not null && balanceMax is not null && balanceMin > balanceMax)
        {
            BalanceMaxError = MessageCatalog.GetMessage(MessageCode.InterestRateBalanceRangeInvalid);
        }

        if (!decimal.TryParse(AnnualRateText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var annualRate) || annualRate < 0)
        {
            AnnualRateError = "Please enter a valid annual rate.";
        }
        else
        {
            AnnualRateError = string.Empty;
        }

        decimal? earlyWithdrawalRate = null;
        EarlyWithdrawalRateError = string.Empty;
        if (EarlyWithdrawalRateText.Trim().Length > 0)
        {
            if (!decimal.TryParse(EarlyWithdrawalRateText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedRate) || parsedRate < 0)
            {
                EarlyWithdrawalRateError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
            }
            else
            {
                earlyWithdrawalRate = parsedRate;
            }
        }

        EffectiveFromError = EffectiveFrom is null
            ? "Please choose an effective from date."
            : string.Empty;

        EffectiveToError = EffectiveTo is not null && EffectiveFrom is not null && EffectiveTo < EffectiveFrom
            ? MessageCatalog.GetMessage(MessageCode.InvalidDateRange)
            : string.Empty;

        StatusError = SelectedStatus is null
            ? "Please choose a status."
            : string.Empty;

        var isValid = !HasAccountTypeError && !HasAnnualRateError && !HasEarlyWithdrawalRateError
            && !HasBalanceMinError && !HasBalanceMaxError && !HasEffectiveFromError && !HasEffectiveToError && !HasStatusError;

        input = isValid
            ? new RuleInput(
                SelectedAccountType!.Id,
                termDays,
                termMonths,
                balanceMin,
                balanceMax,
                annualRate,
                earlyWithdrawalRate,
                DateOnly.FromDateTime(EffectiveFrom!.Value),
                EffectiveTo is null ? null : DateOnly.FromDateTime(EffectiveTo.Value),
                SelectedStatus!)
            : default;

        return isValid;
    }

    // Puts a server rejection next to the field it is about; anything else goes to the banner.
    private void ShowServerError(AppException exception)
    {
        switch (exception.Code)
        {
            case MessageCode.AccountTypeNotFound:
                AccountTypeError = exception.Message;
                break;
            case MessageCode.InterestRateRuleNotFound:
                FormError = exception.Message;
                break;
            default:
                FormError = exception.Message;
                break;
        }
    }

    // Normalized, parsed input ready to send to the server.
    private readonly record struct RuleInput(
        long AccountTypeId,
        int? TermDays,
        int? TermMonths,
        decimal? BalanceMin,
        decimal? BalanceMax,
        decimal AnnualRate,
        decimal? EarlyWithdrawalRate,
        DateOnly EffectiveFrom,
        DateOnly? EffectiveTo,
        string Status);
}
