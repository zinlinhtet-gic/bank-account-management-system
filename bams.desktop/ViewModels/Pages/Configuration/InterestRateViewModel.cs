using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Exceptions;
using bams.desktop.Models.Configuration;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// ViewModel for the Interest Rate list page.
public sealed class InterestRateViewModel : ViewModelBase, IAsyncInitializable
{
    private const int FirstPage = 1;
    private const int PageSize = 10;

    private readonly IInterestRateService _interestRateService;
    private readonly IDialogService _dialogService;
    private IReadOnlyList<AccountTypeOption> _accountTypes = [];
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private int _page = FirstPage;
    private int _totalCount;

    public InterestRateViewModel(IInterestRateService interestRateService, IDialogService dialogService)
    {
        // Constructors only store dependencies. No server calls here.
        _interestRateService = interestRateService;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
        CreateInterestRateCommand = new AsyncRelayCommand(CreateInterestRateAsync);
        EditInterestRateCommand = new AsyncRelayCommand(EditInterestRateAsync);
        PreviousPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page - 1), () => CanGoToPreviousPage);
        NextPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page + 1), () => CanGoToNextPage);
    }

    public string PageTitle => "Interest Rate";
    public string PageDescription => "Interest rate rules per account type.";

    public ObservableCollection<InterestRateRowModel> Rows { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand CreateInterestRateCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="InterestRateRowModel"/>.</summary>
    public AsyncRelayCommand EditInterestRateCommand { get; }

    public AsyncRelayCommand PreviousPageCommand { get; }

    public AsyncRelayCommand NextPageCommand { get; }

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
    public string CountText => _totalCount == 1 ? "1 rule" : $"{_totalCount} rules";

    /// <summary>The page currently shown, starting at 1.</summary>
    public int Page
    {
        get => _page;
        private set => SetProperty(ref _page, value);
    }

    /// <summary>e.g. "Page 2 of 3".</summary>
    public string PageText => $"Page {Page} of {TotalPages}";

    private int TotalPages => Math.Max(FirstPage, (int)Math.Ceiling(_totalCount / (double)PageSize));

    private bool CanGoToPreviousPage => !IsLoading && Page > FirstPage;

    private bool CanGoToNextPage => !IsLoading && Page < TotalPages;

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

    private async Task LoadAsync(CancellationToken cancellationToken, int? page = null)
    {
        ErrorMessage = string.Empty;

        try
        {
            IsLoading = true;
            RaisePagingChanged();

            var result = await _interestRateService.GetInterestRatesAsync(page ?? Page, cancellationToken);

            Rows.Clear();
            foreach (var rate in result.Items)
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

            Page = result.PageNumber;
            _totalCount = result.TotalCount;
            OnPropertyChanged(nameof(CountText));
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
            RaisePagingChanged();
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

    // The pager text and buttons depend on the page, total and loading state.
    private void RaisePagingChanged()
    {
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(IsEmpty));
        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }
}
