using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Operations;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Models.Operations;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Operations;

/// <summary>
/// Operations › Fixed Deposit Maturity: each customer's fixed deposits by maturity date, with interest and renewal instruction.
/// </summary>
public sealed class FixedDepositMaturityViewModel : ViewModelBase, IAsyncInitializable
{
    private const int FirstPage = 1;

    private static readonly IReadOnlyList<FilterOption<FixedDepositStatus>> StatusOptionList =
    [
        new(null, "All statuses"),
        new(FixedDepositStatus.Active, "Active"),
        new(FixedDepositStatus.Matured, "Matured"),
        new(FixedDepositStatus.Closed, "Closed"),
        new(FixedDepositStatus.Cancelled, "Cancelled")
    ];

    private readonly IOperationService _operationService;
    private readonly IDialogService _dialogService;
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private int _page = FirstPage;
    private int _totalCount;

    public FixedDepositMaturityViewModel(IOperationService operationService, IDialogService dialogService)
    {
        // Constructors only store dependencies. No server calls here.
        _operationService = operationService;
        _dialogService = dialogService;

        Filter = new OperationFilterViewModel<FixedDepositStatus>(StatusOptionList);
        Filter.FiltersChanged += OnFiltersChanged;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
        ShowDetailsCommand = new AsyncRelayCommand(ShowDetailsAsync);
        PreviousPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page - 1), () => CanGoToPreviousPage);
        NextPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page + 1), () => CanGoToNextPage);
    }

    public string PageTitle => "Fixed Deposit Maturity";
    public string PageDescription => "Fixed deposits by maturity date: principal, interest and what happens at maturity.";

    public OperationFilterViewModel<FixedDepositStatus> Filter { get; }

    public ObservableCollection<FixedDepositMaturityRowModel> Rows { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="FixedDepositMaturityRowModel"/>; opens its detail card.</summary>
    public AsyncRelayCommand ShowDetailsCommand { get; }

    public AsyncRelayCommand PreviousPageCommand { get; }

    public AsyncRelayCommand NextPageCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>True when a finished load returned no rows.</summary>
    public bool IsEmpty => !IsLoading && Rows.Count == 0;

    /// <summary>Counter shown next to the table title, e.g. "12 records".</summary>
    public string CountText => _totalCount == 1 ? "1 record" : $"{_totalCount} records";

    public int Page
    {
        get => _page;
        private set => SetProperty(ref _page, value);
    }

    /// <summary>e.g. "Page 2 of 3".</summary>
    public string PageText => $"Page {Page} of {TotalPages}";

    private int TotalPages => Math.Max(FirstPage, (int)Math.Ceiling(_totalCount / (double)OperationDisplay.PageSize));

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
        return LoadAsync(cancellationToken, FirstPage);
    }

    // async void is intentional: event handler; LoadAsync catches every expected failure itself.
    private async void OnFiltersChanged()
    {
        await LoadAsync(CancellationToken.None, FirstPage);
    }

    private async Task LoadAsync(CancellationToken cancellationToken, int? page = null)
    {
        ErrorMessage = string.Empty;

        var (from, to, dateError) = Filter.BuildDateRange();
        if (dateError is { } code)
        {
            ErrorMessage = MessageCatalog.GetMessage(code);
            return;
        }

        try
        {
            IsLoading = true;
            RaisePagingChanged();

            // From/To bound the maturity date.
            var filter = new FixedDepositMaturityFilter(Filter.Search, Filter.SelectedStatus.Value, from, to);
            var result = await _operationService.GetFixedDepositMaturitiesAsync(
                filter, page ?? Page, OperationDisplay.PageSize, cancellationToken);

            Rows.Clear();
            foreach (var item in result.Items)
            {
                Rows.Add(new FixedDepositMaturityRowModel
                {
                    Id = item.Id,
                    Customer = OperationDisplay.OrEmpty(item.CustomerName),
                    CustomerNo = OperationDisplay.OrEmpty(item.CustomerNo),
                    AccountNo = item.AccountNo,
                    AccountType = item.AccountTypeCode,
                    Principal = OperationDisplay.FormatAmount(item.PrincipalAmount),
                    Rate = OperationDisplay.FormatRate(item.AnnualRate),
                    StartDate = OperationDisplay.FormatDate(item.StartDate),
                    MaturityDate = OperationDisplay.FormatDate(item.MaturityDate),
                    // Days left only matter while the deposit is still running.
                    DaysLeft = item.Status == FixedDepositStatus.Active
                        ? OperationDisplay.FormatDaysLeft(item.DaysToMaturity)
                        : DisplayFormats.EmptyValue,
                    InterestAccrued = OperationDisplay.FormatAmount(item.InterestAccrued),
                    MaturityInterest = OperationDisplay.FormatAmount(item.ExpectedMaturityInterest),
                    RenewalInstruction = OperationDisplay.FormatRenewalInstruction(item.RenewalInstruction),
                    PayoutAccountNo = OperationDisplay.OrEmpty(item.PayoutAccountNo),
                    Status = item.Status.ToString(),
                    IsDueSoon = item.Status == FixedDepositStatus.Active &&
                        item.DaysToMaturity <= OperationDisplay.DueSoonDays
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

    // Loads the record's details first, so a failure shows on the page instead of an empty dialog.
    private async Task ShowDetailsAsync(object? parameter)
    {
        if (parameter is not FixedDepositMaturityRowModel row)
        {
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            var detail = await _operationService.GetFixedDepositMaturityByIdAsync(row.Id, CancellationToken.None);
            _dialogService.ShowDialog(new FixedDepositMaturityDetailsViewModel(detail));
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
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
