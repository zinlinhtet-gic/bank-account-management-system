using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Coordinates business-date stages while child ViewModels own their individual presentation state.</summary>
public sealed class EndOfDayViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IEndOfDayClientService _service;
    private readonly AuthContext _authContext;
    private string? _errorMessage;
    private string _successMessage = string.Empty;
    private bool _isBusy;
    private int _selectedStageIndex;

    public EndOfDayViewModel(IEndOfDayClientService service, AuthContext authContext,
        ICashOperationsClientService cashOperations, IReconciliationService reconciliationService,
        OfficerCashSessionContext officerCashSessionContext)
    {
        _service = service;
        _authContext = authContext;
        CashReconciliation = new CashReconciliationViewModel(cashOperations, service, authContext, officerCashSessionContext);
        AccountReconciliation = new AccountReconciliationViewModel(reconciliationService);
        LedgerReconciliation = new LedgerReconciliationViewModel();
        PreCloseChecks = new PreCloseChecksViewModel();
        Summary = new EndOfDaySummaryViewModel();
        ExceptionCenter = new ExceptionCenterViewModel(reconciliationService);
        FinalReview = new FinalReviewViewModel();
        CashHandoffs = new CashHandoffCenterViewModel(cashOperations, authContext);
        RunPreCloseCommand = new AsyncRelayCommand(RunPreCloseAsync, () => CanRunPreClose);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => CanApprove);
        CloseCommand = new AsyncRelayCommand(CloseAsync, () => CanClose);
        RefreshCommand = new AsyncRelayCommand(() => InitializeAsync(CancellationToken.None));
        NavigateToStageCommand = new RelayCommand(parameter =>
        {
            var index = -1;
            if (parameter is int numericIndex)
                index = numericIndex;
            else if (parameter is string stageText)
                int.TryParse(stageText, out index);

            if (index >= 0 && index <= 6)
                SelectedStageIndex = index;
        });
    }

    public EndOfDaySummaryViewModel Summary { get; }
    public PreCloseChecksViewModel PreCloseChecks { get; }
    public CashReconciliationViewModel CashReconciliation { get; }
    public LedgerReconciliationViewModel LedgerReconciliation { get; }
    public AccountReconciliationViewModel AccountReconciliation { get; }
    public ExceptionCenterViewModel ExceptionCenter { get; }
    public FinalReviewViewModel FinalReview { get; }
    public CashHandoffCenterViewModel CashHandoffs { get; }
    public AsyncRelayCommand RunPreCloseCommand { get; }
    public AsyncRelayCommand ApproveCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand NavigateToStageCommand { get; }
    public string? ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string SuccessMessage { get => _successMessage; private set { if (SetProperty(ref _successMessage, value)) OnPropertyChanged(nameof(HasSuccess)); } }
    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanRunPreClose));
                RunPreCloseCommand.RaiseCanExecuteChanged();
                ApproveCommand.RaiseCanExecuteChanged();
                CloseCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public int SelectedStageIndex { get => _selectedStageIndex; set => SetProperty(ref _selectedStageIndex, value); }
    public bool CanRunPreClosePermission => _authContext.HasPermission(PermissionCodes.Accounting) || _authContext.HasPermission(PermissionCodes.Audit);
    public bool HasEndOfDayApprovalPermission => _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool CanRunPreClose => !IsBusy && Summary.BusinessDate?.Status == "Open" && CanRunPreClosePermission;
    public string BusinessDateStatus => Summary.BusinessDate?.Status ?? "Loading";
    public string CurrentRunStatus => FinalReview.Run?.Status ?? "Not started";
    public int TotalIssueCount => PreCloseChecks.Stages.Sum(stage => stage.IssueCount);
    public bool HasStages => PreCloseChecks.Stages.Count > 0;
    public bool HasNoStages => !HasStages;
    public string TransactionStatus => GetStageStatus(EndOfDayStageNames.Transactions);
    public int TransactionIssues => GetStageIssues(EndOfDayStageNames.Transactions);
    public string CashStatus => GetStageStatus(EndOfDayStageNames.TellerVaultCash);
    public int CashIssues => GetStageIssues(EndOfDayStageNames.TellerVaultCash);
    public string LedgerStatus => GetStageStatus(EndOfDayStageNames.LedgerReconciliation);
    public string AccountStatus => GetStageStatus(EndOfDayStageNames.AccountReconciliation);
    public string CriticalExceptionStatus => GetStageStatus(EndOfDayStageNames.CriticalExceptions);
    public int CriticalExceptionCount => GetStageIssues(EndOfDayStageNames.CriticalExceptions);
    public bool CanApprove => !IsBusy && FinalReview.Run?.Status == "ReadyForApproval" && _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool CanClose => !IsBusy && FinalReview.Run?.Status == "Approved" && _authContext.HasPermission(PermissionCodes.EndOfDayApproval);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            Summary.BusinessDate = await _service.GetCurrentBusinessDateAsync(cancellationToken);
            OnPropertyChanged(nameof(BusinessDateStatus));
            OnPropertyChanged(nameof(CanRunPreClose));
            RunPreCloseCommand.RaiseCanExecuteChanged();
            if (CashReconciliation.HasCashOperationsPermission)
                await CashReconciliation.InitializeForBusinessDateAsync(Summary.BusinessDate.Date, cancellationToken);
            await CashHandoffs.LoadForBusinessDateAsync(Summary.BusinessDate.Date, cancellationToken);
            await ExceptionCenter.Investigation.InitializeAsync(cancellationToken);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }

    private async Task RunPreCloseAsync()
    {
        if (Summary.BusinessDate is null) return;
        try
        {
            IsBusy = true;
            var run = await _service.RunPreCloseAsync(Summary.BusinessDate.Date, CancellationToken.None);
            ApplyRun(run);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task ApproveAsync()
    {
        if (FinalReview.Run is null) return;
        try { IsBusy = true; ApplyRun(await _service.ApproveAsync(FinalReview.Run.RunId, CancellationToken.None)); }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task CloseAsync()
    {
        if (FinalReview.Run is null) return;
        try
        {
            IsBusy = true;
            SuccessMessage = string.Empty;
            var closedDate = Summary.BusinessDate?.Date;
            ApplyRun(await _service.CloseAsync(FinalReview.Run.RunId, CancellationToken.None));
            Summary.BusinessDate = await _service.GetCurrentBusinessDateAsync(CancellationToken.None);
            SuccessMessage = closedDate.HasValue
                ? $"Business date {closedDate.Value:yyyy-MM-dd} closed successfully. Business date {Summary.BusinessDate.Date:yyyy-MM-dd} is now open."
                : $"Business date closed successfully. Business date {Summary.BusinessDate.Date:yyyy-MM-dd} is now open.";
            OnPropertyChanged(nameof(BusinessDateStatus));
            OnPropertyChanged(nameof(CanRunPreClose));
            RunPreCloseCommand.RaiseCanExecuteChanged();
            if (CashReconciliation.HasCashOperationsPermission)
                await CashReconciliation.InitializeForBusinessDateAsync(Summary.BusinessDate.Date, CancellationToken.None);
            await CashHandoffs.LoadForBusinessDateAsync(Summary.BusinessDate.Date, CancellationToken.None);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private void ApplyRun(EndOfDayRunResponse run)
    {
        FinalReview.Run = run;
        PreCloseChecks.Update(run.Stages);
        var accountStage = run.Stages.FirstOrDefault(stage => stage.Name == EndOfDayStageNames.AccountReconciliation);
        AccountReconciliation.Status = accountStage?.Status ?? "Locked";
        var ledgerStage = run.Stages.FirstOrDefault(stage => stage.Name == EndOfDayStageNames.LedgerReconciliation);
        LedgerReconciliation.Status = ledgerStage?.Status ?? "Locked";
        ExceptionCenter.UnresolvedCount = run.Stages.Sum(stage => stage.IssueCount);
        _ = ExceptionCenter.Investigation.InitializeAsync(CancellationToken.None);
        ApproveCommand.RaiseCanExecuteChanged();
        CloseCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanClose));
        OnPropertyChanged(nameof(CurrentRunStatus));
        OnPropertyChanged(nameof(HasStages));
        OnPropertyChanged(nameof(HasNoStages));
        OnPropertyChanged(nameof(TotalIssueCount));
        OnPropertyChanged(nameof(TransactionStatus));
        OnPropertyChanged(nameof(TransactionIssues));
        OnPropertyChanged(nameof(CashStatus));
        OnPropertyChanged(nameof(CashIssues));
        OnPropertyChanged(nameof(LedgerStatus));
        OnPropertyChanged(nameof(AccountStatus));
        OnPropertyChanged(nameof(CriticalExceptionStatus));
        OnPropertyChanged(nameof(CriticalExceptionCount));
        ErrorMessage = string.Empty;
    }

    private string GetStageStatus(string name) =>
        PreCloseChecks.Stages.FirstOrDefault(stage => stage.Name == name)?.Status ?? "Not run";

    private int GetStageIssues(string name) =>
        PreCloseChecks.Stages.FirstOrDefault(stage => stage.Name == name)?.IssueCount ?? 0;
}
