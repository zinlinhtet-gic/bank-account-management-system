using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Exceptions;
using bams.desktop.Models.Configuration;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// ViewModel for the Other Banks list page.
public sealed class OtherBanksViewModel : ViewModelBase, IAsyncInitializable
{
    private const int FirstPage = 1;
    private const int PageSize = 10;

    private readonly IOtherBankService _otherBankService;
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private int _page = FirstPage;
    private int _totalCount;

    public OtherBanksViewModel(IOtherBankService otherBankService)
    {
        // Constructors only store dependencies. No server calls here.
        _otherBankService = otherBankService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
        PreviousPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page - 1), () => CanGoToPreviousPage);
        NextPageCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None, Page + 1), () => CanGoToNextPage);
    }

    public string PageTitle => "Other Banks";
    public string PageDescription => "View only. Correspondent bank records are managed by back-office operations.";

    public ObservableCollection<OtherBankRowModel> Rows { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand PreviousPageCommand { get; }

    public AsyncRelayCommand NextPageCommand { get; }

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

    /// <summary>True when a finished load returned no rows (shows the "no banks" message).</summary>
    public bool IsEmpty => !IsLoading && Rows.Count == 0;

    /// <summary>Counter shown next to the table title, e.g. "3 banks".</summary>
    public string CountText => _totalCount == 1 ? "1 bank" : $"{_totalCount} banks";

    /// <summary>The page currently shown, starting at 1.</summary>
    public int Page
    {
        get => _page;
        private set => SetProperty(ref _page, value);
    }

    /// <summary>e.g. "Page 2 of 3".</summary>
    public string PageText => $"Page {Page} of {TotalPages}";

    private int TotalPages => Math.Max(FirstPage, (int)Math.Ceiling(_totalCount / (double)PageSize));

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
        return LoadAsync(cancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken, int? page = null)
    {
        ErrorMessage = string.Empty;

        try
        {
            IsLoading = true;
            RaisePagingChanged();

            var result = await _otherBankService.GetOtherBanksAsync(page ?? Page, cancellationToken);

            Rows.Clear();
            foreach (var bank in result.Items)
            {
                Rows.Add(new OtherBankRowModel
                {
                    BankCode = bank.BankCode,
                    BankName = bank.BankName,
                    SwiftCode = bank.SwiftCode ?? string.Empty,
                    Status = bank.Status
                });
            }

            Page = result.Page;
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

    // The pager text and buttons depend on the page, total and loading state.
    private void RaisePagingChanged()
    {
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(IsEmpty));
        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }
}
