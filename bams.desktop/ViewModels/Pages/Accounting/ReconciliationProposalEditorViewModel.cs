using System.Collections.ObjectModel;
using System.Globalization;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Three-step proposal editor. Preview and posting decisions remain server authoritative.</summary>
public sealed class ReconciliationProposalEditorViewModel : ReconciliationOperationViewModel
{
    private readonly IDialogService _dialogs;
    private int _step = 1;
    private bool _dirty, _readOnly;
    private long? _draftId;
    private string _transaction = "", _findings = "", _evidence = "", _amount = "", _recovery = "", _accountSearch = "";
    private DateTime _postingDate = DateTime.Today;
    private DiscrepancyDefinition? _kind;
    private CorrectionAccountEvidence? _accountEntry;
    private CorrectionJournalEvidence? _glEntry;
    private CorrectionGlOption? _correctGl, _offsetGl;
    private ReconciliationAccountOptionResponse? _correctAccount;
    private CorrectionPreview? _preview;
    public ObservableCollection<DiscrepancyDefinition> Kinds { get; } = [];
    public ObservableCollection<CorrectionAccountEvidence> AccountEntries { get; } = [];
    public ObservableCollection<CorrectionJournalEvidence> JournalEntries { get; } = [];
    public ObservableCollection<CorrectionGlOption> GlAccounts { get; } = [];
    public ObservableCollection<ReconciliationAccountOptionResponse> Accounts { get; } = [];
    public int Step { get => _step; private set { SetProperty(ref _step,value); foreach(string name in new[]{"IsStep1","IsStep2","IsStep3","IsStep4","IsNotLastStep","StepLabel"}) OnPropertyChanged(name); } }
    public string StepLabel => $"Step {Step} of 3";
    public bool IsStep1 => Step == 1; public bool IsStep2 => Step == 2; public bool IsStep3 => Step == 3; public bool IsStep4 => Step == 4;
    public bool IsNotLastStep => Step < 3;
    public bool IsReadOnly { get => _readOnly; private set { SetProperty(ref _readOnly,value); OnPropertyChanged(nameof(CanEdit)); } }
    public bool CanEdit => !IsReadOnly;
    public bool IsDirty => _dirty;
    public string ContextLabel => $"Exception #{ExceptionId}";
    public DiscrepancyDefinition? SelectedKind
    {
        get => _kind;
        set
        {
            if(!SetProperty(ref _kind,value)) return;
            AccountEntry=null; GlEntry=null; CorrectGl=null; OffsetGl=null; CorrectAccount=null; CorrectAmount="";
            Invalidate();
            foreach(string n in new[]{"NeedsAccountEntry","NeedsGlEntry","NeedsCorrectGl","NeedsOffsetGl","NeedsCorrectAccount","NeedsAmount","NeedsPostingDate"}) OnPropertyChanged(n);
        }
    }
    private bool Needs(string name) => SelectedKind?.RequiredInputs.Split(',').Contains(name) == true;
    public bool NeedsAccountEntry => Needs("AccountEntryId"); public bool NeedsGlEntry => Needs("GlEntryId");
    public bool NeedsCorrectGl => Needs("CorrectGlAccountId"); public bool NeedsOffsetGl => Needs("OffsetGlAccountId");
    public bool NeedsCorrectAccount => Needs("CorrectAccountId"); public bool NeedsAmount => Needs("CorrectAmount");
    public bool NeedsPostingDate => Needs("CorrectPostingDate");
    public string TransactionIdText { get => _transaction; set { if(SetProperty(ref _transaction,value)) { AccountEntries.Clear(); JournalEntries.Clear(); AccountEntry=null; GlEntry=null; Invalidate(); } } }
    public string Findings { get => _findings; set { if(SetProperty(ref _findings,value)) Invalidate(); } }
    public string EvidenceReference { get => _evidence; set { if(SetProperty(ref _evidence,value)) Invalidate(); } }
    public string CorrectAmount { get => _amount; set { if(SetProperty(ref _amount,value)) Invalidate(); } }
    public string ExternalRecoveryReference { get => _recovery; set { if(SetProperty(ref _recovery,value)) Invalidate(); } }
    public string AccountSearch { get => _accountSearch; set => SetProperty(ref _accountSearch,value); }
    public DateTime CorrectPostingDate { get => _postingDate; set { if(SetProperty(ref _postingDate,value)) Invalidate(); } }
    public CorrectionAccountEvidence? AccountEntry { get => _accountEntry; set { if(SetProperty(ref _accountEntry,value)) Invalidate(); } }
    public CorrectionJournalEvidence? GlEntry { get => _glEntry; set { if(SetProperty(ref _glEntry,value)) Invalidate(); } }
    public CorrectionGlOption? CorrectGl { get => _correctGl; set { if(SetProperty(ref _correctGl,value)) Invalidate(); } }
    public CorrectionGlOption? OffsetGl { get => _offsetGl; set { if(SetProperty(ref _offsetGl,value)) Invalidate(); } }
    public ReconciliationAccountOptionResponse? CorrectAccount { get => _correctAccount; set { if(SetProperty(ref _correctAccount,value)) Invalidate(); } }
    public CorrectionPreview? Preview { get => _preview; private set => SetProperty(ref _preview,value); }
    public RelayCommand PreviousCommand { get; }
    public RelayCommand GoToStepCommand { get; }
    public RelayCommand BackCommand { get; }
    public AsyncRelayCommand ContinueCommand { get; }
    public AsyncRelayCommand LoadEvidenceCommand { get; }
    public AsyncRelayCommand SearchAccountCommand { get; }
    public AsyncRelayCommand PreviewCommand { get; }
    public AsyncRelayCommand SaveDraftCommand { get; }
    public AsyncRelayCommand SubmitCommand { get; }
    public ReconciliationProposalEditorViewModel(ReconciliationWorkflowClient c,IReconciliationService e,AuthContext a,IDialogService dialogs) : base(c,e,a)
    {
        _dialogs=dialogs;
        PreviousCommand=new(_=> { if(Step>1) Step--; });
        GoToStepCommand=new(value=> { if(int.TryParse(value?.ToString(),out int target) && target>=1 && target<=Step) Step=target; });
        BackCommand=new(_=>Navigate(PageNames.AssignedReconciliationTasks,ExceptionId));
        ContinueCommand=new(()=>RunAsync(async () =>
        {
            if(Step==1 && SelectedKind==null) { ErrorMessage="Choose a discrepancy."; return; }
            if(Step==2 && (!long.TryParse(TransactionIdText,out _) || string.IsNullOrWhiteSpace(Findings) || string.IsNullOrWhiteSpace(EvidenceReference)))
            { ErrorMessage="Select the affected transaction and provide findings and an evidence reference."; return; }
            if(Step==2) { if(!await CalculatePreviewAsync()) return; }
            if(Step<3) Step++;
        }));
        LoadEvidenceCommand=new(()=>RunAsync(()=>LoadEvidenceAsync(default)));
        SearchAccountCommand=new(()=>RunAsync(async()=> { Accounts.Clear(); foreach(ReconciliationAccountOptionResponse row in await Existing.SearchAccountsAsync(AccountSearch,default)) Accounts.Add(row); }));
        PreviewCommand=new(()=>RunAsync(async()=> { await CalculatePreviewAsync(); }));
        SaveDraftCommand=new(()=>RunAsync(()=>SaveAsync(false)),()=>CanInvestigate && CanEdit);
        SubmitCommand=new(()=>RunAsync(()=>SaveAsync(true)),()=>CanInvestigate && CanEdit);
    }
    // Any change invalidates the approval preview; the server checks its revision again on submission.
    private void Invalidate() { Preview=null; _dirty=true; }
    private CorrectionInputs? Inputs(bool draft)
    {
        if(SelectedKind==null || !long.TryParse(TransactionIdText,out long id))
        { ErrorMessage="Choose a discrepancy and enter a valid transaction ID."; return null; }
        decimal? amount=null;
        if(!string.IsNullOrWhiteSpace(CorrectAmount))
        {
            if(!decimal.TryParse(CorrectAmount,NumberStyles.Number,CultureInfo.CurrentCulture,out decimal parsed))
            { ErrorMessage="Enter a valid amount."; return null; }
            amount=parsed;
        }
        return new(SelectedKind.Kind,id,Findings,EvidenceReference,AccountEntry?.Id,GlEntry?.Id,CorrectAccount?.Id,
            CorrectGl?.Id,OffsetGl?.Id,amount,NeedsPostingDate?DateOnly.FromDateTime(CorrectPostingDate):null,
            ExternalRecoveryReference:ExternalRecoveryReference);
    }
    private async Task<bool> CalculatePreviewAsync()
    {
        CorrectionInputs? input=Inputs(false); if(input==null || ExceptionId==null) return false;
        Preview=await Client.PreviewAsync(ExceptionId.Value,input,default); return true;
    }
    private async Task LoadEvidenceAsync(CancellationToken ct)
    {
        if(!long.TryParse(TransactionIdText,out long id)) { ErrorMessage="Enter a valid transaction ID."; return; }
        CorrectionEvidence evidence=await Client.EvidenceAsync(id,ct);
        AccountEntries.Clear(); foreach(CorrectionAccountEvidence x in evidence.AccountEntries) AccountEntries.Add(x);
        JournalEntries.Clear(); foreach(CorrectionJournalEvidence x in evidence.JournalEntries) JournalEntries.Add(x);
    }
    private async Task SaveAsync(bool submit)
    {
        CorrectionInputs? input=Inputs(!submit); if(input==null || ExceptionId==null) return;
        if(submit && Preview==null) { ErrorMessage="Calculate the posting preview before submitting."; return; }
        CorrectionProposalResponse result=await Client.SaveAsync(ExceptionId.Value,_draftId,new(input,submit,Preview?.SourceHash),default);
        _draftId=result.Id; _dirty=false;
        if(submit) { IsReadOnly=true; Navigate(PageNames.AssignedReconciliationTasks,ExceptionId,result.Id); }
    }
    // Shell invokes this before leaving the routed editor, including sidebar navigation.
    public async Task<bool> ConfirmLeaveAsync()
    {
        if(!_dirty || IsReadOnly) return true;
        if(_dialogs.Confirm(new("Unsaved proposal","Save a draft before leaving?","Save draft","Discard changes")))
        {
            await RunAsync(()=>SaveAsync(false));
            return !_dirty;
        }
        _dirty=false; return true;
    }
    public override Task InitializeAsync(CancellationToken ct=default)=>RunAsync(async()=>
    {
        Kinds.Clear(); foreach(DiscrepancyDefinition x in await Client.DefinitionsAsync(ct)) Kinds.Add(x);
        GlAccounts.Clear(); foreach(CorrectionGlOption x in await Client.GlOptionsAsync(ct)) GlAccounts.Add(x);
        if(ExceptionId.HasValue)
        {
            ReconciliationExceptionDetailResponse detail=await Existing.GetExceptionByIdAsync(ExceptionId.Value,ct);
            Findings=detail.Exception.Notes??"";
            SelectedKind=Kinds.FirstOrDefault(x=>x.Kind==detail.Exception.Diagnosis);
            TransactionIdText=detail.Exception.RelatedTransactionId?.ToString()??"";
        }
        if(ProposalId.HasValue)
        {
            CorrectionProposalResponse p=await Client.ProposalAsync(ProposalId.Value,ct); CorrectionInputs input=p.Inputs;
            SelectedKind=Kinds.FirstOrDefault(x=>x.Kind==input.Kind);
            TransactionIdText=input.TransactionId.ToString(); Findings=input.Findings; EvidenceReference=input.EvidenceReference;
            ExternalRecoveryReference=input.ExternalRecoveryReference??"";
            await LoadEvidenceAsync(ct);
            AccountEntry=AccountEntries.FirstOrDefault(x=>x.Id==input.AccountEntryId);
            GlEntry=JournalEntries.FirstOrDefault(x=>x.Id==input.GlEntryId);
            CorrectGl=GlAccounts.FirstOrDefault(x=>x.Id==input.CorrectGlAccountId);
            OffsetGl=GlAccounts.FirstOrDefault(x=>x.Id==input.OffsetGlAccountId);
            if(input.CorrectAccountId.HasValue)
            {
                foreach(ReconciliationAccountOptionResponse x in await Existing.SearchAccountsAsync(null,ct)) Accounts.Add(x);
                CorrectAccount=Accounts.FirstOrDefault(x=>x.Id==input.CorrectAccountId);
            }
            CorrectAmount=input.CorrectAmount?.ToString(CultureInfo.CurrentCulture)??"";
            CorrectPostingDate=input.CorrectPostingDate?.ToDateTime(TimeOnly.MinValue)??DateTime.Today;
            IsReadOnly=p.Status is ProposalStatus.PendingApproval or ProposalStatus.Executed;
            _draftId=p.Status==ProposalStatus.Draft?p.Id:null;
            Preview=p.Preview;
            if(IsReadOnly) Step=3;
        }
        _dirty=false; OnPropertyChanged(nameof(ContextLabel));
    });
}


