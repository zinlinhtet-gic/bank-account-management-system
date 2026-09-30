using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using bams.desktop.Commands;
using bams.desktop.DTOs.Customers;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;
using bams.desktop.ViewModels.Pages.Customers;

namespace bams.desktop.ViewModels.Pages;

/// <summary>
/// ViewModel for the Customer List page. Coordinates the <see cref="Filter"/> and <see cref="Table"/>
/// components (a filter change reloads the table's first page) and the <see cref="CreateForm"/>
/// component (switches the page between the table and the create-customer form). Also opens the
/// read-only customer details dialog for a row's "View" action.
/// </summary>
public sealed class CustomerListViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ICustomerService _customerService;
    private readonly IDialogService _dialogService;

    private CancellationTokenSource? _loadCancellation;
    private string _errorMessage = string.Empty;
    private bool _isShowingCreateForm;

    public CustomerListViewModel(
        CustomerFilterViewModel filter,
        CustomerTableViewModel table,
        CustomerCreateViewModel createForm,
        ICustomerService customerService,
        IDialogService dialogService)
    {
        // Constructors only store dependencies and create commands. No server calls here.
        Filter = filter;
        Table = table;
        CreateForm = createForm;
        _customerService = customerService;
        _dialogService = dialogService;

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
        ShowCustomerDetailsCommand = new AsyncRelayCommand(ShowCustomerDetailsAsync);
        EditCustomerCommand = new AsyncRelayCommand(EditCustomerAsync);
    }

    // Kept for the placeholder view; the header already shows the page name.
    public string PageTitle => "Customer List";
    public string PageDescription => "View and search customer records";

    public CustomerFilterViewModel Filter { get; }

    public CustomerTableViewModel Table { get; }

    public CustomerCreateViewModel CreateForm { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public RelayCommand ShowCreateFormCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="CustomerDisplayModel"/>.</summary>
    public AsyncRelayCommand ShowCustomerDetailsCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="CustomerDisplayModel"/>. Opens the same
    /// create form, pre-filled and in edit mode (see <see cref="CustomerCreateViewModel.LoadForEdit"/>).</summary>
    public AsyncRelayCommand EditCustomerCommand { get; }

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

    // Loads the full record (not the possibly-stale row) and shows it in a read-only detail dialog.
    private async Task ShowCustomerDetailsAsync(object? parameter)
    {
        if (parameter is not CustomerDisplayModel row)
        {
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            var customer = await _customerService.GetCustomerByIdAsync(row.Id, CancellationToken.None);
            var photo = await TryLoadPhotoAsync(customer);

            _dialogService.ShowDialog(new Customers.CustomerDetailsViewModel(customer, photo, _customerService, _dialogService));
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    // Loads the customer's Photo document, if any, as an image for the details header. A missing
    // photo or a failed download both just fall back to the default icon (see CustomerDetailsViewModel),
    // rather than stopping the details dialog from opening.
    private async Task<ImageSource?> TryLoadPhotoAsync(CustomerResponse customer)
    {
        var photoDocument = customer.Documents.FirstOrDefault(document =>
            document.DocumentType == DocumentType.Photo && !string.IsNullOrWhiteSpace(document.FileReference));

        if (photoDocument is null)
        {
            return null;
        }

        try
        {
            var bytes = await _customerService.GetCustomerDocumentBytesAsync(customer.Id, photoDocument.Id, CancellationToken.None);

            return CreateImage(bytes);
        }
        catch (AppException)
        {
            return null;
        }
    }

    // Decodes raw image bytes once and freezes the result, so it is safe to bind from a UI-bound property.
    private static ImageSource CreateImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();

        return image;
    }

    // Loads the full record and opens it in the create form's edit mode (same view, pre-filled).
    private async Task EditCustomerAsync(object? parameter)
    {
        if (parameter is not CustomerDisplayModel row)
        {
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            var customer = await _customerService.GetCustomerByIdAsync(row.Id, CancellationToken.None);
            CreateForm.LoadForEdit(customer);
            IsShowingCreateForm = true;
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
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
