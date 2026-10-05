using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.ViewModels.Pages.Transactions;
using bams.desktop.Utils;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Audit;

/// <summary>
/// Read-only transaction investigation page for auditors.
/// Reuses the shared transaction filter and paged transaction list.
/// Detailed transaction inspection is added separately.
/// </summary>
public sealed class TransactionAuditViewModel : ViewModelBase, IAsyncInitializable
{
    private string _errorMessage = string.Empty;
    private readonly ITransactionService _transactionService;
    public TransactionAuditViewModel(
        ITransactionService transactionService,
        TransactionFilterViewModel filter,
        TransactionListViewModel list)
    {
        _transactionService = transactionService;
        SelectTransactionCommand = new AsyncRelayCommand(SelectTransactionAsync);
        Filter = filter;
        List = list;

        // The parent coordinates its child components.
        Filter.FiltersChanged += OnFiltersChanged;
        List.PageRequested += OnPageRequested;

        RefreshCommand = new AsyncRelayCommand(
            () => ReloadAsync(List.Page, CancellationToken.None));
    }

    public string PageTitle => "Transaction Audit";

    public string PageDescription =>
        "Review transaction activity, posting details and audit evidence";

    public TransactionFilterViewModel Filter { get; }

    public TransactionListViewModel List { get; }

    private TransactionAuditDetailViewModel? _detail;
    private bool _isLoadingDetail;
    public TransactionAuditDetailViewModel? Detail
    {
        get => _detail;
        private set
        {
            if(SetProperty(ref _detail, value))
            {
                OnPropertyChanged(nameof(HasDetail));
            }
        }
    }
    public bool HasDetail => Detail is not null;
    public bool IsLoadingDetail
    {
        get => _isLoadingDetail;
        private set => SetProperty(ref _isLoadingDetail, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand SelectTransactionCommand { get; }

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

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Loads the first transaction page whenever the auditor opens the page.
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return ReloadAsync(
            TransactionListViewModel.FirstPageNumber,
            cancellationToken);
    }

    // Event handler intentionally uses async void.
    private async void OnFiltersChanged()
    {
        await ReloadAsync(
            TransactionListViewModel.FirstPageNumber,
            CancellationToken.None);
    }

    // Event handler intentionally uses async void.
    private async void OnPageRequested(int page)
    {
        await ReloadAsync(page, CancellationToken.None);
    }

    /// <summary>
    /// Loads a transaction page using the current filter state.
    /// </summary>
    private async Task ReloadAsync(
        int page,
        CancellationToken cancellationToken)
    {
        var (filter, error) = Filter.BuildFilter();

        if (error is not null)
        {
            ErrorMessage = MessageCatalog.GetMessage(error.Value);
            return;
        }
        Detail = null;
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
    /// Loads the full transaction selected in the audit table.
    /// </summary>
    private async Task SelectTransactionAsync(object? parameter)
    {
        if (parameter is not TransactionDisplayModel row)
        {
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            IsLoadingDetail = true;

            var transaction = await _transactionService.GetTransactionByIdAsync(
                    row.Id,
                    CancellationToken.None);

            Detail = new TransactionAuditDetailViewModel(transaction);
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