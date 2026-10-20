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
    private IReadOnlyList<AccountTypeOption> _accountTypes = [];
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private int _page = FirstPage;
    private int _totalCount;
    private FeeRateFormViewModel? _currentForm;

    public FeeRateViewModel(IFeeRateService feeRateService)
    {
        // Constructors only store dependencies. No server calls here.
        _feeRateService = feeRateService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
        CreateFeeRuleCommand = new AsyncRelayCommand(CreateFeeRuleAsync);
        EditFeeRuleCommand = new AsyncRelayCommand(EditFeeRuleAsync);
        PreviousPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page - 1), () => CanGoToPreviousPage);
        NextPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page + 1), () => CanGoToNextPage);
    }

    public string PageTitle => "Fee Rate";
    public string PageDescription => "Fee rules per account type.";

    public ObservableCollection<FeeRateRowModel> Rows { get; } = new();

    /// <summary>The create / edit form shown in place of the list, or null while the list is shown.</summary>
    public FeeRateFormViewModel? CurrentForm
    {
        get => _currentForm;
        private set
        {
            if (SetProperty(ref _currentForm, value))
            {
                OnPropertyChanged(nameof(IsFormOpen));
                OnPropertyChanged(nameof(IsListVisible));
            }
        }
    }

    public bool IsFormOpen => CurrentForm is not null;

    public bool IsListVisible => !IsFormOpen;

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

    // Shows the empty form in place of the list.
    private async Task CreateFeeRuleAsync()
    {
        ErrorMessage = string.Empty;

        if (!await EnsureAccountTypesLoadedAsync())
        {
            return;
        }

        var form = new FeeRateFormViewModel(_feeRateService, _accountTypes, existingRule: null);
        OpenForm(form);
    }

    // Shows the form filled with the row's rule in place of the list.
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
        OpenForm(form);
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

    // Shows the form in place of the list until it is saved or cancelled.
    private void OpenForm(FeeRateFormViewModel form)
    {
        form.CloseRequested += OnFormClosed;
        CurrentForm = form;
    }

    // Back to the list; reloads it after a successful save so the change shows.
    // async void is intentional: an event handler; LoadAsync reports its own failures.
    private async void OnFormClosed(bool saved)
    {
        if (CurrentForm is not null)
        {
            CurrentForm.CloseRequested -= OnFormClosed;
        }

        CurrentForm = null;

        if (saved)
        {
            await LoadAsync(CancellationToken.None);
        }
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
