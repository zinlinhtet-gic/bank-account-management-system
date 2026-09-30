using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Exceptions;
using bams.desktop.Models.Configuration;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// ViewModel for the Bank Policies list page.
public sealed class BankPoliciesViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IBankPolicyService _bankPolicyService;
    private readonly IDialogService _dialogService;
    private string _errorMessage = string.Empty;
    private bool _isLoading;

    public BankPoliciesViewModel(IBankPolicyService bankPolicyService, IDialogService dialogService)
    {
        // Constructors only store dependencies. No server calls here.
        _bankPolicyService = bankPolicyService;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
        CreateBankPolicyCommand = new AsyncRelayCommand(CreateBankPolicyAsync);
        EditBankPolicyCommand = new AsyncRelayCommand(EditBankPolicyAsync);
    }

    public string PageTitle => "Bank Policies";
    public string PageDescription => "Opening balance, minimum maintained balance and limits per account type.";

    public ObservableCollection<BankPolicyRowModel> Rows { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand CreateBankPolicyCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="BankPolicyRowModel"/>.</summary>
    public AsyncRelayCommand EditBankPolicyCommand { get; }

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

    /// <summary>True when a finished load returned no rows (shows the "no policies" message).</summary>
    public bool IsEmpty => !IsLoading && Rows.Count == 0;

    /// <summary>Counter shown next to the table title, e.g. "3 policies".</summary>
    public string CountText => Rows.Count == 1 ? "1 policy" : $"{Rows.Count} policies";

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

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        try
        {
            IsLoading = true;

            var policies = await _bankPolicyService.GetBankPoliciesAsync(cancellationToken);

            Rows.Clear();
            foreach (var policy in policies)
            {
                Rows.Add(new BankPolicyRowModel
                {
                    AccountType = $"{policy.Code} - {policy.Name}",
                    MinOpeningBalance = policy.MinimumOpeningBalance.ToString("N0"),
                    MinMaintainedBalance = policy.MinimumMaintainedBalance.ToString("N0"),
                    DailyLimit = policy.DailyTransactionLimit?.ToString("N0") ?? "-",
                    Status = policy.Status,
                    Source = policy
                });
            }

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

    // Opens the empty form; after a successful save, reloads the list.
    private async Task CreateBankPolicyAsync()
    {
        ErrorMessage = string.Empty;

        var form = new BankPolicyFormViewModel(_bankPolicyService, existingPolicy: null);
        if (!_dialogService.ShowDialog(form))
        {
            return;
        }

        await LoadAsync(CancellationToken.None);
    }

    // Opens the form filled with the row's policy; after a successful save, reloads the list.
    private async Task EditBankPolicyAsync(object? parameter)
    {
        if (parameter is not BankPolicyRowModel row)
        {
            return;
        }

        ErrorMessage = string.Empty;

        var form = new BankPolicyFormViewModel(_bankPolicyService, row.Source);
        if (!_dialogService.ShowDialog(form))
        {
            return;
        }

        await LoadAsync(CancellationToken.None);
    }
}
