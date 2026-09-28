using bams.desktop.Commands;
using bams.desktop.Exceptions;
using bams.desktop.Utils;
using bams.desktop.ViewModels.Pages.Customers;

namespace bams.desktop.ViewModels.Pages;

/// <summary>
/// ViewModel for the Customer List page. Coordinates the <see cref="Filter"/> and <see cref="Table"/>
/// components (a filter change reloads the table's first page) and the <see cref="CreateForm"/>
/// component (switches the page between the table and the create-customer form).
/// </summary>
public sealed class CustomerListViewModel : ViewModelBase, IAsyncInitializable
{
    private CancellationTokenSource? _loadCancellation;
    private string _errorMessage = string.Empty;
    private bool _isShowingCreateForm;

    public CustomerListViewModel(CustomerFilterViewModel filter, CustomerTableViewModel table, CustomerCreateViewModel createForm)
    {
        // Constructors only store dependencies and create commands. No server calls here.
        Filter = filter;
        Table = table;
        CreateForm = createForm;

        // The parent coordinates its components: a filter change reloads the table, and the create
        // form finishing (Cancel or a successful Save) switches the page back to the table.
        Filter.FiltersChanged += OnFiltersChanged;
        CreateForm.Completed += OnCreateFormCompleted;

        RefreshCommand = new AsyncRelayCommand(() => ReloadFirstPageAsync(CancellationToken.None));
        ShowCreateFormCommand = new RelayCommand(_ =>
        {
            CreateForm.Reset();
            IsShowingCreateForm = true;
        });
    }

    // Kept for the placeholder view; the header already shows the page name.
    public string PageTitle => "Customer List";
    public string PageDescription => "View and search customer records";

    public CustomerFilterViewModel Filter { get; }

    public CustomerTableViewModel Table { get; }

    public CustomerCreateViewModel CreateForm { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public RelayCommand ShowCreateFormCommand { get; }

    /// <summary>True while the create-customer form replaces the table.</summary>
    public bool IsShowingCreateForm
    {
        get => _isShowingCreateForm;
        private set
        {
            if (SetProperty(ref _isShowingCreateForm, value))
            {
                OnPropertyChanged(nameof(IsShowingList));
            }
        }
    }

    public bool IsShowingList => !IsShowingCreateForm;

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

    // The create form finished (Cancel, or a successful Save): show the table again, refreshing it
    // so a newly created customer appears immediately. async void is intentional: an event handler.
    private async void OnCreateFormCompleted(object? sender, EventArgs e)
    {
        IsShowingCreateForm = false;
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
