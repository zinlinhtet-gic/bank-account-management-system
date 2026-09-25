using bams.desktop.Commands;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;
using bams.desktop.ViewModels.Pages.Transactions;

namespace bams.desktop.ViewModels.Pages;

/// <summary>
/// ViewModel for the Transaction History page (auditors, <c>transaction_history</c>): a read-only, filterable
/// transaction table with the detail card. Uses the same components as the Transactions page, without the actions.
/// </summary>
public sealed class TransactionHistoryViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ITransactionService _transactionService;
    private readonly TransactionDetailsLauncher _detailsLauncher;

    private string _errorMessage = string.Empty;

    public TransactionHistoryViewModel(
        ITransactionService transactionService,
        TransactionDetailsLauncher detailsLauncher,
        TransactionFilterViewModel filter,
        TransactionListViewModel list)
    {
        _transactionService = transactionService;
        _detailsLauncher = detailsLauncher;
        Filter = filter;
        List = list;

        Filter.FiltersChanged += OnFiltersChanged;
        List.PageRequested += OnPageRequested;

        RefreshCommand = new AsyncRelayCommand(() => ReloadAsync(List.Page, CancellationToken.None));
        ShowDetailsCommand = new AsyncRelayCommand(ShowDetailsAsync);
    }

    // Kept for the placeholder view; the header already shows the page name.
    public string PageTitle => "Transaction History";
    public string PageDescription => "View historical transaction records";

    public TransactionFilterViewModel Filter { get; }

    public TransactionListViewModel List { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>Row click: parameter is the clicked <see cref="TransactionDisplayModel"/>.</summary>
    public AsyncRelayCommand ShowDetailsCommand { get; }

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
        return ReloadAsync(TransactionListViewModel.FirstPageNumber, cancellationToken);
    }

    // async void is intentional: event handlers; ReloadAsync catches every expected failure itself.
    private async void OnFiltersChanged()
    {
        await ReloadAsync(TransactionListViewModel.FirstPageNumber, CancellationToken.None);
    }

    private async void OnPageRequested(int page)
    {
        await ReloadAsync(page, CancellationToken.None);
    }

    // Reloads the table page with the current filters and shows a failure in the banner.
    private async Task ReloadAsync(int page, CancellationToken cancellationToken)
    {
        var (filter, error) = Filter.BuildFilter();
        if (error is not null)
        {
            ErrorMessage = MessageCatalog.GetMessage(error.Value);
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            await List.LoadAsync(filter!, page, cancellationToken);
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    // Loads the full record and shows it; the card can continue to an account statement.
    private async Task ShowDetailsAsync(object? parameter)
    {
        if (parameter is not TransactionDisplayModel row)
        {
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            var transaction = await _transactionService.GetTransactionByIdAsync(row.Id, CancellationToken.None);
            ErrorMessage = await _detailsLauncher.ShowAsync(transaction) ?? string.Empty;
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
    }
}
