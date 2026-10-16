using System.Collections.ObjectModel;
using System.IO;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

public sealed class ReconciliationActionDialog : ViewModelBase, IDialogViewModel
{
    public event Action<bool>? CloseRequested;
    public bool CanCloseOnBackdropClick => false;
    public bool CanCancel => true;
    public string Title { get; }
    public bool IsAssignment { get; }
    public IReadOnlyList<ReconciliationStaffOptionResponse> Staff { get; }
    private ReconciliationStaffOptionResponse? _selected;
    private string _note = "";
    public ReconciliationStaffOptionResponse? SelectedStaff { get => _selected; set => SetProperty(ref _selected,value); }
    public string Note { get => _note; set => SetProperty(ref _note,value); }
    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }
    public ReconciliationActionDialog(string title,bool assignment,IReadOnlyList<ReconciliationStaffOptionResponse>? staff = null)
    {
        Title=title; IsAssignment=assignment; Staff=staff ?? [];
        ConfirmCommand=new(_=> { if((!IsAssignment || SelectedStaff != null) && Note.Trim().Length>=5) CloseRequested?.Invoke(true); });
        CancelCommand=new(_=>CloseRequested?.Invoke(false));
    }
}

/// <summary>The master queue and assigned-task page share row actions and the same server check.</summary>
public class UnifiedReconciliationViewModel : ReconciliationOperationViewModel
{
    private readonly UnifiedReconciliationClient _unified;
    private DateTime _from=DateTime.Today, _to=DateTime.Today;
    private bool _isAccountTabSelected = true;
    private bool _isCashTabSelected;
    public virtual bool AssignedOnly => false;
    public bool IsEmbedded { get; set; }
    public bool CanManage => !AssignedOnly && (Auth.HasPermission(PermissionCodes.Accounting) || CanApprove);
    public bool CanForcePass => !AssignedOnly && CanApprove;
    public DateTime FromDate { get=>_from; set=>SetProperty(ref _from,value); }
    public DateTime ToDate { get=>_to; set=>SetProperty(ref _to,value); }
    public bool IsAccountTabSelected { get=>_isAccountTabSelected; set { if(SetProperty(ref _isAccountTabSelected,value) && value) IsCashTabSelected=false; } }
    public bool IsCashTabSelected { get=>_isCashTabSelected; set { if(SetProperty(ref _isCashTabSelected,value) && value) IsAccountTabSelected=false; } }
    public ObservableCollection<UnifiedExceptionRow> AccountExceptions { get; }=[];
    public ObservableCollection<UnifiedExceptionRow> CashExceptions { get; }=[];
    public AsyncRelayCommand CheckCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AssignCommand { get; }
    public AsyncRelayCommand ForcePassCommand { get; }
    public RelayCommand PerformCommand { get; }
    public UnifiedReconciliationViewModel(ReconciliationWorkflowClient c,IReconciliationService e,AuthContext a,
        UnifiedReconciliationClient unified,IDialogService dialogs):base(c,e,a)
    {
        _unified=unified;
        CheckCommand=new(()=>RunAsync(async()=>Apply(await unified.CheckAsync(DateOnly.FromDateTime(FromDate),DateOnly.FromDateTime(ToDate)))));
        RefreshCommand=new(()=>InitializeAsync());
        PerformCommand=new(value=> { if(value is UnifiedExceptionRow row && row.AssignedTo==Auth.UserId)
            Navigate(row.PositionSessionId.HasValue?PageNames.CashProposalEditor:PageNames.ReconciliationProposalEditor,row.Id); },
            value=> value is UnifiedExceptionRow row && row.AssignedTo==Auth.UserId && CanInvestigate);
        AssignCommand=new(value=>RunAsync(async()=>
        {
            if(value is not UnifiedExceptionRow row) return;
            IReadOnlyList<ReconciliationStaffOptionResponse> staff=await Existing.GetInvestigatorOptionsAsync(default);
            ReconciliationActionDialog dialog=new("Assign investigator",true,staff);
            if(dialogs.ShowDialog(dialog) && dialog.SelectedStaff is not null)
            { await unified.AssignAsync(row.Id,dialog.SelectedStaff.Id,dialog.Note); await LoadAsync(default); }
        }),_=>CanManage);
        ForcePassCommand=new(value=>RunAsync(async()=>
        {
            if(value is not UnifiedExceptionRow row) return;
            ReconciliationActionDialog dialog=new("Force pass exception",false);
            if(dialogs.ShowDialog(dialog)) { await unified.ForcePassAsync(row.Id,dialog.Note); await LoadAsync(default); }
        }),_=>CanForcePass);
    }
    private void Apply(UnifiedReconciliationResponse response)
    {
        AccountExceptions.Clear(); foreach(UnifiedExceptionRow row in response.AccountAndGl) AccountExceptions.Add(row);
        CashExceptions.Clear(); foreach(UnifiedExceptionRow row in response.Cash) CashExceptions.Add(row);
    }
    private async Task LoadAsync(CancellationToken ct)
    {
        if(AssignedOnly)
        {
            IReadOnlyList<UnifiedExceptionRow> rows=await _unified.TasksAsync(ct);
            Apply(new(0,default,default,rows.Where(x=>!x.PositionSessionId.HasValue).ToList(),rows.Where(x=>x.PositionSessionId.HasValue).ToList()));
        }
        else Apply(await _unified.ReadAsync(DateOnly.FromDateTime(FromDate),DateOnly.FromDateTime(ToDate),ct));
    }
    public override Task InitializeAsync(CancellationToken ct=default)=>RunAsync(()=>LoadAsync(ct));
}

public sealed class AssignedReconciliationTasksViewModel(ReconciliationWorkflowClient c,IReconciliationService e,AuthContext a,
    UnifiedReconciliationClient u,IDialogService d):UnifiedReconciliationViewModel(c,e,a,u,d)
{ public override bool AssignedOnly=>true; }

public sealed record ReconcileProposalRow(long Id,long ExceptionId,string Category,string Discrepancy,string Findings,
    string Evidence,string Details,decimal? Amount,long ProposerId,bool IsCash,string? ReceiptName=null,string? ReceiptBase64=null);

public sealed class ReconcileProposalsViewModel:ReconciliationOperationViewModel
{
    private readonly UnifiedReconciliationClient _unified;
    public ObservableCollection<ReconcileProposalRow> Proposals { get; }=[];
    public AsyncRelayCommand AcceptCommand { get; }
    public AsyncRelayCommand RejectCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ReceiptCommand { get; }
    public ReconcileProposalsViewModel(ReconciliationWorkflowClient c,IReconciliationService e,AuthContext a,
        UnifiedReconciliationClient u,IDialogService dialogs):base(c,e,a)
    {
        _unified=u;
        AcceptCommand=new(value=>ReviewAsync(value,true,dialogs),_=>CanApprove);
        RejectCommand=new(value=>ReviewAsync(value,false,dialogs),_=>CanApprove);
        RefreshCommand=new(()=>InitializeAsync());
        ReceiptCommand=new(value=>RunAsync(async()=>
        {
            if(value is not ReconcileProposalRow {ReceiptName:not null,ReceiptBase64:not null} row) return;
            string? path=dialogs.PickSaveFile("Save expense receipt",row.ReceiptName,"Receipt|*.pdf;*.jpg;*.jpeg;*.png");
            if(path==null) return;
            await File.WriteAllBytesAsync(path,Convert.FromBase64String(row.ReceiptBase64));
        }),value=>value is ReconcileProposalRow {ReceiptName:not null,ReceiptBase64:not null});
    }
    private Task ReviewAsync(object? value,bool approve,IDialogService dialogs)=>RunAsync(async()=>
    {
        if(value is not ReconcileProposalRow row) return;
        ReconciliationActionDialog dialog=new(approve?"Accept reconciliation proposal":"Reject reconciliation proposal",false);
        if(!dialogs.ShowDialog(dialog)) return;
        if(row.IsCash) await _unified.ReviewAsync(row.Id,approve,dialog.Note);
        else await Client.ReviewAsync(row.Id,new(approve,dialog.Note),default);
        await LoadAsync(default);
    });
    private async Task LoadAsync(CancellationToken ct)
    {
        Proposals.Clear();
        foreach(CorrectionProposalResponse p in (await Client.ProposalsAsync(null,ct)).Where(x=>x.Status==ProposalStatus.PendingApproval))
            Proposals.Add(new(p.Id,p.ExceptionId,"Account / GL",p.Kind.ToString(),p.Inputs.Findings,p.Inputs.EvidenceReference,
                p.Preview is null ? "" : p.Preview.Explanation + "\n" + string.Join("\n",p.Preview.Lines.Select(line=>$"{line.Direction} {line.Amount:N2} GL {line.AccountId} / customer {line.CustomerAccountId}")),
                p.Preview?.SuggestedAmount,p.ProposerId,false));
        foreach(CashCorrectionProposalResponse p in await _unified.PendingAsync(ct))
            Proposals.Add(new(p.Id,p.ExceptionId,"Cash",p.Kind.ToString(),p.Inputs.Findings,p.Inputs.EvidenceReference,
                $"Payee: {p.Inputs.Payee}; Purpose: {p.Inputs.Purpose}; Receipt: {p.Inputs.ReceiptFileName}; Missing receipt reason: {p.Inputs.MissingReceiptReason}",
                p.Inputs.Amount,p.ProposerId,true,p.Inputs.ReceiptFileName,p.Inputs.ReceiptBase64));
    }
    public override Task InitializeAsync(CancellationToken ct=default)=>RunAsync(()=>LoadAsync(ct));
}
