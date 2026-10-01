using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>
/// Loads, filters and exposes General Ledger accounts
/// for the Accounting section.
/// </summary>
public sealed class GeneralLedgerViewModel :ViewModelBase,IAsyncInitializable
{
    private readonly IAccountingService _accountingService;
    // Keeps the complete server result.
    // The Accounts collection contains only the currently filtered rows.
    private readonly List<GlAccountDisplayModel> _allAccounts = [];
    private bool _isLoading;
    private string? _errorMessage;
    private string _searchText = string.Empty;
    private FilterOption<GlAccountClass>? _selectedAccountClass;
    private FilterOption<string>? _selectedStatus;
    /// <summary>
    /// Represents one selectable filter option.
    /// A null Value means no filtering / all values.
    /// </summary>
    public sealed record FilterOption<T>(string Label,T Value,bool IsAll = false);
    public GeneralLedgerViewModel(IAccountingService accountingService)
    {
        _accountingService = accountingService;
        // Select "All" by default.
        _selectedAccountClass = AccountClassOptions[0];
        _selectedStatus = StatusOptions[0];
        RefreshCommand = new AsyncRelayCommand(async _ =>await LoadAsync(CancellationToken.None));
        ResetFiltersCommand =new RelayCommand(_ => ResetFilters());
    }
    /// <summary>
    /// Rows currently visible in the General Ledger table.
    /// </summary>
    public ObservableCollection<GlAccountDisplayModel> Accounts { get; } = [];
    /// <summary>
    /// Account-class options shown in the filter dropdown.
    /// </summary>
    public IReadOnlyList<FilterOption<GlAccountClass>> AccountClassOptions { get; } =
    [
        new("All classes", default, true),
        new("Asset", GlAccountClass.Asset),
        new("Liability", GlAccountClass.Liability),
        new("Equity", GlAccountClass.Equity),
        new("Income", GlAccountClass.Income),
        new("Expense", GlAccountClass.Expense)
    ];
    /// <summary>
    /// Account-status options shown in the filter dropdown.
    /// </summary>
    public IReadOnlyList<FilterOption<string>> StatusOptions { get; } =
    [
        new("All statuses", string.Empty, true),
        new("Active", "Active"),
        new("Inactive", "Inactive")
    ];
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText,value))
            {
                ApplyFilters();
            }
        }
    }

    public FilterOption<GlAccountClass>? SelectedAccountClass
    {
        get => _selectedAccountClass;
        set
        {
            if (SetProperty(ref _selectedAccountClass,value))
            {
                ApplyFilters();
            }
        }
    }
    public FilterOption<string>? SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus,value))
            {
                ApplyFilters();
            }
        }
    }
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading,value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage,value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }
    /// <summary>
    /// Indicates whether the current filtered result contains no accounts.
    /// </summary>
    public bool IsEmpty => !IsLoading && Accounts.Count == 0;
    /// <summary>
    /// Indicates whether an API or network error should be shown.
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    /// <summary>
    /// Counter displayed in the General Ledger table caption.
    /// </summary>
    public string CountText => Accounts.Count == 1
            ? "1 account"
            : $"{Accounts.Count} accounts";
    /// <summary>
    /// Loads General Ledger data when the page is opened.
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(cancellationToken);
    }
    /// <summary>
    /// Retrieves the latest General Ledger accounts from the server
    /// and reapplies the current filters.
    /// </summary>
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var accounts =
                await _accountingService
                    .GetGlAccountsAsync(
                        cancellationToken);

            // Used to turn ParentId into a readable
            // "CODE - Account Name" value.
            var accountsById =
                accounts.ToDictionary(
                    account => account.Id);

            _allAccounts.Clear();

            foreach (var account in accounts)
            {
                var parentDisplay = "-";

                if (account.ParentId.HasValue &&
                    accountsById.TryGetValue(
                        account.ParentId.Value,
                        out var parent))
                {
                    parentDisplay =
                        $"{parent.Code} - {parent.Name}";
                }

                _allAccounts.Add(
                    new GlAccountDisplayModel
                    {
                        Id = account.Id,
                        Code = account.Code,
                        Name = account.Name,
                        AccountClass =
                            account.AccountClass,
                        ParentId =
                            account.ParentId,
                        ParentDisplay =
                            parentDisplay,
                        Status =
                            account.Status
                    });
            }

            // Refresh does not clear the user's filters.
            // It applies them again to the latest server data.
            ApplyFilters();
        }
        catch (AppException exception)
        {
            ErrorMessage =
                exception.Message;
        }
        finally
        {
            IsLoading = false;

            OnPropertyChanged(
                nameof(IsEmpty));

            OnPropertyChanged(
                nameof(CountText));
        }
    }

    /// <summary>
    /// Applies search, account-class and status filters
    /// to the locally loaded General Ledger rows.
    /// </summary>
    private void ApplyFilters()
    {
        IEnumerable<GlAccountDisplayModel> query =_allAccounts;
        // Search by GL code or account name.
        if (!string.IsNullOrWhiteSpace(
                SearchText))
        {
            var search =SearchText.Trim();
            query =
                query.Where(
                    account =>
                        account.Code.Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase) ||
                        account.Name.Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase));
        }

        // Null means "All classes".
        if (SelectedAccountClass is not null && !SelectedAccountClass.IsAll)
        {
            query = query.Where(account =>account.AccountClass == SelectedAccountClass.Value);
        }

        // Null means "All statuses".
        if (SelectedStatus is not null && !SelectedStatus.IsAll)
        {
            query = query.Where( account =>string.Equals(account.Status,SelectedStatus.Value,StringComparison.Ordinal));
        }

        Accounts.Clear();
        foreach (var account in query)
        {
            Accounts.Add(account);
        }
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountText));
    }

    /// <summary>
    /// Clears all General Ledger filters without making
    /// another server request.
    /// </summary>
    private void ResetFilters()
    {
        SearchText = string.Empty;
        SelectedAccountClass =AccountClassOptions[0];
        SelectedStatus = StatusOptions[0];
    }
}