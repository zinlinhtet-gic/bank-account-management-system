using System.Collections.ObjectModel;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;
using bams.desktop.Exceptions;

namespace bams.desktop.ViewModels.Pages.Accounting;
/// <summary>
/// Loads and exposes General Ledger accounts for the Accounting section.
/// </summary>
public sealed class GeneralLedgerViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IAccountingService _accountingService;
    private bool _isLoading;
    private string? _errorMessage;
    public GeneralLedgerViewModel(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }
    public ObservableCollection<GlAccountResponse> Accounts { get; } = [];
    public string PageTitle => "General Ledger";
    public string PageDescription => "View general ledger accounts and classifications";
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if(SetProperty(ref _isLoading, value))
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
            if(SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }
    private bool IsEmpty => !IsLoading && Accounts.Count == 0;
    private bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public string CountText => Accounts.Count == 1 ? "1 account" : $"{Accounts.Count} accounts";
    /// <summary>
    /// Loads General Ledger accounts when the page is opened.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            var accounts = await _accountingService.GetGlAccountsAsync(cancellationToken);
            Accounts.Clear();
            foreach(var account in accounts)
            {
                Accounts.Add(account);
            }
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(CountText));
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}