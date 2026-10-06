using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;
using bams.desktop.ViewModels.Pages.Transactions;

namespace bams.desktop.ViewModels.Pages.Audit;

/// <summary>
/// Read-only transaction investigation page for auditors.
/// Owns the transaction search/list and requests navigation to
/// a separate transaction-audit detail page.
/// </summary>
public sealed class TransactionAuditViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ITransactionService _transactionService;

    private string _errorMessage = string.Empty;
    private bool _isLoadingDetail;

    public TransactionAuditViewModel(
        ITransactionService transactionService,
        TransactionFilterViewModel filter,
        TransactionListViewModel list)
    {
        _transactionService = transactionService;

        Filter = filter;
        List = list;

        Filter.FiltersChanged += OnFiltersChanged;
        List.PageRequested += OnPageRequested;

        RefreshCommand = new AsyncRelayCommand(
            () => ReloadAsync(List.Page, CancellationToken.None));

        SelectTransactionCommand =
            new AsyncRelayCommand(SelectTransactionAsync);
    }

    /// <summary>
    /// Raised after the selected transaction has been fully loaded.
    /// MainViewModel handles the actual page navigation.
    /// </summary>
    public event Action<TransactionAuditDetailViewModel>? DetailRequested;

    public TransactionFilterViewModel Filter { get; }

    public TransactionListViewModel List { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand SelectTransactionCommand { get; }

    public bool IsLoadingDetail
    {
        get => _isLoadingDetail;
        private set => SetProperty(ref _isLoadingDetail, value);
    }

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

    public bool HasError =>
        !string.IsNullOrWhiteSpace(ErrorMessage);

    public Task InitializeAsync(
        CancellationToken cancellationToken)
    {
        return ReloadAsync(
            TransactionListViewModel.FirstPageNumber,
            cancellationToken);
    }

    private async void OnFiltersChanged()
    {
        await ReloadAsync(
            TransactionListViewModel.FirstPageNumber,
            CancellationToken.None);
    }

    private async void OnPageRequested(int page)
    {
        await ReloadAsync(
            page,
            CancellationToken.None);
    }

    private async Task ReloadAsync(
        int page,
        CancellationToken cancellationToken)
    {
        var (filter, error) = Filter.BuildFilter();

        if (error is not null)
        {
            ErrorMessage =
                MessageCatalog.GetMessage(error.Value);

            return;
        }

        try
        {
            ErrorMessage = string.Empty;

            await List.LoadAsync(
                filter!,
                page,
                cancellationToken);
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    /// <summary>
    /// Loads the selected transaction and asks the shell to navigate
    /// to its standalone audit-detail page.
    /// </summary>
    private async Task SelectTransactionAsync(
        object? parameter)
    {
        if (parameter is not TransactionDisplayModel row)
        {
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            IsLoadingDetail = true;

            var transaction =
                await _transactionService.GetTransactionByIdAsync(
                    row.Id,
                    CancellationToken.None);

            var detailViewModel =
                new TransactionAuditDetailViewModel(transaction);

            DetailRequested?.Invoke(detailViewModel);
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoadingDetail = false;
        }
    }
}