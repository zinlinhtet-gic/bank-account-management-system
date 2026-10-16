using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Starts today's Myanmar business date together with its opening vault balance.</summary>
public sealed class BusinessDateOpeningViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IEndOfDayClientService _businessDates;
    private readonly ICashOperationsClientService _cashOperations;
    private readonly AuthContext _authContext;
    private BusinessDateResponse? _businessDate;
    private CashPositionSessionResponse? _vaultSession;
    private string _startingCashText = string.Empty;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private bool _isBusy;

    public AsyncRelayCommand StartBusinessDateCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public BusinessDateOpeningViewModel(IEndOfDayClientService businessDates,
        ICashOperationsClientService cashOperations, AuthContext authContext)
    {
        _businessDates = businessDates;
        _cashOperations = cashOperations;
        _authContext = authContext;
        StartBusinessDateCommand = new AsyncRelayCommand(StartBusinessDateAsync, () => CanStartBusinessDate);
        RefreshCommand = new AsyncRelayCommand(() => InitializeAsync(CancellationToken.None), () => !IsBusy);
    }

    public BusinessDateResponse? BusinessDate { get => _businessDate; private set => SetProperty(ref _businessDate, value); }
    public CashPositionSessionResponse? VaultSession { get => _vaultSession; private set => SetProperty(ref _vaultSession, value); }
    public string StartingCashText { get => _startingCashText; set { if (SetProperty(ref _startingCashText, value)) ErrorMessage = string.Empty; } }
    public string ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public string SuccessMessage { get => _successMessage; private set { if (SetProperty(ref _successMessage, value)) OnPropertyChanged(nameof(HasSuccess)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanStartBusinessDate));
            OnPropertyChanged(nameof(ShowStartForm));
            StartBusinessDateCommand?.RaiseCanExecuteChanged();
            RefreshCommand?.RaiseCanExecuteChanged();
        }
    }
    public bool CanStartBusinessDate => !IsBusy && HasManagerPermission && BusinessDate?.CanStartNewDate == true;
    public bool ShowStartForm => HasManagerPermission && BusinessDate?.CanStartNewDate == true;
    public bool HasManagerPermission => _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public string BusinessDateSummary => BusinessDate is null ? "Loading business date…" :
        $"{BusinessDate.Date:yyyy-MM-dd} · {BusinessDate.Status}";
    public string VaultSummary => VaultSession is null
        ? "No vault session is open for this business date."
        : $"Vault session #{VaultSession.Id} · starting balance {VaultSession.OpeningCash:N2} · expected balance {VaultSession.ExpectedClosingCash:N2}";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsBusy = true;
            BusinessDate = await _businessDates.GetCurrentBusinessDateAsync(cancellationToken);
            VaultSession = null;
            if (BusinessDate.Status == "Open")
            {
                var sessions = await _cashOperations.GetSessionsAsync(BusinessDate.Date, cancellationToken);
                VaultSession = sessions.FirstOrDefault(session => session.PositionType == "Vault");
            }
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            OnPropertyChanged(nameof(BusinessDateSummary));
            OnPropertyChanged(nameof(VaultSummary));
            OnPropertyChanged(nameof(CanStartBusinessDate));
            OnPropertyChanged(nameof(ShowStartForm));
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task StartBusinessDateAsync()
    {
        if (!CanStartBusinessDate) return;
        if (!decimal.TryParse(StartingCashText, out var startingCash) || startingCash < 0m ||
            decimal.Round(startingCash, 2) != startingCash)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(StartingCashText)
                ? "Starting vault cash is required."
                : "Enter a non-negative amount with no more than 2 decimal places.";
            return;
        }

        try
        {
            IsBusy = true;
            BusinessDate = await _businessDates.StartCurrentBusinessDateAsync(
                new StartBusinessDateRequest(startingCash), CancellationToken.None);
            OnPropertyChanged(nameof(BusinessDateSummary));
            OnPropertyChanged(nameof(CanStartBusinessDate));
            OnPropertyChanged(nameof(ShowStartForm));
            var sessions = await _cashOperations.GetSessionsAsync(BusinessDate.Date, CancellationToken.None);
            VaultSession = sessions.FirstOrDefault(session => session.PositionType == "Vault");
            SuccessMessage = $"Business date {BusinessDate.Date:yyyy-MM-dd} opened with a vault session starting at {startingCash:N2}.";
            ErrorMessage = string.Empty;
            StartingCashText = string.Empty;
            OnPropertyChanged(nameof(BusinessDateSummary));
            OnPropertyChanged(nameof(VaultSummary));
            OnPropertyChanged(nameof(CanStartBusinessDate));
            OnPropertyChanged(nameof(ShowStartForm));
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }
}
