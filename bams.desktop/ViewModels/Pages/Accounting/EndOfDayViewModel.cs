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
    private readonly ICashOperationsClientService _cashOperations;
    private CashPositionSessionResponse? _vault;
    private CashCountHistoryResponse? _vaultCount;
    private EndOfDayRunResponse? _forceCloseRun;
    private EndOfDayAuditReport? _auditReport;
    public EndOfDayAuditReport? AuditReport { get => _auditReport; private set => SetProperty(ref _auditReport, value); }
    public AsyncRelayCommand PollCommand { get; }
    public AsyncRelayCommand AuditCommand { get; }
    private string? _errorMessage;
    private string _successMessage = string.Empty;
    private bool _isBusy;
    private int _selectedStageIndex;
    private DateTime? _selectedBusinessDate;
    private string _forceCloseReason = string.Empty;
    private readonly UnifiedReconciliationClient _unifiedClient;
    private string _vaultActual = "", _vaultNote = "";
    public UnifiedReconciliationViewModel Reconciliation { get; }
    public event Action<ReconciliationRoute>? NavigateRequested;
    public string VaultActual { get => _vaultActual; set => SetProperty(ref _vaultActual, value); }
    public string VaultNote { get => _vaultNote; set => SetProperty(ref _vaultNote, value); }
    public AsyncRelayCommand FinalVaultCountCommand { get; }

    public EndOfDayViewModel(IEndOfDayClientService service, AuthContext authContext,
        ICashOperationsClientService cashOperations, IReconciliationService reconciliationService,
        OfficerCashSessionContext officerCashSessionContext, IDialogService dialogService,
        UnifiedReconciliationViewModel reconciliation, UnifiedReconciliationClient unifiedClient)
    {
        _service = service;
        _cashOperations = cashOperations;
        _authContext = authContext;
        _unifiedClient = unifiedClient;
        PollCommand = new(PollAsync, () => !IsBusy);
        Reconciliation = reconciliation;
        Reconciliation.IsEmbedded = true;
        Reconciliation.NavigateRequested += route => NavigateRequested?.Invoke(route);
        FinalVaultCountCommand = new AsyncRelayCommand(FinalVaultCountAsync, () => CanRecordVaultCount);
        CashReconciliation = new CashReconciliationViewModel(cashOperations, service, authContext,
            officerCashSessionContext, dialogService);
        AccountReconciliation = new AccountReconciliationViewModel(reconciliationService, service, authContext);
        LedgerReconciliation = new LedgerReconciliationViewModel();
        PreCloseChecks = new PreCloseChecksViewModel();
        Summary = new EndOfDaySummaryViewModel();
        AuditCommand = new(LoadAuditAsync, () => !IsBusy && Summary.BusinessDate is not null);
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
        PreviousStepCommand = new RelayCommand(_ => { if (SelectedStageIndex > 0) SelectedStageIndex--; }, _ => !IsBusy && SelectedStageIndex > 0);
        NextStepCommand = new RelayCommand(_ => { if (SelectedStageIndex < 4) SelectedStageIndex++; }, _ => !IsBusy && SelectedStageIndex < 4);
        NavigateToStageCommand = new RelayCommand(parameter =>
        {
            int index = -1;
            if (parameter is int numericIndex)
                index = numericIndex;
            else if (parameter is string stageText)
                int.TryParse(stageText, out index);

            if (index >= 0 && index <= 4)
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
                FinalVaultCountCommand.RaiseCanExecuteChanged();
                PreviousStepCommand.RaiseCanExecuteChanged();
                NextStepCommand.RaiseCanExecuteChanged();
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
    public int SelectedStageIndex { get => _selectedStageIndex; set { if (SetProperty(ref _selectedStageIndex, value)) { foreach (string name in new[] { nameof(IsStep1), nameof(IsStep2), nameof(IsStep3), nameof(IsStep4), nameof(IsStep5), nameof(StepTitle) }) OnPropertyChanged(name); PreviousStepCommand.RaiseCanExecuteChanged(); NextStepCommand.RaiseCanExecuteChanged(); } } }
    public RelayCommand PreviousStepCommand { get; }
    public RelayCommand NextStepCommand { get; }
    public bool IsStep1 => SelectedStageIndex == 0;
    public bool IsStep2 => SelectedStageIndex == 1;
    public bool IsStep3 => SelectedStageIndex == 2;
    public bool IsStep4 => SelectedStageIndex == 3;
    public bool IsStep5 => SelectedStageIndex == 4;
    public string StepTitle => SelectedStageIndex switch { 0 => "1. Run reconciliation checks", 1 => "2. Validate final vault cash", 2 => "3. Review closing readiness", 3 => "4. Manager review and approval", _ => "5. Close business date" };
    public bool CanRecordVaultCount => !IsBusy && HasEndOfDayApprovalPermission && Summary.BusinessDate?.Status == "Open" && _vault?.Status == "Open" && _vaultCount is null;
    public string VaultStatus => _vault is null ? "No vault session exists for this date." : $"Vault session #{_vault.Id}: {_vault.Status}";
    public decimal? VaultExpected => _vault?.ExpectedClosingCash;
    public decimal? VaultCounted => _vaultCount?.ActualAmount;
    public decimal? VaultDifference => _vaultCount is null || _vault is null ? null : _vaultCount.ActualAmount - _vault.ExpectedClosingCash;
    public bool IsDateClosed => Summary.BusinessDate?.Status == "Closed";
    private void ResumeStep()
    {
        SelectedStageIndex = IsDateClosed || FinalReview.Run?.Status is "Approved" or "OverrideApproved" ? 4 :
            FinalReview.Run?.Status is "ReadyForApproval" or "OverridePending" ? 3 :
            _vault?.Status == "Closed" && _vaultCount is not null ? 2 : FinalReview.Run is not null ? 1 : 0;
        OnPropertyChanged(nameof(IsDateClosed));
    }
    public bool CanRunPreClosePermission => _authContext.HasPermission(PermissionCodes.Accounting) || _authContext.HasPermission(PermissionCodes.Audit) || HasEndOfDayApprovalPermission;
    public bool HasEndOfDayApprovalPermission => _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool CanReviewPreClose => !IsBusy && Summary.BusinessDate?.Status == "Open" &&
        FinalReview.Run?.Status == "ReadyForApproval" &&
        HasEndOfDayApprovalPermission;
    public bool CanViewCashSessions => CashReconciliation.CanViewCashSessions;
    public bool CanRunPreClose => !IsBusy && Summary.BusinessDate?.Status == "Open" &&
        FinalReview.Run?.Status != "OverrideApproved" && CanRunPreClosePermission;
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
        _forceCloseRun?.Status is not ("OverridePending" or "OverrideApproved") &&
        ForceCloseReason.Trim().Length is >= 5 and <= 1000 && _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool CanOverrideOpen => !IsBusy && Summary.BusinessDate?.Status == "Open" &&
        _forceCloseRun?.Status is not ("OverridePending" or "OverrideApproved") &&
        _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool HasOverridePending => _forceCloseRun?.Status == "OverridePending";
    public bool HasManagerOverride => _forceCloseRun?.OverrideReason is not null;
    public bool CanApproveForceClose => !IsBusy && HasOverridePending && _authContext.UserId.HasValue &&
        _forceCloseRun!.OverrideRequiredUserIds.Contains(_authContext.UserId.Value) &&
        !_forceCloseRun.OverrideApprovedUserIds.Contains(_authContext.UserId.Value) &&
        _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public string OverrideApprovalStatus => _forceCloseRun is { OverrideReason: not null } run
        ? $"Manager override approvals: {run.OverrideApprovedUserIds.Count} / {run.OverrideRequiredUserIds.Count}. " +
          $"Approved user IDs: {string.Join(", ", run.OverrideApprovedUserIds.Select(id => run.OverrideApprovalTimesUtc.TryGetValue(id, out DateTime at) ? $"#{id} at {at:yyyy-MM-dd HH:mm} UTC" : $"#{id}"))}. " +
          $"Reason: {run.OverrideReason}"
        : string.Empty;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            Summary.BusinessDate = await _service.GetCurrentBusinessDateAsync(cancellationToken);
            SelectedBusinessDate = Summary.BusinessDate.Date.ToDateTime(TimeOnly.MinValue);
            await LoadSupportingViewsAsync(Summary.BusinessDate.Date, cancellationToken);
            EndOfDayRunResponse? latestRun = await _service.GetLatestRunAsync(Summary.BusinessDate.Date, cancellationToken);
            _forceCloseRun = null;
            if (latestRun is not null) ApplyRun(latestRun); else ClearRun();
            ResumeStep();
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
                IReadOnlyList<BusinessDateResponse> dates = await _service.SearchBusinessDatesAsync(null, null, cancellationToken);
                BusinessDateResponse? openDate = dates.Where(item => item.Status == "Open").OrderBy(item => item.Date).FirstOrDefault();
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
            DateOnly date = DateOnly.FromDateTime(SelectedBusinessDate.Value);
            IReadOnlyList<BusinessDateResponse> dates = await _service.SearchBusinessDatesAsync(date, date, CancellationToken.None);
            BusinessDateResponse? selected = dates.FirstOrDefault(item => item.Date == date);
            if (selected is null) { ErrorMessage = "No business-date record exists for the selected date."; return; }
            Summary.BusinessDate = selected;
            _forceCloseRun = null;
            EndOfDayRunResponse? latestRun = await _service.GetLatestRunAsync(date, CancellationToken.None);
            if (latestRun is not null) ApplyRun(latestRun);
            else ClearRun();
            await LoadSupportingViewsAsync(date, CancellationToken.None);
            ErrorMessage = string.Empty;
            SuccessMessage = selected.Status == "Open"
                ? $"Loaded open business date {date:yyyy-MM-dd}. Run pre-close checks to continue."
                : $"Loaded closed business date {date:yyyy-MM-dd} for review.";
            ResumeStep();
            OnPropertyChanged(nameof(BusinessDateStatus));
            OnPropertyChanged(nameof(CanRunPreClose));
            RunPreCloseCommand.RaiseCanExecuteChanged();
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task LoadSupportingViewsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        AuditReport = await _unifiedClient.AuditAsync(date, cancellationToken);
        Reconciliation.FromDate = date.ToDateTime(TimeOnly.MinValue);
        Reconciliation.ToDate = date.ToDateTime(TimeOnly.MinValue);
        await Reconciliation.InitializeAsync(cancellationToken);
        IReadOnlyList<CashPositionSessionResponse> sessions = await _cashOperations.GetSessionsAsync(date, cancellationToken);
        _vault = sessions.SingleOrDefault(session => session.PositionType == "Vault");
        _vaultCount = null;
        if (_vault is not null)
        {
            CashPositionSessionDetailResponse detail = await _cashOperations.GetSessionDetailAsync(_vault.Id, cancellationToken);
            _vaultCount = detail.Counts.OrderByDescending(count => count.Id).FirstOrDefault();
        }
        foreach (string name in new[] { nameof(VaultStatus), nameof(VaultExpected), nameof(VaultCounted), nameof(VaultDifference), nameof(CanRecordVaultCount) }) OnPropertyChanged(name);
        FinalVaultCountCommand.RaiseCanExecuteChanged();
    }

    private async Task FinalVaultCountAsync()
    {
        if (Summary.BusinessDate is null || !decimal.TryParse(VaultActual, out decimal actual))
        { ErrorMessage = "Enter the vault's physical cash amount."; return; }
        try
        {
            IsBusy = true;
            await _unifiedClient.FinalCountAsync(Summary.BusinessDate.Date, actual, VaultNote);
            await LoadSupportingViewsAsync(Summary.BusinessDate.Date, default);
            SelectedStageIndex = 2;
            SuccessMessage = "Final vault count recorded. Rerun readiness checks; any mismatch must be resolved before normal close.";
        }
        catch (AppException error) { await RefreshStoppedWorkflowAsync(error); }
        finally { IsBusy = false; }
    }

    private async Task RunPreCloseAsync()
    {
        if (Summary.BusinessDate is null) return;
        try
        {
            IsBusy = true;
            EndOfDayRunResponse run = await _service.RunPreCloseAsync(Summary.BusinessDate.Date, CancellationToken.None);
            ApplyRun(run);
            await LoadSupportingViewsAsync(Summary.BusinessDate.Date, default);
            ResumeStep();
            SuccessMessage = run.Status == "ReadyForApproval"
                ? "Checks are complete. The close request is ready for manager review."
                : "Checks are complete. Resolve the listed blockers and run the checks again.";
        }
        catch (AppException exception) { await RefreshStoppedWorkflowAsync(exception); }
        finally { IsBusy = false; }
    }

    private async Task ReviewPreCloseAsync()
    {
        if (Summary.BusinessDate is null || !CanReviewPreClose) return;
        try
        {
            IsBusy = true;
            EndOfDayRunResponse run = await _service.ReviewPreCloseAsync(Summary.BusinessDate.Date, CancellationToken.None);
            ApplyRun(run);
            ResumeStep();
            SuccessMessage = run.Status == "ReadyForApproval"
                ? "Manager checks are complete. You can now approve and close this business date."
                : "Manager checks found blockers. Resolve them and ask the auditor to submit a new close request.";
        }
        catch (AppException exception) { await RefreshStoppedWorkflowAsync(exception); }
        finally { IsBusy = false; }
    }

    private async Task RequestForceCloseAsync()
    {
        if (Summary.BusinessDate is null || !CanRequestForceClose) return;
        try
        {
            IsBusy = true;
            EndOfDayRunResponse run = await _service.RequestForceCloseAsync(Summary.BusinessDate.Date,
                new ForceCloseRequest(ForceCloseReason.Trim()), CancellationToken.None);
            ForceCloseReason = string.Empty;
            ApplyRun(run);
            await RefreshClosedDateAsync(run);
            ResumeStep();
            SuccessMessage = run.Status == "Closed"
                ? "All eligible managers approved the override and the business date is closed."
                : run.Status == "OverrideApproved"
                    ? "All eligible managers have approved the override. Close the business date to finish."
                    : $"Override requested. {run.OverrideApprovedUserIds.Count} of {run.OverrideRequiredUserIds.Count} manager approvals recorded.";
        }
        catch (AppException exception) { await RefreshStoppedWorkflowAsync(exception); }
        finally { IsBusy = false; }
    }

    private async Task ApproveForceCloseAsync()
    {
        if (_forceCloseRun is null || !CanApproveForceClose) return;
        try
        {
            IsBusy = true;
            EndOfDayRunResponse forceResponse = await _service.ApproveForceCloseAsync(_forceCloseRun.RunId, CancellationToken.None);
            ApplyRun(forceResponse);
            if (forceResponse.Status != "Closed")
            {
                EndOfDayRunResponse? latest = await _service.GetLatestRunAsync(forceResponse.BusinessDate, CancellationToken.None);
                if (latest is not null) ApplyRun(latest);
            }
            await RefreshClosedDateAsync(forceResponse);
            ResumeStep();
            SuccessMessage = forceResponse.Status == "Closed"
                ? "All eligible managers approved. The business date is closed; unfinished normal-close operations are stopped."
                : $"Manager approval recorded: {forceResponse.OverrideApprovedUserIds.Count} of {forceResponse.OverrideRequiredUserIds.Count}.";
        }
        catch (AppException exception) { await RefreshStoppedWorkflowAsync(exception); }
        finally { IsBusy = false; }
    }

    private async Task ApproveAsync()
    {
        if (FinalReview.Run is null) return;
        try { IsBusy = true; ApplyRun(await _service.ApproveAsync(FinalReview.Run.RunId, CancellationToken.None)); ResumeStep(); SuccessMessage = "Approval recorded. Close the date to perform final validation and generate summaries."; }
        catch (AppException exception) { await RefreshStoppedWorkflowAsync(exception); }
        finally { IsBusy = false; }
    }

    private async Task CloseAsync()
    {
        if (FinalReview.Run is null) return;
        try
        {
            IsBusy = true;
            SuccessMessage = string.Empty;
            DateOnly? closedDate = Summary.BusinessDate?.Date;
            ApplyRun(await _service.CloseAsync(FinalReview.Run.RunId, CancellationToken.None));
            await RefreshClosedDateAsync(FinalReview.Run!);
            ResumeStep();
            SuccessMessage = $"Business date {closedDate:yyyy-MM-dd} closed successfully. Audit and final summaries are saved.";
        }
        catch (AppException exception) { await RefreshStoppedWorkflowAsync(exception); }
        finally { IsBusy = false; }
    }

    private async Task LoadAuditAsync()
    {
        if (Summary.BusinessDate is null || IsBusy) return;
        try { IsBusy = true; AuditReport = await _unifiedClient.AuditAsync(Summary.BusinessDate.Date); }
        catch (AppException failure) { ErrorMessage = failure.Message; }
        finally { IsBusy = false; }
    }

    // Read-only polling never reruns reconciliation or overwrites investigation/count inputs.
    private async Task PollAsync()
    {
        if (IsBusy || Summary.BusinessDate is null) return;
        try
        {
            IsBusy = true;
            DateOnly date = Summary.BusinessDate.Date;
            EndOfDayRunResponse? latest = await _service.GetLatestRunAsync(date, CancellationToken.None);
            if (latest is null) return;
            string oldState = System.Text.Json.JsonSerializer.Serialize(FinalReview.Run);
            string newState = System.Text.Json.JsonSerializer.Serialize(latest);
            if (oldState == newState) return;
            _forceCloseRun = null;
            ApplyRun(latest);
            if (latest.Status == "Closed")
            {
                await RefreshClosedDateAsync(latest);
                ResumeStep();
                SuccessMessage = "The business date was closed by another manager. Previous close operations are stopped.";
            }
        }
        catch (AppException) { /* A later poll or manual refresh can recover a temporary connectivity failure. */ }
        finally { IsBusy = false; }
    }

    // Another manager can finish either close path while this page still displays an older run.
    private async Task RefreshStoppedWorkflowAsync(AppException failure)
    {
        ErrorMessage = failure.Message;
        if (Summary.BusinessDate is null) return;
        try
        {
            EndOfDayRunResponse? latest = await _service.GetLatestRunAsync(Summary.BusinessDate.Date, CancellationToken.None);
            if (latest?.Status != "Closed") return;
            ApplyRun(latest);
            await RefreshClosedDateAsync(latest);
            ResumeStep();
            ErrorMessage = "This business date has already been closed. Its previous close operations are stopped; financial reconciliation corrections require a later active open date.";
        }
        catch (AppException) { ErrorMessage = failure.Message; }
    }

    // Keep the closed date selected so the completion screen and its run cannot refer to different dates.
    private async Task RefreshClosedDateAsync(EndOfDayRunResponse run)
    {
        if (run.Status != "Closed") return;
        IReadOnlyList<BusinessDateResponse> dates = await _service.SearchBusinessDatesAsync(run.BusinessDate, run.BusinessDate, CancellationToken.None);
        Summary.BusinessDate = dates.Single(date => date.Date == run.BusinessDate);
        await LoadSupportingViewsAsync(run.BusinessDate, CancellationToken.None);
        OnPropertyChanged(nameof(BusinessDateStatus));
        OnPropertyChanged(nameof(IsDateClosed));
    }

    private void ApplyRun(EndOfDayRunResponse run)
    {
        if (_forceCloseRun?.BusinessDate != run.BusinessDate) _forceCloseRun = null;
        if (run.Status == "Closed") _forceCloseRun = run.OverrideReason is null ? null : run;
        else if (run.OverrideReason is not null) _forceCloseRun = run;
        else if (run.PendingForceClose is not null) _forceCloseRun = run.PendingForceClose;
        FinalReview.Run = run;
        PreCloseChecks.Update(run.Stages);
        RunPreCloseCommand.RaiseCanExecuteChanged();
        ReviewPreCloseCommand.RaiseCanExecuteChanged();
        EndOfDayStageResponse? accountStage = run.Stages.FirstOrDefault(stage => stage.Name == EndOfDayStageNames.AccountReconciliation);
        AccountReconciliation.Status = accountStage?.Status ?? "Locked";
        EndOfDayStageResponse? ledgerStage = run.Stages.FirstOrDefault(stage => stage.Name == EndOfDayStageNames.LedgerReconciliation);
        LedgerReconciliation.Status = ledgerStage?.Status ?? "Locked";
        ExceptionCenter.UnresolvedCount = run.Stages.Sum(stage => stage.IssueCount);

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
        _forceCloseRun = null;
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
