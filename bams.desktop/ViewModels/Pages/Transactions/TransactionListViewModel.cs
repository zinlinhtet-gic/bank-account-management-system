using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Models;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// A transaction table: one page of rows, the pager, loading and empty state. The page asks for loads and shows
/// errors; a newer load cancels one still running, so quickly changing filters never shows an older result last.
/// </summary>
public sealed class TransactionListViewModel : ViewModelBase
{
    private const int FirstPage = 1;

    private readonly ITransactionService _transactionService;

    private CancellationTokenSource? _loadCancellation;
    private bool _isLoading;
    private int _page = FirstPage;
    private int _totalCount;

    public TransactionListViewModel(ITransactionService transactionService)
    {
        _transactionService = transactionService;

        PreviousPageCommand = new RelayCommand(_ => PageRequested?.Invoke(Page - 1), _ => CanGoToPreviousPage);
        NextPageCommand = new RelayCommand(_ => PageRequested?.Invoke(Page + 1), _ => CanGoToNextPage);
    }

    /// <summary>
    /// Raised when the user asks for another page; the page reloads with its current filters.
    /// </summary>
    public event Action<int>? PageRequested;

    public ObservableCollection<TransactionDisplayModel> Transactions { get; } = [];

    public RelayCommand PreviousPageCommand { get; }

    public RelayCommand NextPageCommand { get; }

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

    /// <summary>True when a finished load returned no rows (shows the "no transactions" message).</summary>
    public bool IsEmpty => !IsLoading && Transactions.Count == 0;

    /// <summary>The page currently shown, starting at 1.</summary>
    public int Page
    {
        get => _page;
        private set => SetProperty(ref _page, value);
    }

    /// <summary>Counter shown next to the table title, e.g. "45 transactions".</summary>
    public string CountText => _totalCount == 1 ? "1 transaction" : $"{_totalCount} transactions";

    /// <summary>e.g. "Page 2 of 3".</summary>
    public string PageText => $"Page {Page} of {TotalPages}";

    private int TotalPages => Math.Max(FirstPage, (int)Math.Ceiling(_totalCount / (double)TransactionFieldRules.PageSize));

    private bool CanGoToPreviousPage => !IsLoading && Page > FirstPage;

    private bool CanGoToNextPage => !IsLoading && Page < TotalPages;

    /// <summary>
    /// Loads one page of transactions matching the filter and replaces the rows only after the call succeeds.
    /// Returns quietly when a newer load replaced this one.
    /// </summary>
    /// <exception cref="Exceptions.AppException">Server or network failure; the page shows the message.</exception>
    public async Task LoadAsync(TransactionListFilter filter, int page, CancellationToken cancellationToken)
    {
        _loadCancellation?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = cancellation;

        try
        {
            IsLoading = true;
            RaisePagingChanged();

            var result = await _transactionService.GetTransactionsAsync(
                filter,
                Math.Max(FirstPage, page),
                TransactionFieldRules.PageSize,
                cancellation.Token);

            Transactions.Clear();
            foreach (var transaction in result.Items)
            {
                Transactions.Add(TransactionDisplayModel.FromResponse(transaction));
            }

            Page = result.Page;
            _totalCount = result.TotalCount;
            OnPropertyChanged(nameof(CountText));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Replaced by a newer load, or the user left the page.
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
                IsLoading = false;
                RaisePagingChanged();
            }
        }
    }

    /// <summary>The first page number, for loads after a filter change or a new posting.</summary>
    public static int FirstPageNumber => FirstPage;

    // The pager text and buttons depend on the page, total and loading state.
    private void RaisePagingChanged()
    {
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(IsEmpty));
        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }
}
