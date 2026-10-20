using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Explicit navigation context shared by operation pages, without relying on selected rows elsewhere.</summary>
public sealed record ReconciliationRoute(string Page, long? ExceptionId = null, long? ProposalId = null);

public abstract class ReconciliationOperationViewModel : ViewModelBase, IAsyncInitializable, INotificationFocusTarget
{
    // Record a notification asked this page to select; consumed by the page after its next load.
    protected long? FocusId { get; private set; }
    protected bool FocusIsCash { get; private set; }

    /// <inheritdoc />
    public void FocusOn(string targetType, long targetId)
    {
        FocusId = targetId;
        FocusIsCash = NotificationTargets.IsCashRecord(targetType);
    }

    /// <summary>Returns the pending focus id once and clears it so later polls keep the user's own selection.</summary>
    protected long? TakeFocusId()
    {
        var id = FocusId;
        FocusId = null;
        return id;
    }

    protected readonly ReconciliationWorkflowClient Client;
    protected readonly IReconciliationService Existing;
    protected readonly AuthContext Auth;
    private string? _error;
    private bool _busy;
    public event Action<ReconciliationRoute>? NavigateRequested;
    public long? ExceptionId { get; private set; }
    public long? ProposalId { get; private set; }
    public string? ErrorMessage { get => _error; protected set => SetProperty(ref _error, value); }
    public bool IsBusy { get => _busy; protected set => SetProperty(ref _busy, value); }
    public bool CanInvestigate => Auth.HasPermission(PermissionCodes.ReconciliationInvestigation);
    public bool CanApprove => Auth.HasPermission(PermissionCodes.TransactionCorrectionApproval);
    protected ReconciliationOperationViewModel(ReconciliationWorkflowClient client, IReconciliationService existing, AuthContext auth)
    { Client = client; Existing = existing; Auth = auth; }
    public void SetRoute(ReconciliationRoute route) { ExceptionId = route.ExceptionId; ProposalId = route.ProposalId; }
    protected void Navigate(string page, long? exceptionId = null, long? proposalId = null) => NavigateRequested?.Invoke(new(page, exceptionId, proposalId));
    protected async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy) return;
        try { IsBusy = true; ErrorMessage = null; await operation(); }
        catch (OperationCanceledException) { }
        catch (AppException error) { ErrorMessage = error.Message; }
        finally { IsBusy = false; }
    }
    public abstract Task InitializeAsync(CancellationToken ct = default);
}

/// <summary>Runs checks and exposes provisional summary revisions for the selected date.</summary>
public sealed class ReconciliationChecksViewModel : ReconciliationOperationViewModel
{
    private DateTime _from = DateTime.Today, _to = DateTime.Today;
    private ReconciliationExceptionResponse? _selected;
    public DateTime FromDate { get => _from; set => SetProperty(ref _from, value); }
    public DateTime ToDate { get => _to; set => SetProperty(ref _to, value); }
    public ReconciliationExceptionResponse? SelectedException { get => _selected; set => SetProperty(ref _selected, value); }
    public ObservableCollection<AccountReconciliationResultResponse> Results { get; } = [];
    public ObservableCollection<ReconciliationExceptionResponse> Exceptions { get; } = [];
    public ObservableCollection<SummaryRevisionResponse> Summaries { get; } = [];
    public AsyncRelayCommand CheckCommand { get; }
    public RelayCommand OpenExceptionCommand { get; }
    public ReconciliationChecksViewModel(ReconciliationWorkflowClient c, IReconciliationService e, AuthContext a) : base(c,e,a)
    {
        CheckCommand = new(() => RunAsync(async () =>
        {
            var result = await Client.CheckAsync(new(DateOnly.FromDateTime(FromDate), DateOnly.FromDateTime(ToDate), null), default);
            Results.Clear(); foreach (var row in result.Results) Results.Add(row);
            await LoadAsync(default);
        }));
        OpenExceptionCommand = new(_ => { if (SelectedException is {} x) Navigate(PageNames.ReconciliationExceptions, x.Id); });
    }
    private async Task LoadAsync(CancellationToken ct)
    {
        Exceptions.Clear();
        foreach (var row in (await Existing.GetExceptionsAsync(DateOnly.FromDateTime(FromDate),DateOnly.FromDateTime(ToDate),null,ct)).Items.Where(x => x.PositionSessionId == null)) Exceptions.Add(row);
        Summaries.Clear(); foreach (var row in await Client.SummariesAsync(DateOnly.FromDateTime(ToDate),ct)) Summaries.Add(row);
    }
    public override Task InitializeAsync(CancellationToken ct = default) => RunAsync(() => LoadAsync(ct));
}

/// <summary>Assignment queue is separate from the investigator's working page.</summary>
public sealed class ReconciliationExceptionsViewModel : ReconciliationOperationViewModel
{
    private ReconciliationExceptionResponse? _selected;
    private ReconciliationStaffOptionResponse? _staff;
    public ObservableCollection<ReconciliationExceptionResponse> Exceptions { get; } = [];
    public ObservableCollection<ReconciliationStaffOptionResponse> Staff { get; } = [];
    public ReconciliationExceptionResponse? SelectedException { get => _selected; set => SetProperty(ref _selected,value); }
    public ReconciliationStaffOptionResponse? SelectedInvestigator { get => _staff; set => SetProperty(ref _staff,value); }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AssignCommand { get; }
    public RelayCommand InvestigateCommand { get; }
    public ReconciliationExceptionsViewModel(ReconciliationWorkflowClient c, IReconciliationService e, AuthContext a) : base(c,e,a)
    {
        RefreshCommand = new(() => InitializeAsync());
        AssignCommand = new(() => RunAsync(async () =>
        {
            if (SelectedException is not {} x || SelectedInvestigator is not {} staff) return;
            await Client.AssignAsync(x.Id, new(staff.Id,x.Notes ?? "",null,DateOnly.FromDateTime(DateTime.Today.AddDays(7))),default);
            await LoadAsync(default);
        }), () => CanInvestigate);
        InvestigateCommand = new(_ => { if (SelectedException is {} x) Navigate(PageNames.ReconciliationInvestigations,x.Id); });
    }
    private async Task LoadAsync(CancellationToken ct)
    {
        var selected = SelectedException?.Id ?? ExceptionId;
        Exceptions.Clear(); foreach (var x in (await Existing.GetExceptionsAsync(null,null,null,ct)).Items.Where(x => x.PositionSessionId == null && x.Status != "Resolved")) Exceptions.Add(x);
        SelectedException = Exceptions.FirstOrDefault(x => x.Id == selected);
        if (CanInvestigate) { Staff.Clear(); foreach (var x in await Existing.GetInvestigatorOptionsAsync(ct)) Staff.Add(x); }
    }
    public override Task InitializeAsync(CancellationToken ct = default) => RunAsync(() => LoadAsync(ct));
}

/// <summary>Maintains the investigator's diagnosis and evidence independently of proposal submission.</summary>
public sealed class ReconciliationInvestigationsViewModel : ReconciliationOperationViewModel
{
    private ReconciliationExceptionResponse? _selected;
    private string _findings = "";
    private DiscrepancyDefinition? _kind;
    private DateTime _due = DateTime.Today.AddDays(7);
    public ObservableCollection<ReconciliationExceptionResponse> Exceptions { get; } = [];
    public ObservableCollection<DiscrepancyDefinition> Kinds { get; } = [];
    public ReconciliationExceptionResponse? SelectedException { get => _selected; set { if(SetProperty(ref _selected,value)) { Findings=value?.Notes ?? ""; SelectedKind=Kinds.FirstOrDefault(x=>x.Kind==value?.Diagnosis); ReviewDueDate=value?.ReviewDueDate?.ToDateTime(TimeOnly.MinValue)??DateTime.Today.AddDays(7); } } }
    public string Findings { get => _findings; set => SetProperty(ref _findings,value); }
    public DateTime ReviewDueDate { get => _due; set => SetProperty(ref _due,value); }
    public DiscrepancyDefinition? SelectedKind { get => _kind; set => SetProperty(ref _kind,value); }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand PrepareCommand { get; }
    public ReconciliationInvestigationsViewModel(ReconciliationWorkflowClient c, IReconciliationService e, AuthContext a) : base(c,e,a)
    {
        SaveCommand = new(() => RunAsync(async () =>
        {
            if (SelectedException is not {} x || x.AssignedTo != Auth.UserId) return;
            await Client.AssignAsync(x.Id,new(Auth.UserId!.Value,Findings,SelectedKind?.Kind,DateOnly.FromDateTime(ReviewDueDate)),default);
        }), () => CanInvestigate);
        PrepareCommand = new(_ => { if (SelectedException is {} x && x.AssignedTo == Auth.UserId) Navigate(PageNames.ReconciliationProposalEditor,x.Id); });
    }
    public override Task InitializeAsync(CancellationToken ct = default) => RunAsync(async () =>
    {
        Exceptions.Clear(); foreach(var x in (await Existing.GetExceptionsAsync(null,null,null,ct)).Items.Where(x => x.PositionSessionId == null && x.AssignedTo == Auth.UserId && x.Status != "Resolved")) Exceptions.Add(x);
        SelectedException = Exceptions.FirstOrDefault(x => x.Id == ExceptionId) ?? Exceptions.FirstOrDefault();
        Kinds.Clear(); foreach(var x in await Client.DefinitionsAsync(ct)) Kinds.Add(x);
    });
}

/// <summary>Reusable proposal list state; each operation has a distinct page and commands.</summary>
public abstract class ReconciliationProposalQueueViewModel : ReconciliationOperationViewModel
{
    private CorrectionProposalResponse? _selected;
    private string _note = "", _attestation = "";
    private ReconciliationExceptionDetailResponse? _timeline;
    public ObservableCollection<CorrectionProposalResponse> Proposals { get; } = [];
    public CorrectionProposalResponse? SelectedProposal { get => _selected; set { SetProperty(ref _selected,value); OnPropertyChanged(nameof(Preview)); } }
    public CorrectionPreview? Preview => SelectedProposal?.Preview;
    public string ReviewNote { get => _note; set => SetProperty(ref _note,value); }
    public string RecoveryAttestation { get => _attestation; set => SetProperty(ref _attestation,value); }
    public ReconciliationExceptionDetailResponse? Timeline { get => _timeline; set => SetProperty(ref _timeline,value); }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ApproveCommand { get; }
    public AsyncRelayCommand RejectCommand { get; }
    public AsyncRelayCommand VerifyCommand { get; }
    public AsyncRelayCommand TimelineCommand { get; }
    public RelayCommand EditCommand { get; }
    protected abstract bool Include(CorrectionProposalResponse proposal);
    protected ReconciliationProposalQueueViewModel(ReconciliationWorkflowClient c, IReconciliationService e, AuthContext a) : base(c,e,a)
    {
        RefreshCommand = new(() => InitializeAsync());
        ApproveCommand = new(() => ReviewAsync(true), () => CanApprove);
        RejectCommand = new(() => ReviewAsync(false), () => CanApprove);
        VerifyCommand = new(() => RunAsync(async () => { if(SelectedProposal is {} p) { SelectedProposal=await Client.VerifyAsync(p.Id,default); await LoadAsync(default); }}), () => CanInvestigate);
        EditCommand = new(_ => { if(SelectedProposal is {} p) Navigate(PageNames.ReconciliationProposalEditor,p.ExceptionId,p.Id); });
        TimelineCommand = new(() => RunAsync(async () => { if(SelectedProposal is {} p) Timeline=await Existing.GetExceptionByIdAsync(p.ExceptionId,default); }));
    }
    private Task ReviewAsync(bool approve) => RunAsync(async () => { if(SelectedProposal is {} p) { SelectedProposal=await Client.ReviewAsync(p.Id,new(approve,ReviewNote,RecoveryAttestation),default); await LoadAsync(default); }});
    private async Task LoadAsync(CancellationToken ct)
    {
        var selected = SelectedProposal?.Id ?? ProposalId;
        Proposals.Clear(); foreach(var p in (await Client.ProposalsAsync(ExceptionId,ct)).Where(Include)) Proposals.Add(p);
        SelectedProposal=Proposals.FirstOrDefault(x=>x.Id==selected) ?? Proposals.FirstOrDefault();
    }
    public override Task InitializeAsync(CancellationToken ct = default) => RunAsync(() => LoadAsync(ct));
}
public sealed class ReconciliationProposalsViewModel(ReconciliationWorkflowClient c,IReconciliationService e,AuthContext a) : ReconciliationProposalQueueViewModel(c,e,a)
{ protected override bool Include(CorrectionProposalResponse p) => p.ProposerId == Auth.UserId; }
public sealed class ReconciliationApprovalsViewModel(ReconciliationWorkflowClient c,IReconciliationService e,AuthContext a) : ReconciliationProposalQueueViewModel(c,e,a)
{ protected override bool Include(CorrectionProposalResponse p) => p.Status == ProposalStatus.PendingApproval; }
public sealed class ReconciliationHistoryViewModel(ReconciliationWorkflowClient c,IReconciliationService e,AuthContext a) : ReconciliationProposalQueueViewModel(c,e,a)
{ protected override bool Include(CorrectionProposalResponse p) => true; }

