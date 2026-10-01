using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Exceptions;
using bams.desktop.Models.Configuration;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// ViewModel for the Fee Rate list page.
public sealed class FeeRateViewModel : ViewModelBase, IAsyncInitializable
{
    private const int FirstPage = 1;
    private const int PageSize = 10;

    private readonly IFeeRateService _feeRateService;
    private readonly IDialogService _dialogService;
    private IReadOnlyList<AccountTypeOption> _accountTypes = [];
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private int _page = FirstPage;
    private int _totalCount;

    public FeeRateViewModel(IFeeRateService feeRateService, IDialogService dialogService)
    {
        // Constructors only store dependencies. No server calls here.
        _feeRateService = feeRateService;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
        CreateFeeRuleCommand = new AsyncRelayCommand(CreateFeeRuleAsync);
        EditFeeRuleCommand = new AsyncRelayCommand(EditFeeRuleAsync);
        PreviousPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page - 1), () => CanGoToPreviousPage);
        NextPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page + 1), () => CanGoToNextPage);
    }

    public string PageTitle => "Fee Rate";
    public string PageDescription => "Fee rules per account type.";

    public ObservableCollection<FeeRateRowModel> Rows { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand CreateFeeRuleCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="FeeRateRowModel"/>.</summary>
    public AsyncRelayCommand EditFeeRuleCommand { get; }

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

            var result = await _feeRateService.GetFeeRulesAsync(page ?? Page, cancellationToken);

            Rows.Clear();
            foreach (var rule in result.Items)
            {
                Rows.Add(new FeeRateRowModel
                {
                    AccountType = rule.AccountTypeCode,
                    FeeType = FormatFeeType(rule.FeeType),
                    Amount = rule.Amount?.ToString("N0") ?? "-",
                    Percentage = rule.Percentage is null ? "-" : $"{rule.Percentage:0.##}%",
                    Status = rule.Status,
                    Source = rule
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
    private async Task CreateFeeRuleAsync()
    {
        ErrorMessage = string.Empty;

        if (!await EnsureAccountTypesLoadedAsync())
        {
            return;
        }

        var form = new FeeRateFormViewModel(_feeRateService, _accountTypes, existingRule: null);
        if (!_dialogService.ShowDialog(form))
        {
            return;
        }

        await LoadAsync(CancellationToken.None);
    }

    // Opens the form filled with the row's rule; after a successful save, reloads the list.
    private async Task EditFeeRuleAsync(object? parameter)
    {
        if (parameter is not FeeRateRowModel row)
        {
            return;
        }

        ErrorMessage = string.Empty;

        if (!await EnsureAccountTypesLoadedAsync())
        {
            return;
        }

        var form = new FeeRateFormViewModel(_feeRateService, _accountTypes, row.Source);
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
            var accountTypes = await _feeRateService.GetAccountTypeOptionsAsync(CancellationToken.None);
            _accountTypes = accountTypes.Select(AccountTypeOption.FromResponse).ToList();
            return true;
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }
    }

    private static string FormatFeeType(DTOs.Configuration.FeeType feeType)
    {
        return FeeTypeOption.All.FirstOrDefault(option => option.Value == feeType)?.DisplayName ?? feeType.ToString();
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
