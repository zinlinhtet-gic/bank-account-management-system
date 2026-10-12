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
    private DateTime? _selectedBusinessDate;
    private string _forceCloseReason = string.Empty;

    public EndOfDayViewModel(IEndOfDayClientService service, AuthContext authContext,
        ICashOperationsClientService cashOperations, IReconciliationService reconciliationService,
        OfficerCashSessionContext officerCashSessionContext)
    {
        _service = service;
        _authContext = authContext;
        CashReconciliation = new CashReconciliationViewModel(cashOperations, service, authContext, officerCashSessionContext);
        AccountReconciliation = new AccountReconciliationViewModel(reconciliationService, service, authContext);
        LedgerReconciliation = new LedgerReconciliationViewModel();
        PreCloseChecks = new PreCloseChecksViewModel();
        Summary = new EndOfDaySummaryViewModel();
        ExceptionCenter = new ExceptionCenterViewModel(reconciliationService, service, authContext);
        FinalReview = new FinalReviewViewModel();
        CashHandoffs = new CashHandoffCenterViewModel(cashOperations, authContext);
        RunPreCloseCommand = new AsyncRelayCommand(RunPreCloseAsync, () => CanRunPreClose);
        ReviewPreCloseCommand = new AsyncRelayCommand(ReviewPreCloseAsync, () => CanReviewPreClose);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => CanApprove);
        CloseCommand = new AsyncRelayCommand(CloseAsync, () => CanClose);
        RefreshCommand = new AsyncRelayCommand(() => InitializeAsync(CancellationToken.None));
        LoadBusinessDateCommand = new AsyncRelayCommand(LoadSelectedBusinessDateAsync, () => !IsBusy && SelectedBusinessDate.HasValue);
        RequestForceCloseCommand = new AsyncRelayCommand(RequestForceCloseAsync, () => CanRequestForceClose);
        ApproveForceCloseCommand = new AsyncRelayCommand(ApproveForceCloseAsync, () => CanApproveForceClose);
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
    public AsyncRelayCommand ReviewPreCloseCommand { get; }
    public AsyncRelayCommand ApproveCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand LoadBusinessDateCommand { get; }
    public AsyncRelayCommand RequestForceCloseCommand { get; }
    public AsyncRelayCommand ApproveForceCloseCommand { get; }
    public string ForceCloseReason { get => _forceCloseReason; set { if (SetProperty(ref _forceCloseReason, value)) RequestForceCloseCommand.RaiseCanExecuteChanged(); } }
    public DateTime? SelectedBusinessDate
    {
        get => _selectedBusinessDate;
        set { if (SetProperty(ref _selectedBusinessDate, value)) LoadBusinessDateCommand.RaiseCanExecuteChanged(); }
    }
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
                OnPropertyChanged(nameof(CanReviewPreClose));
                OnPropertyChanged(nameof(CanRequestForceClose));
                OnPropertyChanged(nameof(CanOverrideOpen));
                OnPropertyChanged(nameof(CanApproveForceClose));
                RunPreCloseCommand.RaiseCanExecuteChanged();
                ReviewPreCloseCommand.RaiseCanExecuteChanged();
                ApproveCommand.RaiseCanExecuteChanged();
                CloseCommand.RaiseCanExecuteChanged();
                LoadBusinessDateCommand.RaiseCanExecuteChanged();
                RequestForceCloseCommand.RaiseCanExecuteChanged();
                ApproveForceCloseCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public int SelectedStageIndex { get => _selectedStageIndex; set => SetProperty(ref _selectedStageIndex, value); }
    public bool CanRunPreClosePermission => _authContext.HasPermission(PermissionCodes.Accounting) || _authContext.HasPermission(PermissionCodes.Audit);
    public bool HasEndOfDayApprovalPermission => _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool CanReviewPreClose => !IsBusy && Summary.BusinessDate?.Status == "Open" &&
        FinalReview.Run?.Status == "ReadyForApproval" &&
        HasEndOfDayApprovalPermission;
    public bool CanViewCashSessions => CashReconciliation.CanViewCashSessions;
    public bool CanRunPreClose => !IsBusy && Summary.BusinessDate?.Status == "Open" &&
        FinalReview.Run?.Status is not ("OverridePending" or "OverrideApproved") && CanRunPreClosePermission;
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
    public bool CanApprove => !IsBusy && FinalReview.Run?.Status == "ReadyForApproval" &&
        FinalReview.Run.ReviewedBy == _authContext.UserId && HasEndOfDayApprovalPermission;
    public bool CanClose => !IsBusy && (FinalReview.Run?.Status is "Approved" or "OverrideApproved") && _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool CanRequestForceClose => !IsBusy && Summary.BusinessDate?.Status == "Open" &&
        FinalReview.Run?.Status is not ("OverridePending" or "OverrideApproved") &&
        ForceCloseReason.Trim().Length is >= 5 and <= 1000 && _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool CanOverrideOpen => !IsBusy && Summary.BusinessDate?.Status == "Open" &&
        FinalReview.Run?.Status is not ("OverridePending" or "OverrideApproved") &&
        _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool HasOverridePending => FinalReview.Run?.Status == "OverridePending";
    public bool HasManagerOverride => FinalReview.Run?.OverrideReason is not null;
    public bool CanApproveForceClose => !IsBusy && HasOverridePending && _authContext.UserId.HasValue &&
        FinalReview.Run!.OverrideRequiredUserIds.Contains(_authContext.UserId.Value) &&
        !FinalReview.Run.OverrideApprovedUserIds.Contains(_authContext.UserId.Value) &&
        _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public string OverrideApprovalStatus => FinalReview.Run is { OverrideReason: not null } run
        ? $"Manager override approvals: {run.OverrideApprovedUserIds.Count} / {run.OverrideRequiredUserIds.Count}. " +
          $"Approved user IDs: {string.Join(", ", run.OverrideApprovedUserIds.Select(id => run.OverrideApprovalTimesUtc.TryGetValue(id, out var at) ? $"#{id} at {at:yyyy-MM-dd HH:mm} UTC" : $"#{id}"))}. " +
          $"Reason: {run.OverrideReason}"
        : string.Empty;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            Summary.BusinessDate = await _service.GetCurrentBusinessDateAsync(cancellationToken);
            SelectedBusinessDate = Summary.BusinessDate.Date.ToDateTime(TimeOnly.MinValue);
            await LoadSupportingViewsAsync(Summary.BusinessDate.Date, cancellationToken);
            var latestRun = await _service.GetLatestRunAsync(Summary.BusinessDate.Date, cancellationToken);
            if (latestRun is not null) ApplyRun(latestRun);
            OnPropertyChanged(nameof(BusinessDateStatus));
            OnPropertyChanged(nameof(CanRunPreClose));
            RunPreCloseCommand.RaiseCanExecuteChanged();
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
            // The current-date lookup can be blocked by an unclosed prior date. The date search remains available.
            try
            {
                var dates = await _service.SearchBusinessDatesAsync(null, null, cancellationToken);
                var openDate = dates.Where(item => item.Status == "Open").OrderBy(item => item.Date).FirstOrDefault();
                if (openDate is not null)
                {
                    SelectedBusinessDate = openDate.Date.ToDateTime(TimeOnly.MinValue);
                    await LoadSelectedBusinessDateAsync();
                }
            }
            catch (AppException) { }
        }
    }

    private async Task LoadSelectedBusinessDateAsync()
    {
        if (SelectedBusinessDate is null) return;
        try
        {
            IsBusy = true;
            var date = DateOnly.FromDateTime(SelectedBusinessDate.Value);
            var dates = await _service.SearchBusinessDatesAsync(date, date, CancellationToken.None);
            var selected = dates.FirstOrDefault(item => item.Date == date);
            if (selected is null) { ErrorMessage = "No business-date record exists for the selected date."; return; }
            Summary.BusinessDate = selected;
            var latestRun = await _service.GetLatestRunAsync(date, CancellationToken.None);
            if (latestRun is not null) ApplyRun(latestRun);
            else ClearRun();
            await LoadSupportingViewsAsync(date, CancellationToken.None);
            ErrorMessage = string.Empty;
            SuccessMessage = selected.Status == "Open"
                ? $"Loaded open business date {date:yyyy-MM-dd}. Run pre-close checks to continue."
                : $"Loaded closed business date {date:yyyy-MM-dd} for review.";
            OnPropertyChanged(nameof(BusinessDateStatus));
            OnPropertyChanged(nameof(CanRunPreClose));
            RunPreCloseCommand.RaiseCanExecuteChanged();
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task LoadSupportingViewsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await AccountReconciliation.Results.InitializeForBusinessDateAsync(date, cancellationToken);
        if (CashReconciliation.CanViewCashSessions)
            await CashReconciliation.InitializeForBusinessDateAsync(date, cancellationToken);
        await CashHandoffs.LoadForBusinessDateAsync(date, cancellationToken);
        await ExceptionCenter.Investigation.InitializeForBusinessDateAsync(date, cancellationToken);
    }

    private async Task RunPreCloseAsync()
    {
        if (Summary.BusinessDate is null) return;
        try
        {
            IsBusy = true;
            var run = await _service.RunPreCloseAsync(Summary.BusinessDate.Date, CancellationToken.None);
            ApplyRun(run);
            SuccessMessage = run.Status == "ReadyForApproval"
                ? "Checks are complete. The close request is ready for manager review."
                : "Checks are complete. Resolve the listed blockers and run the checks again.";
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task ReviewPreCloseAsync()
    {
        if (Summary.BusinessDate is null || !CanReviewPreClose) return;
        try
        {
            IsBusy = true;
            var run = await _service.ReviewPreCloseAsync(Summary.BusinessDate.Date, CancellationToken.None);
            ApplyRun(run);
            SuccessMessage = run.Status == "ReadyForApproval"
                ? "Manager checks are complete. You can now approve and close this business date."
                : "Manager checks found blockers. Resolve them and ask the auditor to submit a new close request.";
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task RequestForceCloseAsync()
    {
        if (Summary.BusinessDate is null || !CanRequestForceClose) return;
        try
        {
            IsBusy = true;
            var run = await _service.RequestForceCloseAsync(Summary.BusinessDate.Date,
                new ForceCloseRequest(ForceCloseReason.Trim()), CancellationToken.None);
            ForceCloseReason = string.Empty;
            ApplyRun(run);
            SuccessMessage = run.Status == "Closed"
                ? "All eligible managers approved the override and the business date is closed."
                : run.Status == "OverrideApproved"
                    ? "All eligible managers have approved the override. Close the business date to finish."
                    : $"Override requested. {run.OverrideApprovedUserIds.Count} of {run.OverrideRequiredUserIds.Count} manager approvals recorded.";
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task ApproveForceCloseAsync()
    {
        if (FinalReview.Run is null) return;
        try
        {
            IsBusy = true;
            ApplyRun(await _service.ApproveForceCloseAsync(FinalReview.Run.RunId, CancellationToken.None));
            SuccessMessage = FinalReview.Run.Status == "OverrideApproved"
                ? "All eligible managers have approved the override. Close the business date to finish."
                : $"Manager approval recorded: {FinalReview.Run.OverrideApprovedUserIds.Count} of {FinalReview.Run.OverrideRequiredUserIds.Count}.";
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
            var nextDate = closedDate?.AddDays(1);
            IReadOnlyList<BusinessDateResponse> nextRecords = nextDate.HasValue
                ? await _service.SearchBusinessDatesAsync(nextDate, nextDate, CancellationToken.None)
                : Array.Empty<BusinessDateResponse>();
            BusinessDateResponse? nextBusinessDate = nextRecords.FirstOrDefault();
            if (nextBusinessDate is null && closedDate.HasValue)
            {
                IReadOnlyList<BusinessDateResponse> closedRecords = await _service.SearchBusinessDatesAsync(
                    closedDate, closedDate, CancellationToken.None);
                nextBusinessDate = closedRecords.FirstOrDefault();
            }
            Summary.BusinessDate = nextBusinessDate ?? await _service.GetCurrentBusinessDateAsync(CancellationToken.None);
            SelectedBusinessDate = Summary.BusinessDate.Date.ToDateTime(TimeOnly.MinValue);
            SuccessMessage = closedDate.HasValue
                ? Summary.BusinessDate.Status == "Closed"
                    ? $"Business date {closedDate.Value:yyyy-MM-dd} closed successfully. No future business date was opened."
                    : $"Business date {closedDate.Value:yyyy-MM-dd} closed successfully. Business date {Summary.BusinessDate.Date:yyyy-MM-dd} is now selected."
                : $"Business date closed successfully. Business date {Summary.BusinessDate.Date:yyyy-MM-dd} is now selected.";
            OnPropertyChanged(nameof(BusinessDateStatus));
            OnPropertyChanged(nameof(CanRunPreClose));
            RunPreCloseCommand.RaiseCanExecuteChanged();
            await LoadSupportingViewsAsync(Summary.BusinessDate.Date, CancellationToken.None);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private void ApplyRun(EndOfDayRunResponse run)
    {
        FinalReview.Run = run;
        PreCloseChecks.Update(run.Stages);
        RunPreCloseCommand.RaiseCanExecuteChanged();
        ReviewPreCloseCommand.RaiseCanExecuteChanged();
        var accountStage = run.Stages.FirstOrDefault(stage => stage.Name == EndOfDayStageNames.AccountReconciliation);
        AccountReconciliation.Status = accountStage?.Status ?? "Locked";
        var ledgerStage = run.Stages.FirstOrDefault(stage => stage.Name == EndOfDayStageNames.LedgerReconciliation);
        LedgerReconciliation.Status = ledgerStage?.Status ?? "Locked";
        ExceptionCenter.UnresolvedCount = run.Stages.Sum(stage => stage.IssueCount);
        _ = ExceptionCenter.Investigation.InitializeForBusinessDateAsync(run.BusinessDate, CancellationToken.None);
        ApproveCommand.RaiseCanExecuteChanged();
        CloseCommand.RaiseCanExecuteChanged();
        RequestForceCloseCommand.RaiseCanExecuteChanged();
        ApproveForceCloseCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanClose));
        OnPropertyChanged(nameof(CanRunPreClose));
        OnPropertyChanged(nameof(CanReviewPreClose));
        OnPropertyChanged(nameof(CanApproveForceClose));
        OnPropertyChanged(nameof(CanRequestForceClose));
        OnPropertyChanged(nameof(CanOverrideOpen));
        OnPropertyChanged(nameof(HasOverridePending));
        OnPropertyChanged(nameof(HasManagerOverride));
        OnPropertyChanged(nameof(OverrideApprovalStatus));
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

    private void ClearRun()
    {
        FinalReview.Run = null;
        PreCloseChecks.Update([]);
        AccountReconciliation.Status = "Locked";
        LedgerReconciliation.Status = "Locked";
        ExceptionCenter.UnresolvedCount = 0;
        RunPreCloseCommand.RaiseCanExecuteChanged();
        ReviewPreCloseCommand.RaiseCanExecuteChanged();
        ApproveCommand.RaiseCanExecuteChanged();
        CloseCommand.RaiseCanExecuteChanged();
        RequestForceCloseCommand.RaiseCanExecuteChanged();
        ApproveForceCloseCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CurrentRunStatus));
        OnPropertyChanged(nameof(CanRunPreClose));
        OnPropertyChanged(nameof(CanReviewPreClose));
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanClose));
        OnPropertyChanged(nameof(CanRequestForceClose));
        OnPropertyChanged(nameof(CanOverrideOpen));
        OnPropertyChanged(nameof(CanApproveForceClose));
        OnPropertyChanged(nameof(HasOverridePending));
        OnPropertyChanged(nameof(HasManagerOverride));
        OnPropertyChanged(nameof(OverrideApprovalStatus));
        OnPropertyChanged(nameof(HasStages));
        OnPropertyChanged(nameof(HasNoStages));
        OnPropertyChanged(nameof(TotalIssueCount));
    }

    private string GetStageStatus(string name) =>
        PreCloseChecks.Stages.FirstOrDefault(stage => stage.Name == name)?.Status ?? "Not run";

    private int GetStageIssues(string name) =>
        PreCloseChecks.Stages.FirstOrDefault(stage => stage.Name == name)?.IssueCount ?? 0;
}
