using System.Globalization;
using bams.desktop.Commands;
using bams.desktop.DTOs.Configuration;
using bams.desktop.Exceptions;
using bams.desktop.Models.Configuration;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// <summary>
/// The create / edit fee rule form shown in a modal. Closes itself with <c>true</c> after a
/// successful save; <see cref="SavedFeeRule"/> holds the result.
/// </summary>
public sealed class FeeRateFormViewModel : ViewModelBase, IDialogViewModel
{
    public static readonly IReadOnlyList<string> Statuses = ["Active", "Inactive"];

    /// <summary>Bindable alias of <see cref="Statuses"/> for the status drop-down.</summary>
    public IReadOnlyList<string> StatusOptions => Statuses;

    public IReadOnlyList<FeeTypeOption> FeeTypeOptions => FeeTypeOption.All;

    private readonly IFeeRateService _feeRateService;
    private readonly FeeRuleResponse? _existingRule;

    private AccountTypeOption? _selectedAccountType;
    private FeeTypeOption? _selectedFeeType;
    private string _amountText = string.Empty;
    private string _percentageText = string.Empty;
    private string _minimumFeeText = string.Empty;
    private string _maximumFeeText = string.Empty;
    private string _taxRateText = string.Empty;
    private DateTime? _effectiveFrom;
    private DateTime? _effectiveTo;
    private string? _selectedStatus;

    private string _accountTypeError = string.Empty;
    private string _feeTypeError = string.Empty;
    private string _amountError = string.Empty;
    private string _percentageError = string.Empty;
    private string _minimumFeeError = string.Empty;
    private string _maximumFeeError = string.Empty;
    private string _taxRateError = string.Empty;
    private string _effectiveFromError = string.Empty;
    private string _effectiveToError = string.Empty;
    private string _statusError = string.Empty;
    private string _formError = string.Empty;
    private bool _isBusy;

    /// <summary>
    /// Creates the form. Pass <paramref name="existingRule"/> to edit that rule; null creates a new one.
    /// </summary>
    public FeeRateFormViewModel(
        IFeeRateService feeRateService,
        IReadOnlyList<AccountTypeOption> accountTypes,
        FeeRuleResponse? existingRule)
    {
        _feeRateService = feeRateService;
        _existingRule = existingRule;
        AccountTypes = accountTypes;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false), _ => CanCancel);

        if (existingRule is not null)
        {
            FillFromExistingRule(existingRule);
        }
    }

    public event Action<bool>? CloseRequested;

    // A stray click outside must not throw away what was typed.
    public bool CanCloseOnBackdropClick => false;

    public bool CanCancel => !IsBusy;

    public bool IsEditMode => _existingRule is not null;

    public string Title => IsEditMode ? "Edit fee rule" : "Create fee rule";

    public string Subtitle => IsEditMode
        ? "Update the fee terms for this rule."
        : "Add a new fee rule for an account type.";

    public string SaveText => IsEditMode ? "Save changes" : "Create rule";

    public IReadOnlyList<AccountTypeOption> AccountTypes { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// <summary>The saved rule, set just before the dialog closes with success.</summary>
    public FeeRuleResponse? SavedFeeRule { get; private set; }

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

    public FeeTypeOption? SelectedFeeType
    {
        get => _selectedFeeType;
        set
        {
            if (SetProperty(ref _selectedFeeType, value))
            {
                FeeTypeError = string.Empty;
            }
        }
    }

    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetProperty(ref _amountText, value))
            {
                AmountError = string.Empty;
            }
        }
    }

    public string PercentageText
    {
        get => _percentageText;
        set
        {
            if (SetProperty(ref _percentageText, value))
            {
                PercentageError = string.Empty;
            }
        }
    }

    public string MinimumFeeText
    {
        get => _minimumFeeText;
        set
        {
            if (SetProperty(ref _minimumFeeText, value))
            {
                MinimumFeeError = string.Empty;
            }
        }
    }

    public string MaximumFeeText
    {
        get => _maximumFeeText;
        set
        {
            if (SetProperty(ref _maximumFeeText, value))
            {
                MaximumFeeError = string.Empty;
            }
        }
    }

    public string TaxRateText
    {
        get => _taxRateText;
        set
        {
            if (SetProperty(ref _taxRateText, value))
            {
                TaxRateError = string.Empty;
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

    public string FeeTypeError
    {
        get => _feeTypeError;
        private set
        {
            if (SetProperty(ref _feeTypeError, value))
            {
                OnPropertyChanged(nameof(HasFeeTypeError));
            }
        }
    }

    public bool HasFeeTypeError => !string.IsNullOrEmpty(FeeTypeError);

    public string AmountError
    {
        get => _amountError;
        private set
        {
            if (SetProperty(ref _amountError, value))
            {
                OnPropertyChanged(nameof(HasAmountError));
            }
        }
    }

    public bool HasAmountError => !string.IsNullOrEmpty(AmountError);

    public string PercentageError
    {
        get => _percentageError;
        private set
        {
            if (SetProperty(ref _percentageError, value))
            {
                OnPropertyChanged(nameof(HasPercentageError));
            }
        }
    }

    public bool HasPercentageError => !string.IsNullOrEmpty(PercentageError);

    public string MinimumFeeError
    {
        get => _minimumFeeError;
        private set
        {
            if (SetProperty(ref _minimumFeeError, value))
            {
                OnPropertyChanged(nameof(HasMinimumFeeError));
            }
        }
    }

    public bool HasMinimumFeeError => !string.IsNullOrEmpty(MinimumFeeError);

    public string MaximumFeeError
    {
        get => _maximumFeeError;
        private set
        {
            if (SetProperty(ref _maximumFeeError, value))
            {
                OnPropertyChanged(nameof(HasMaximumFeeError));
            }
        }
    }

    public bool HasMaximumFeeError => !string.IsNullOrEmpty(MaximumFeeError);

    public string TaxRateError
    {
        get => _taxRateError;
        private set
        {
            if (SetProperty(ref _taxRateError, value))
            {
                OnPropertyChanged(nameof(HasTaxRateError));
            }
        }
    }

    public bool HasTaxRateError => !string.IsNullOrEmpty(TaxRateError);

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

    private void FillFromExistingRule(FeeRuleResponse rule)
    {
        _selectedAccountType = AccountTypes.FirstOrDefault(type => type.Id == rule.AccountTypeId);
        _selectedFeeType = FeeTypeOptions.FirstOrDefault(option => option.Value == rule.FeeType);
        _amountText = rule.Amount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _percentageText = rule.Percentage?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _minimumFeeText = rule.MinimumFee?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _maximumFeeText = rule.MaximumFee?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _taxRateText = rule.TaxRate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _effectiveFrom = rule.EffectiveFrom.ToDateTime(TimeOnly.MinValue);
        _effectiveTo = rule.EffectiveTo?.ToDateTime(TimeOnly.MinValue);
        _selectedStatus = Statuses.FirstOrDefault(status => status == rule.Status) ?? rule.Status;
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

            SavedFeeRule = IsEditMode
                ? await _feeRateService.UpdateFeeRuleAsync(
                    _existingRule!.Id,
                    new UpdateFeeRuleRequest(
                        input.AccountTypeId, input.FeeType, input.Amount, input.Percentage,
                        input.MinimumFee, input.MaximumFee, input.TaxRate,
                        input.EffectiveFrom, input.EffectiveTo, input.Status),
                    CancellationToken.None)
                : await _feeRateService.CreateFeeRuleAsync(
                    new CreateFeeRuleRequest(
                        input.AccountTypeId, input.FeeType, input.Amount, input.Percentage,
                        input.MinimumFee, input.MaximumFee, input.TaxRate,
                        input.EffectiveFrom, input.EffectiveTo, input.Status),
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

        FeeTypeError = SelectedFeeType is null
            ? "Please choose a fee type."
            : string.Empty;

        decimal? amount = null;
        AmountError = string.Empty;
        if (AmountText.Trim().Length > 0)
        {
            if (!decimal.TryParse(AmountText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount) || parsedAmount < 0)
            {
                AmountError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
            }
            else
            {
                amount = parsedAmount;
            }
        }

        decimal? percentage = null;
        PercentageError = string.Empty;
        if (PercentageText.Trim().Length > 0)
        {
            if (!decimal.TryParse(PercentageText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedPercentage) || parsedPercentage < 0)
            {
                PercentageError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
            }
            else
            {
                percentage = parsedPercentage;
            }
        }

        decimal? minimumFee = null;
        MinimumFeeError = string.Empty;
        if (MinimumFeeText.Trim().Length > 0)
        {
            if (!decimal.TryParse(MinimumFeeText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedMin) || parsedMin < 0)
            {
                MinimumFeeError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
            }
            else
            {
                minimumFee = parsedMin;
            }
        }

        decimal? maximumFee = null;
        MaximumFeeError = string.Empty;
        if (MaximumFeeText.Trim().Length > 0)
        {
            if (!decimal.TryParse(MaximumFeeText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedMax) || parsedMax < 0)
            {
                MaximumFeeError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
            }
            else
            {
                maximumFee = parsedMax;
            }
        }

        if (MaximumFeeError.Length == 0 && minimumFee is not null && maximumFee is not null && minimumFee > maximumFee)
        {
            MaximumFeeError = MessageCatalog.GetMessage(MessageCode.FeeRuleAmountRangeInvalid);
        }

        decimal? taxRate = null;
        TaxRateError = string.Empty;
        if (TaxRateText.Trim().Length > 0)
        {
            if (!decimal.TryParse(TaxRateText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedTax) || parsedTax < 0)
            {
                TaxRateError = MessageCatalog.GetMessage(MessageCode.InvalidAmount);
            }
            else
            {
                taxRate = parsedTax;
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

        var isValid = !HasAccountTypeError && !HasFeeTypeError && !HasAmountError && !HasPercentageError
            && !HasMinimumFeeError && !HasMaximumFeeError && !HasTaxRateError
            && !HasEffectiveFromError && !HasEffectiveToError && !HasStatusError;

        input = isValid
            ? new RuleInput(
                SelectedAccountType!.Id,
                SelectedFeeType!.Value,
                amount,
                percentage,
                minimumFee,
                maximumFee,
                taxRate,
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
            default:
                FormError = exception.Message;
                break;
        }
    }

    // Normalized, parsed input ready to send to the server.
    private readonly record struct RuleInput(
        long AccountTypeId,
        FeeType FeeType,
        decimal? Amount,
        decimal? Percentage,
        decimal? MinimumFee,
        decimal? MaximumFee,
        decimal? TaxRate,
        DateOnly EffectiveFrom,
        DateOnly? EffectiveTo,
        string Status);
}
