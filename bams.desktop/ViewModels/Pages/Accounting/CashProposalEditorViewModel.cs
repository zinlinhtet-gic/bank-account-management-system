using System.Collections.ObjectModel;
using System.IO;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Three-step investigator form. The server calculates and validates every posting effect.</summary>
public sealed class CashProposalEditorViewModel:ReconciliationOperationViewModel
{
    private readonly UnifiedReconciliationClient _unified;
    private CashDiscrepancyDefinition? _kind;
    private int _step=1;
    private string _findings="",_evidence="",_amount="",_account="",_transaction="",_movement="",_destination="",_payee="",_purpose="",_missingReceipt="";
    private string? _receiptName,_receiptType,_receiptBase64;
    private CashCorrectionPreview? _preview;
    private CashCorrectionInputs? _previewInputs;
    public ObservableCollection<CashDiscrepancyDefinition> Kinds { get; }=[];
    public CashDiscrepancyDefinition? SelectedKind { get=>_kind; set { if(SetProperty(ref _kind,value)) { if(value?.Kind==CashDiscrepancyKind.CountEntryError) Amount=""; Invalidate(); foreach(string name in new[]{nameof(NeedsAccount),nameof(NeedsTransaction),nameof(NeedsMovement),nameof(NeedsDestination),nameof(NeedsExpense)}) OnPropertyChanged(name); } } }
    public int Step { get=>_step; private set { SetProperty(ref _step,value); OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); } }
    public bool IsStep1=>Step==1; public bool IsStep2=>Step==2; public bool IsStep3=>Step==3;
    public bool NeedsAccount=>SelectedKind?.Kind is CashDiscrepancyKind.UnrecordedDeposit or CashDiscrepancyKind.UnrecordedWithdrawal;
    public bool NeedsTransaction=>SelectedKind?.Kind==CashDiscrepancyKind.MissingMovement;
    public bool NeedsMovement=>SelectedKind?.Kind is CashDiscrepancyKind.DuplicateMovement or CashDiscrepancyKind.IncorrectMovementAmount or CashDiscrepancyKind.WrongCashPosition or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch;
    public bool NeedsDestination=>SelectedKind?.Kind is CashDiscrepancyKind.WrongCashPosition or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch;
    public bool NeedsExpense=>SelectedKind?.Kind==CashDiscrepancyKind.BankExpenseNotRecorded;
    public string Findings { get=>_findings; set { SetProperty(ref _findings,value); Invalidate(); } }
    public string Evidence { get=>_evidence; set { SetProperty(ref _evidence,value); Invalidate(); } }
    public string Amount { get=>_amount; set { SetProperty(ref _amount,value); Invalidate(); } }
    public string AccountId { get=>_account; set { SetProperty(ref _account,value); Invalidate(); } }
    public string TransactionId { get=>_transaction; set { SetProperty(ref _transaction,value); Invalidate(); } }
    public string MovementId { get=>_movement; set { SetProperty(ref _movement,value); Invalidate(); } }
    public string DestinationId { get=>_destination; set { SetProperty(ref _destination,value); Invalidate(); } }
    public string Payee { get=>_payee; set { SetProperty(ref _payee,value); Invalidate(); } }
    public string Purpose { get=>_purpose; set { SetProperty(ref _purpose,value); Invalidate(); } }
    public string MissingReceiptReason { get=>_missingReceipt; set { SetProperty(ref _missingReceipt,value); Invalidate(); } }
    public string? ReceiptName=>_receiptName;
    public CashCorrectionPreview? Preview { get=>_preview; private set=>SetProperty(ref _preview,value); }
    public RelayCommand PreviousCommand { get; }
    public AsyncRelayCommand ContinueCommand { get; }
    public AsyncRelayCommand SubmitCommand { get; }
    public AsyncRelayCommand AttachCommand { get; }
    public CashProposalEditorViewModel(ReconciliationWorkflowClient c,IReconciliationService e,AuthContext a,
        UnifiedReconciliationClient unified,IDialogService dialogs):base(c,e,a)
    {
        _unified=unified;
        PreviousCommand=new(_=> { if(Step>1) Step--; });
        ContinueCommand=new(()=>RunAsync(async()=>
        {
            if(SelectedKind==null) { ErrorMessage="Choose a discrepancy."; return; }
            if(Step==1) { Step=2; return; }
            CashCorrectionInputs? input=GetInputs();
            if(input==null || ExceptionId==null) return;
            Preview=await unified.PreviewAsync(ExceptionId.Value,input);
            _previewInputs=input; Step=3;
        }));
        SubmitCommand=new(()=>RunAsync(async()=>
        {
            if(ExceptionId==null || Preview==null || _previewInputs==null) return;
            await unified.SubmitAsync(ExceptionId.Value,_previewInputs,Preview.SourceHash);
            Navigate(PageNames.AssignedReconciliationTasks);
        }));
        AttachCommand=new(()=>RunAsync(async()=>
        {
            string? path=dialogs.PickFile("Expense receipt","Receipts|*.pdf;*.jpg;*.jpeg;*.png");
            if(path==null) return;
            try
            {
                FileInfo info=new(path);
                if(info.Length>5*1024*1024) { ErrorMessage="Receipt must be no larger than 5 MB."; return; }
                byte[] data=await File.ReadAllBytesAsync(path);
                _receiptBase64=Convert.ToBase64String(data); _receiptName=Path.GetFileName(path);
                _receiptType=Path.GetExtension(path).ToLowerInvariant() switch { ".pdf"=>"application/pdf", ".png"=>"image/png", _=>"image/jpeg" };
                OnPropertyChanged(nameof(ReceiptName)); Invalidate();
            }
            catch(IOException error) { ErrorMessage=error.Message; }
            catch(UnauthorizedAccessException error) { ErrorMessage=error.Message; }
        }));
    }
    private void Invalidate() { Preview=null; _previewInputs=null; }
    private CashCorrectionInputs? GetInputs()
    {
        decimal? amount=null;
        if(!string.IsNullOrWhiteSpace(Amount)) { if(!decimal.TryParse(Amount,out decimal parsed)) { ErrorMessage="Enter a valid amount."; return null; } amount=parsed; }
        if((NeedsAccount && !long.TryParse(AccountId,out _)) || (NeedsTransaction && !long.TryParse(TransactionId,out _)) ||
            (NeedsMovement && !long.TryParse(MovementId,out _)) || (NeedsDestination && !long.TryParse(DestinationId,out _)))
        { ErrorMessage="Enter valid source identifiers for the selected discrepancy."; return null; }
        return new(SelectedKind!.Kind,Findings,Evidence,amount,NeedsAccount?long.Parse(AccountId):null,
            NeedsTransaction?long.Parse(TransactionId):null,NeedsMovement?long.Parse(MovementId):null,
            NeedsDestination?long.Parse(DestinationId):null,NeedsExpense?Payee:null,NeedsExpense?Purpose:null,
            NeedsExpense?_receiptName:null,NeedsExpense?_receiptType:null,NeedsExpense?_receiptBase64:null,NeedsExpense?MissingReceiptReason:null);
    }
    public override Task InitializeAsync(CancellationToken ct=default)=>RunAsync(async()=>
    {
        Kinds.Clear(); foreach(CashDiscrepancyDefinition kind in await _unified.DefinitionsAsync(ct)) Kinds.Add(kind);
        if(ExceptionId.HasValue)
        {
            ReconciliationExceptionDetailResponse detail=await Existing.GetExceptionByIdAsync(ExceptionId.Value,ct);
            Amount=Math.Abs(detail.Exception.Difference).ToString("0.00");
        }
    });
}
