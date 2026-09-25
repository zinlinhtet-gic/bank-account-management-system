using bams.desktop.Commands;
using bams.desktop.Exceptions;
using bams.desktop.Utils;
using bams.desktop.ViewModels.Pages.Customers;

namespace bams.desktop.ViewModels.Pages;

/// <summary>
/// ViewModel for the Customer List page. Coordinates the <see cref="Filter"/> and <see cref="Table"/>
/// components: a filter change reloads the table's first page.
/// </summary>
public sealed class CustomerListViewModel : ViewModelBase, IAsyncInitializable
{
    private CancellationTokenSource? _loadCancellation;
    private string _errorMessage = string.Empty;

    public CustomerListViewModel(CustomerFilterViewModel filter, CustomerTableViewModel table)
    {
        // Constructors only store dependencies and create commands. No server calls here.
        Filter = filter;
        Table = table;

        // The parent coordinates its components: a filter change reloads the table.
        Filter.FiltersChanged += OnFiltersChanged;

        RefreshCommand = new AsyncRelayCommand(() => ReloadFirstPageAsync(CancellationToken.None));
    }

    // Kept for the placeholder view; the header already shows the page name.
    public string PageTitle => "Customer List";
    public string PageDescription => "View and search customer records";

    public CustomerFilterViewModel Filter { get; }

    public CustomerTableViewModel Table { get; }

    public AsyncRelayCommand RefreshCommand { get; }

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
        return ReloadFirstPageAsync(cancellationToken);
    }

    // async void is intentional: an event handler; ReloadFirstPageAsync catches every expected failure itself.
    private async void OnFiltersChanged()
    {
        await ReloadFirstPageAsync(CancellationToken.None);
    }

    // Reloads the table's first page with the current filters. A newer reload cancels one still running,
    // so quickly changing filters never shows an older result last.
    private async Task ReloadFirstPageAsync(CancellationToken cancellationToken)
    {
        var (filter, error) = Filter.BuildFilter();
        if (error is not null)
        {
            ErrorMessage = MessageCatalog.GetMessage(error.Value);
            return;
        }

        _loadCancellation?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = cancellation;

        try
        {
            ErrorMessage = string.Empty;
            await Table.LoadFirstPageAsync(filter!, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Replaced by a newer reload, or the user left the page.
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
            }
        }
    }
}
