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

    private int _step = 1;
    private readonly string _openingKey = Guid.NewGuid().ToString("N");
    public SessionScheduleEditorViewModel Editor { get; } = new();
    public int Step { get => _step; private set { if (SetProperty(ref _step, value)) { OnPropertyChanged(nameof(IsStepOne)); OnPropertyChanged(nameof(IsStepTwo)); OnPropertyChanged(nameof(IsStepThree)); OnPropertyChanged(nameof(ReviewSummary)); NextCommand?.RaiseCanExecuteChanged(); PreviousCommand?.RaiseCanExecuteChanged(); } } }
    public bool IsStepOne => Step == 1;
    public bool IsStepTwo => Step == 2;
    public bool IsStepThree => Step == 3;
    public string TodayLabel => (BusinessDate?.Today ?? BusinessDate?.Date)?.ToString("yyyy-MM-dd") ?? "Loading";
    public string ReviewSummary => $"Date: {TodayLabel}   |   Vault cash: {StartingCashText}   |   Officer sessions: {Editor.Rows.Count}";
    public RelayCommand NextCommand { get; }
    public RelayCommand PreviousCommand { get; }
    // Checks required values before advancing the opening stepper.
    private void AdvanceStep()
    {
        if (!decimal.TryParse(StartingCashText, out var cash) || cash < 0 || decimal.Round(cash, 2) != cash)
        { ErrorMessage = "Enter non-negative vault cash with at most two decimal places."; return; }
        if (Step == 2 && !Editor.TryBuild(BusinessDate!.Today ?? BusinessDate.Date, out _, out var error)) { ErrorMessage = error; return; }
        ErrorMessage = "";
        if (Step < 3) Step++;
    }

    public AsyncRelayCommand StartBusinessDateCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public BusinessDateOpeningViewModel(IEndOfDayClientService businessDates,
        ICashOperationsClientService cashOperations, AuthContext authContext)
    {
        _businessDates = businessDates;
        _cashOperations = cashOperations;
        _authContext = authContext;
        NextCommand = new(_ => AdvanceStep(), _ => !IsBusy && CanStartBusinessDate && Step < 3);
        PreviousCommand = new(_ => { if (Step > 1) Step--; }, _ => !IsBusy && Step > 1);
        StartBusinessDateCommand = new AsyncRelayCommand(StartBusinessDateAsync, () => CanStartBusinessDate);
        RefreshCommand = new AsyncRelayCommand(() => InitializeAsync(CancellationToken.None), () => !IsBusy);
    }

    public BusinessDateResponse? BusinessDate { get => _businessDate; private set => SetProperty(ref _businessDate, value); }
    public CashPositionSessionResponse? VaultSession { get => _vaultSession; private set => SetProperty(ref _vaultSession, value); }
    public string StartingCashText { get => _startingCashText; set { if (SetProperty(ref _startingCashText, value)) ErrorMessage = string.Empty; } }
    public string ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public string SuccessMessage { get => _successMessage; private set { if (SetProperty(ref _successMessage, value)) OnPropertyChanged(nameof(HasSuccess)); } }
    public bams.desktop.Utils.MessageCode? ErrorCode => HasError ? bams.desktop.Utils.MessageCode.ValidationFailed : null;
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
            NextCommand?.RaiseCanExecuteChanged();
            PreviousCommand?.RaiseCanExecuteChanged();
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
            if (HasManagerPermission) await Editor.LoadAsync(_cashOperations, cancellationToken);
            OnPropertyChanged(nameof(TodayLabel));
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

        if (!Editor.TryBuild(BusinessDate!.Today ?? BusinessDate.Date, out var schedules, out var scheduleError)) { ErrorMessage = scheduleError; return; }
        try
        {
            IsBusy = true;
            BusinessDate = await _businessDates.StartCurrentBusinessDateAsync(
                new StartBusinessDateRequest(startingCash, schedules, _openingKey, BusinessDate.Today ?? BusinessDate.Date), CancellationToken.None);
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
