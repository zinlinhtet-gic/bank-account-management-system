using System.Globalization;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>
/// Modal form for "Export to Excel": GL account class (or "All Account"), start and end month/year,
/// and the local file the export is saved to. Closing with true means the form is valid; the caller then
/// reads <see cref="BuildRequest"/>.
/// </summary>
public sealed class ExportAccountingDialogViewModel : ViewModelBase, IDialogViewModel
{
    private readonly IDialogService _dialogService;
    private readonly IAccountingService _accountingService;
    private AccountClassOption _selectedAccountClass;
    private MonthOption _startMonth;
    private MonthOption _endMonth;
    private int _startYear;
    private int _endYear;
    private string _filePath = string.Empty;
    private string _error = string.Empty;

    /// <summary>One entry of the account type list; <see cref="Value"/> is null for "All Account".</summary>
    public sealed record AccountClassOption(string Label, GlAccountClass? Value);

    /// <summary>One entry of a month list.</summary>
    public sealed record MonthOption(string Label, int Number);

    public ExportAccountingDialogViewModel(IDialogService dialogService, IAccountingService accountingService)
    {
        _dialogService = dialogService;
        _accountingService = accountingService;
        AccountClassOptions = [new(AccountingConstants.AllAccountsLabel, null)];
        MonthOptions = Enumerable
            .Range(AccountingConstants.FirstMonthOfYear, AccountingConstants.LastMonthOfYear)
            .Select(month => new MonthOption(CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month), month))
            .ToList();

        var currentYear = MyanmarTimeDisplay.ToMyanmarTime(DateTime.UtcNow).Year;
        var currentMonth = MyanmarTimeDisplay.ToMyanmarTime(DateTime.UtcNow).Month;
        YearOptions = Enumerable
            .Range(AccountingConstants.EarliestExportYear, currentYear + AccountingConstants.ExportYearsAheadOfCurrent - AccountingConstants.EarliestExportYear + 1)
            .ToList();

        // Default period: the current month.
        _selectedAccountClass = AccountClassOptions[0];
        _startMonth = MonthOptions[currentMonth - AccountingConstants.FirstMonthOfYear];
        _endMonth = _startMonth;
        _startYear = currentYear;
        _endYear = currentYear;

        BrowseCommand = new RelayCommand(_ => ChooseFilePath());
        ConfirmCommand = new RelayCommand(_ => Confirm());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
    }

    public event Action<bool>? CloseRequested;
    public bool CanCloseOnBackdropClick => false;
    public bool CanCancel => true;

    public IReadOnlyList<AccountClassOption> AccountClassOptions { get; private set; }
    public IReadOnlyList<MonthOption> MonthOptions { get; }
    public IReadOnlyList<int> YearOptions { get; }

    public RelayCommand BrowseCommand { get; }
    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }

    public AccountClassOption SelectedAccountClass
    {
        get => _selectedAccountClass;
        set => SetProperty(ref _selectedAccountClass, value);
    }

    public MonthOption StartMonth { get => _startMonth; set => SetProperty(ref _startMonth, value); }
    public MonthOption EndMonth { get => _endMonth; set => SetProperty(ref _endMonth, value); }
    public int StartYear { get => _startYear; set => SetProperty(ref _startYear, value); }
    public int EndYear { get => _endYear; set => SetProperty(ref _endYear, value); }

    /// <summary>Local path the file will be saved to; empty until the user browses.</summary>
    public string FilePath { get => _filePath; private set => SetProperty(ref _filePath, value); }

    public string Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(ErrorCode));
            }
        }
    }

    /// <summary>Stable code of the current validation problem, or null when the form is valid.</summary>
    public MessageCode? ErrorCode { get; private set; }

    /// <summary>Creates the export request from the validated form values.</summary>
    public AccountingExportRequest BuildRequest()
    {
        return new AccountingExportRequest(
            SelectedAccountClass.Value,
            StartYear,
            StartMonth.Number,
            EndYear,
            EndMonth.Number,
            FilePath);
    }

    // Opens the Save dialog with a name that describes the chosen period.
    private void ChooseFilePath()
    {
        var suggestedName = $"{AccountingConstants.ExportFileNamePrefix}-{StartYear}-{StartMonth.Number:00}-to-{EndYear}-{EndMonth.Number:00}{AccountingConstants.ExcelFileExtension}";
        var chosenPath = _dialogService.PickSaveFile(
            AccountingConstants.ExportDialogTitle,
            suggestedName,
            AccountingConstants.ExcelFileFilter);
        if (chosenPath is not null)
        {
            FilePath = chosenPath;
        }
    }

    // Closes the dialog only when the period and file location are valid.
    private void Confirm()
    {
        var problem = FindValidationProblem();
        SetValidationProblem(problem);
        if (problem is null)
        {
            CloseRequested?.Invoke(true);
        }
    }

    public async Task LoadAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _accountingService.GetGlAccountsAsync(cancellationToken);
        AccountClassOptions =
        [
            new(AccountingConstants.AllAccountsLabel, null),
            .. accounts.Select(account => new AccountClassOption($"{account.Code} - {account.Name}", account.AccountClass))
        ];
        OnPropertyChanged(nameof(AccountClassOptions));
        SelectedAccountClass = AccountClassOptions[0];
    }

    // Local validation only; the server stays authoritative for the export content.
    private MessageCode? FindValidationProblem()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            return MessageCode.RequiredFieldMissing;
        }

        var start = StartYear * AccountingConstants.LastMonthOfYear + StartMonth.Number;
        var end = EndYear * AccountingConstants.LastMonthOfYear + EndMonth.Number;
        return start > end ? MessageCode.InvalidDateRange : null;
    }

    // Stores the code and its catalog text so the view can show the message.
    private void SetValidationProblem(MessageCode? code)
    {
        ErrorCode = code;
        Error = code is null ? string.Empty : MessageCatalog.GetMessage(code.Value);
    }
}
