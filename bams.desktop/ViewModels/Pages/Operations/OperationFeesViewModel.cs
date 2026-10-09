using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Configuration;
using bams.desktop.DTOs.Operations;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Models.Operations;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Operations;

/// <summary>
/// Operations › Fees: each fee charged to a customer account (maintenance, early withdrawal, dormant penalty, ...).
/// </summary>
public sealed class OperationFeesViewModel : ViewModelBase, IAsyncInitializable
{
    private const int FirstPage = 1;

    private static readonly IReadOnlyList<FilterOption<FeeAccrualStatus>> StatusOptionList =
    [
        new(null, "All statuses"),
        new(FeeAccrualStatus.Accrued, "Accrued"),
        new(FeeAccrualStatus.Posted, "Posted"),
        new(FeeAccrualStatus.Waived, "Waived"),
        new(FeeAccrualStatus.Cancelled, "Cancelled")
    ];

    // The Operations fee types: maintenance, early withdrawal and the dormant-account penalty.
    private static readonly IReadOnlyList<FilterOption<FeeType>> FeeTypeOptionList =
    [
        new(null, "All fee types"),
        new(FeeType.Maintenance, OperationDisplay.FormatFeeType(FeeType.Maintenance)),
        new(FeeType.EarlyWithdrawal, OperationDisplay.FormatFeeType(FeeType.EarlyWithdrawal)),
        new(FeeType.DormantAccount, OperationDisplay.FormatFeeType(FeeType.DormantAccount))
    ];

    private readonly IOperationService _operationService;
    private readonly IDialogService _dialogService;
    private FilterOption<FeeType> _selectedFeeType = FeeTypeOptionList[0];
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private int _page = FirstPage;
    private int _totalCount;

    public OperationFeesViewModel(IOperationService operationService, IDialogService dialogService)
    {
        // Constructors only store dependencies. No server calls here.
        _operationService = operationService;
        _dialogService = dialogService;

        Filter = new OperationFilterViewModel<FeeAccrualStatus>(StatusOptionList);
        Filter.FiltersChanged += OnFiltersChanged;
        // Reset also clears the fee type; the filter reloads once afterwards, so set the field without reloading.
        Filter.Resetting += () =>
        {
            _selectedFeeType = FeeTypeOptionList[0];
            OnPropertyChanged(nameof(SelectedFeeType));
        };

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
        ShowDetailsCommand = new AsyncRelayCommand(ShowDetailsAsync);
        PreviousPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page - 1), () => CanGoToPreviousPage);
        NextPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page + 1), () => CanGoToNextPage);
    }

    public string PageTitle => "Fees";
    public string PageDescription => "Maintenance, early withdrawal and dormant fees charged to each customer account.";

    public OperationFilterViewModel<FeeAccrualStatus> Filter { get; }

    public IReadOnlyList<FilterOption<FeeType>> FeeTypeOptions => FeeTypeOptionList;

    /// <summary>Fee type drop-down; changing it reloads from page 1.</summary>
    public FilterOption<FeeType> SelectedFeeType
    {
        get => _selectedFeeType;
        set
        {
            if (SetProperty(ref _selectedFeeType, value ?? FeeTypeOptionList[0]))
            {
                OnFiltersChanged();
            }
        }
    }

    public ObservableCollection<FeeOperationRowModel> Rows { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="FeeOperationRowModel"/>; opens its detail card.</summary>
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

            var filter = new FeeOperationFilter(
                Filter.Search, SelectedFeeType.Value, Filter.SelectedStatus.Value, from, to);
            var result = await _operationService.GetFeeOperationsAsync(
                filter, page ?? Page, OperationDisplay.PageSize, cancellationToken);

            Rows.Clear();
            foreach (var item in result.Items)
            {
                Rows.Add(new FeeOperationRowModel
                {
                    Id = item.Id,
                    Customer = OperationDisplay.OrEmpty(item.CustomerName),
                    CustomerNo = OperationDisplay.OrEmpty(item.CustomerNo),
                    AccountNo = item.AccountNo,
                    AccountType = item.AccountTypeCode,
                    FeeType = OperationDisplay.FormatFeeType(item.FeeType),
                    Period = OperationDisplay.FormatPeriod(item.PeriodStart, item.PeriodEnd),
                    Amount = OperationDisplay.FormatAmount(item.Amount),
                    Tax = OperationDisplay.FormatAmount(item.TaxAmount),
                    Status = item.Status.ToString(),
                    PostedAt = OperationDisplay.FormatOptionalDateTime(item.PostedAt)
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
        if (parameter is not FeeOperationRowModel row)
        {
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            var detail = await _operationService.GetFeeOperationByIdAsync(row.Id, CancellationToken.None);
            _dialogService.ShowDialog(new FeeOperationDetailsViewModel(detail));
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
