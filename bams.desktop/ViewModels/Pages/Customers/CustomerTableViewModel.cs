using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Customers;
using bams.desktop.Models;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Customers;

/// <summary>
/// The customer table: its rows, loading state, empty state and pagination. Errors are passed back
/// to the page, which shows them.
/// </summary>
public sealed class CustomerTableViewModel : ViewModelBase
{
    private readonly ICustomerService _customerService;

    private CustomerListFilter _lastFilter = new(null, null, null, null, null, null, null);
    private bool _isLoading;
    private int _pageNumber = 1;
    private int _totalPages = 1;
    private int _totalCount;

    public CustomerTableViewModel(ICustomerService customerService)
    {
        _customerService = customerService;

        NextPageCommand = new AsyncRelayCommand(() => LoadPageAsync(_lastFilter, PageNumber + 1, CancellationToken.None), () => CanGoToNextPage);
        PreviousPageCommand = new AsyncRelayCommand(() => LoadPageAsync(_lastFilter, PageNumber - 1, CancellationToken.None), () => CanGoToPreviousPage);
    }

    public ObservableCollection<CustomerDisplayModel> Customers { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                RaisePagingCommandsChanged();
            }
        }
    }

    /// <summary>True when a finished load returned no rows (shows the "no customers" message).</summary>
    public bool IsEmpty => !IsLoading && Customers.Count == 0;

    /// <summary>Counter shown next to the table title, e.g. "12 customers".</summary>
    public string CountText => _totalCount == 1 ? "1 customer" : $"{_totalCount} customers";

    public int PageNumber
    {
        get => _pageNumber;
        private set
        {
            if (SetProperty(ref _pageNumber, value))
            {
                OnPropertyChanged(nameof(PageIndicatorText));
                RaisePagingCommandsChanged();
            }
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (SetProperty(ref _totalPages, value))
            {
                OnPropertyChanged(nameof(PageIndicatorText));
                RaisePagingCommandsChanged();
            }
        }
    }

    /// <summary>e.g. "Page 1 of 4".</summary>
    public string PageIndicatorText => $"Page {PageNumber} of {Math.Max(TotalPages, 1)}";

    public bool CanGoToPreviousPage => !IsLoading && PageNumber > 1;

    public bool CanGoToNextPage => !IsLoading && PageNumber < TotalPages;

    public AsyncRelayCommand NextPageCommand { get; }

    public AsyncRelayCommand PreviousPageCommand { get; }

    /// <summary>
    /// Loads page 1 with the given filter. Used on the page's initial load and whenever the filters change.
    /// </summary>
    /// <exception cref="Exceptions.AppException">Server or network failure; the page shows the message.</exception>
    public Task LoadFirstPageAsync(CustomerListFilter filter, CancellationToken cancellationToken)
    {
        return LoadPageAsync(filter, pageNumber: 1, cancellationToken);
    }

    // Loads one page of customers and replaces the rows only after the call succeeds.
    private async Task LoadPageAsync(CustomerListFilter filter, int pageNumber, CancellationToken cancellationToken)
    {
        _lastFilter = filter;

        try
        {
            IsLoading = true;

            var request = new GetCustomersRequest(
                pageNumber,
                filter.CustomerNo,
                filter.CustomerName,
                filter.KycStatus,
                filter.Status,
                filter.RiskLevel,
                filter.StartDate,
                filter.EndDate);

            var page = await _customerService.GetCustomersAsync(request, cancellationToken);

            // "No" numbers rows across every page, e.g. row 1 of page 2 is 11 at the default page size.
            var rowOffset = (page.PageNumber - 1) * page.PageSize;

            Customers.Clear();
            for (var index = 0; index < page.Items.Count; index++)
            {
                Customers.Add(CustomerDisplayModel.FromResponse(page.Items[index], rowOffset + index + 1));
            }

            _totalCount = page.TotalCount;
            OnPropertyChanged(nameof(CountText));

            PageNumber = page.PageNumber;
            TotalPages = Math.Max(page.TotalPages, 1);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RaisePagingCommandsChanged()
    {
        NextPageCommand.RaiseCanExecuteChanged();
        PreviousPageCommand.RaiseCanExecuteChanged();
    }
}
