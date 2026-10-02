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
    private bool _isBusy;

    public EndOfDayViewModel(IEndOfDayClientService service, AuthContext authContext,
        ICashOperationsClientService cashOperations, IReconciliationService reconciliationService)
    {
        _service = service;
        _authContext = authContext;
        CashReconciliation = new CashReconciliationViewModel(cashOperations, authContext);
        AccountReconciliation = new AccountReconciliationViewModel(reconciliationService);
        LedgerReconciliation = new LedgerReconciliationViewModel();
        PreCloseChecks = new PreCloseChecksViewModel();
        Summary = new EndOfDaySummaryViewModel();
        ExceptionCenter = new ExceptionCenterViewModel(reconciliationService);
        FinalReview = new FinalReviewViewModel();
        RunPreCloseCommand = new AsyncRelayCommand(RunPreCloseAsync);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => CanApprove);
        CloseCommand = new AsyncRelayCommand(CloseAsync, () => CanClose);
    }

    public EndOfDaySummaryViewModel Summary { get; }
    public PreCloseChecksViewModel PreCloseChecks { get; }
    public CashReconciliationViewModel CashReconciliation { get; }
    public LedgerReconciliationViewModel LedgerReconciliation { get; }
    public AccountReconciliationViewModel AccountReconciliation { get; }
    public ExceptionCenterViewModel ExceptionCenter { get; }
    public FinalReviewViewModel FinalReview { get; }
    public AsyncRelayCommand RunPreCloseCommand { get; }
    public AsyncRelayCommand ApproveCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }
    public string? ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool CanApprove => FinalReview.Run?.Status == "ReadyForApproval" && _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool CanClose => FinalReview.Run?.Status == "Approved" && _authContext.HasPermission(PermissionCodes.EndOfDayApproval);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            Summary.BusinessDate = await _service.GetCurrentBusinessDateAsync(cancellationToken);
            await CashReconciliation.InitializeAsync(cancellationToken);
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
            ApplyRun(await _service.CloseAsync(FinalReview.Run.RunId, CancellationToken.None));
            Summary.BusinessDate = await _service.GetCurrentBusinessDateAsync(CancellationToken.None);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private void ApplyRun(EndOfDayRunResponse run)
    {
        FinalReview.Run = run;
        PreCloseChecks.Update(run.Stages);
        var accountStage = run.Stages.FirstOrDefault(stage => stage.Name == "Account Reconciliation");
        AccountReconciliation.Status = accountStage?.Status ?? "Locked";
        var ledgerStage = run.Stages.FirstOrDefault(stage => stage.Name == "Ledger Reconciliation");
        LedgerReconciliation.Status = ledgerStage?.Status ?? "Locked";
        ExceptionCenter.UnresolvedCount = run.Stages.Sum(stage => stage.IssueCount);
        _ = ExceptionCenter.Investigation.InitializeAsync(CancellationToken.None);
        ApproveCommand.RaiseCanExecuteChanged();
        CloseCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanClose));
        ErrorMessage = string.Empty;
    }
}
