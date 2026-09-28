using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Exceptions;
using bams.desktop.Models.Configuration;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// ViewModel for the Interest Rate list page.
public sealed class InterestRateViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IInterestRateService _interestRateService;
    private readonly IDialogService _dialogService;
    private IReadOnlyList<AccountTypeOption> _accountTypes = [];
    private string _errorMessage = string.Empty;
    private bool _isLoading;

    public InterestRateViewModel(IInterestRateService interestRateService, IDialogService dialogService)
    {
        // Constructors only store dependencies. No server calls here.
        _interestRateService = interestRateService;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
        CreateInterestRateCommand = new AsyncRelayCommand(CreateInterestRateAsync);
        EditInterestRateCommand = new AsyncRelayCommand(EditInterestRateAsync);
    }

    public string PageTitle => "Interest Rate";
    public string PageDescription => "Interest rate rules per account type.";

    public ObservableCollection<InterestRateRowModel> Rows { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand CreateInterestRateCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="InterestRateRowModel"/>.</summary>
    public AsyncRelayCommand EditInterestRateCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    /// <summary>True when a finished load returned no rows (shows the "no rules" message).</summary>
    public bool IsEmpty => !IsLoading && Rows.Count == 0;

    /// <summary>Counter shown next to the table title, e.g. "3 rules".</summary>
    public string CountText => Rows.Count == 1 ? "1 rule" : $"{Rows.Count} rules";

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

    // Called by MainViewModel every time the user opens this page.
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(cancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        try
        {
            IsLoading = true;

            var interestRates = await _interestRateService.GetInterestRatesAsync(cancellationToken);

            Rows.Clear();
            foreach (var rate in interestRates)
            {
                Rows.Add(new InterestRateRowModel
                {
                    AccountType = rate.AccountTypeCode,
                    Term = FormatTerm(rate.TermDays, rate.TermMonths),
                    AnnualRate = $"{rate.AnnualRate:0.##}%",
                    MinBalance = rate.BalanceMin?.ToString("N0") ?? "-",
                    MaxBalance = rate.BalanceMax?.ToString("N0") ?? "-",
                    Status = rate.Status,
                    Source = rate
                });
            }

            OnPropertyChanged(nameof(CountText));
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Opens the empty form; after a successful save, reloads the list.
    private async Task CreateInterestRateAsync()
    {
        ErrorMessage = string.Empty;

        if (!await EnsureAccountTypesLoadedAsync())
        {
            return;
        }

        var form = new InterestRateFormViewModel(_interestRateService, _accountTypes, existingRate: null);
        if (!_dialogService.ShowDialog(form))
        {
            return;
        }

        await LoadAsync(CancellationToken.None);
    }

    // Opens the form filled with the row's rule; after a successful save, reloads the list.
    private async Task EditInterestRateAsync(object? parameter)
    {
        if (parameter is not InterestRateRowModel row)
        {
            return;
        }

        ErrorMessage = string.Empty;

        if (!await EnsureAccountTypesLoadedAsync())
        {
            return;
        }

        var form = new InterestRateFormViewModel(_interestRateService, _accountTypes, row.Source);
        if (!_dialogService.ShowDialog(form))
        {
            return;
        }

        await LoadAsync(CancellationToken.None);
    }

    // Loads the account types once per page visit for the form's drop-down.
    private async Task<bool> EnsureAccountTypesLoadedAsync()
    {
        if (_accountTypes.Count > 0)
        {
            return true;
        }

        try
        {
            var accountTypes = await _interestRateService.GetAccountTypeOptionsAsync(CancellationToken.None);
            _accountTypes = accountTypes.Select(AccountTypeOption.FromResponse).ToList();
            return true;
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }
    }

    private static string FormatTerm(int? termDays, int? termMonths)
    {
        if (termMonths is not null)
        {
            return termMonths == 1 ? "1 month" : $"{termMonths} months";
        }

        if (termDays is not null)
        {
            return termDays == 1 ? "1 day" : $"{termDays} days";
        }

        return "-";
    }
}
