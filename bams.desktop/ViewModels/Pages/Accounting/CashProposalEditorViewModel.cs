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
    private string _findings="",_evidence="",_amount="",_account="",_transaction="",_movement="",_destination="",_counterpart="",_handoff="",_payee="",_purpose="",_missingReceipt="";
    private string? _receiptName,_receiptType,_receiptBase64;
    private CashCorrectionPreview? _preview;
    private CashCorrectionInputs? _previewInputs;
    private CashSourceCatalog? _catalog;
    private string _sourceSearch = "";
    private CashSourceOption? _selectedAccount, _selectedTransaction, _selectedMovement, _selectedDestination, _selectedCounterpart, _selectedHandoff;
    public string SourceSearch { get => _sourceSearch; set => SetProperty(ref _sourceSearch, value); }
    public AsyncRelayCommand SearchSourcesCommand { get; }
    public IReadOnlyList<CashSourceOption> AccountOptions => _catalog?.Accounts ?? [];
    public IReadOnlyList<CashSourceOption> TransactionOptions => _catalog?.Transactions ?? [];
    public IReadOnlyList<CashSourceOption> DestinationOptions => _catalog?.Sessions.Where(x => x.Id != _catalog.SourceSessionId).ToList() ?? [];
    public IReadOnlyList<CashSourceOption> MovementOptions => _catalog?.Movements.Where(x => x.SessionId == _catalog.SourceSessionId &&
        (SelectedKind?.Kind switch
        {
            CashDiscrepancyKind.IncorrectMovementAmount or CashDiscrepancyKind.WrongCashPosition => x.TransactionId.HasValue,
            CashDiscrepancyKind.TransferMismatch => x.Type is "TransferIn" or "TransferOut" or "VaultIssue" or "CashReceipt",
            CashDiscrepancyKind.HandoffMismatch => x.Type is "VaultReturnIn" or "VaultReturnOut",
            _ => true
        })).ToList() ?? [];
    public IReadOnlyList<CashSourceOption> CounterpartOptions => _catalog?.Movements.Where(x => x.Id != SelectedMovement?.Id &&
        x.SessionId == (SelectedKind?.Kind == CashDiscrepancyKind.DuplicateMovement ? _catalog.SourceSessionId : SelectedDestination?.Id)).ToList() ?? [];
    public IReadOnlyList<CashSourceOption> HandoffOptions => _catalog?.Handoffs.Where(x => x.SessionId == _catalog.SourceSessionId || x.DestinationSessionId == _catalog.SourceSessionId).ToList() ?? [];
    public IReadOnlyList<CashSourceJournalLine> SelectedJournalLines => _catalog?.JournalLines.Where(x => x.TransactionId == (SelectedTransaction?.Id ?? SelectedMovement?.TransactionId)).ToList() ?? [];
    public CashSourceOption? SelectedAccount { get => _selectedAccount; set { if (SetProperty(ref _selectedAccount, value)) AccountId = value?.Id.ToString() ?? ""; } }
    public CashSourceOption? SelectedTransaction { get => _selectedTransaction; set { if (SetProperty(ref _selectedTransaction, value)) { TransactionId = value?.Id.ToString() ?? ""; OnPropertyChanged(nameof(SelectedJournalLines)); } } }
    public CashSourceOption? SelectedMovement { get => _selectedMovement; set { if (SetProperty(ref _selectedMovement, value)) { MovementId = value?.Id.ToString() ?? ""; SelectedCounterpart = null; OnPropertyChanged(nameof(CounterpartOptions)); OnPropertyChanged(nameof(SelectedJournalLines)); if (value?.Amount is decimal amount && SelectedKind?.Kind is CashDiscrepancyKind.IncorrectMovementAmount or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch) Amount = amount.ToString("0.00"); } } }
    public CashSourceOption? SelectedDestination { get => _selectedDestination; set { if (SetProperty(ref _selectedDestination, value)) { DestinationId = value?.Id.ToString() ?? ""; SelectedCounterpart = null; OnPropertyChanged(nameof(CounterpartOptions)); } } }
    public CashSourceOption? SelectedCounterpart { get => _selectedCounterpart; set { if (SetProperty(ref _selectedCounterpart, value)) CounterpartMovementId = value?.Id.ToString() ?? ""; } }
    public CashSourceOption? SelectedHandoff { get => _selectedHandoff; set { if (SetProperty(ref _selectedHandoff, value)) HandoffId = value?.Id.ToString() ?? ""; } }
    private async Task LoadSourcesAsync(CancellationToken ct)
    {
        if (!ExceptionId.HasValue) return;
        Invalidate();
        CashSourceOption? account = SelectedAccount, transaction = SelectedTransaction, movement = SelectedMovement,
            destination = SelectedDestination, counterpart = SelectedCounterpart, handoff = SelectedHandoff;
        CashSourceCatalog loaded = await _unified.SourcesAsync(ExceptionId.Value, SourceSearch, ct);
        IReadOnlyList<CashSourceJournalLine> retainedJournal = _catalog?.JournalLines.Where(line =>
            !loaded.JournalLines.Any(current => current.TransactionId == line.TransactionId) &&
            (line.TransactionId == transaction?.Id || line.TransactionId == movement?.TransactionId)).ToList() ?? [];
        _catalog = loaded with
        {
            Accounts = KeepSelected(loaded.Accounts, account), Transactions = KeepSelected(loaded.Transactions, transaction),
            Movements = KeepSelected(loaded.Movements, movement, counterpart), Sessions = KeepSelected(loaded.Sessions, destination),
            Handoffs = KeepSelected(loaded.Handoffs, handoff), JournalLines = loaded.JournalLines.Concat(retainedJournal).ToList()
        };
        SelectedAccount = _catalog.Accounts.SingleOrDefault(x => x.Id == account?.Id);
        SelectedTransaction = _catalog.Transactions.SingleOrDefault(x => x.Id == transaction?.Id);
        SelectedMovement = _catalog.Movements.SingleOrDefault(x => x.Id == movement?.Id);
        SelectedDestination = _catalog.Sessions.SingleOrDefault(x => x.Id == destination?.Id);
        SelectedCounterpart = _catalog.Movements.SingleOrDefault(x => x.Id == counterpart?.Id);
        SelectedHandoff = _catalog.Handoffs.SingleOrDefault(x => x.Id == handoff?.Id);
        foreach (string name in new[] { nameof(AccountOptions), nameof(TransactionOptions), nameof(MovementOptions), nameof(DestinationOptions), nameof(CounterpartOptions), nameof(HandoffOptions), nameof(SelectedJournalLines) }) OnPropertyChanged(name);
    }
    private static IReadOnlyList<CashSourceOption> KeepSelected(IReadOnlyList<CashSourceOption> values, params CashSourceOption?[] selected) =>
        values.Concat(selected.OfType<CashSourceOption>()).DistinctBy(value => value.Id).ToList();
    public ObservableCollection<CashDiscrepancyDefinition> Kinds { get; }=[];
    public CashDiscrepancyDefinition? SelectedKind { get=>_kind; set { if(SetProperty(ref _kind,value)) { if(value?.Kind==CashDiscrepancyKind.CountEntryError) Amount=""; Invalidate(); foreach(string name in new[]{nameof(NeedsAccount),nameof(NeedsTransaction),nameof(NeedsMovement),nameof(NeedsDestination),nameof(NeedsExpense),nameof(NeedsCounterpart),nameof(NeedsHandoff),nameof(NeedsAmount),nameof(AmountLabel),nameof(CounterpartOptions),nameof(NeedsSources),nameof(MovementOptions)}) OnPropertyChanged(name); } } }
    public int Step { get=>_step; private set { SetProperty(ref _step,value); OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); } }
    public bool IsStep1=>Step==1; public bool IsStep2=>Step==2; public bool IsStep3=>Step==3;
    public bool NeedsSources => NeedsAccount || NeedsTransaction || NeedsMovement || NeedsDestination || NeedsHandoff;
    public bool NeedsAmount=>SelectedKind?.Kind is CashDiscrepancyKind.CountEntryError or CashDiscrepancyKind.UnrecordedDeposit or CashDiscrepancyKind.UnrecordedWithdrawal or CashDiscrepancyKind.IncorrectMovementAmount or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch or CashDiscrepancyKind.UnexplainedOverShort or CashDiscrepancyKind.BankExpenseNotRecorded;
    public string AmountLabel=>SelectedKind?.Kind switch
    {
        CashDiscrepancyKind.CountEntryError=>"Actual counted cash",
        CashDiscrepancyKind.IncorrectMovementAmount or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch=>"Correct movement total (not adjustment amount)",
        _=>"Amount"
    };
    public bool NeedsAccount=>SelectedKind?.Kind is CashDiscrepancyKind.UnrecordedDeposit or CashDiscrepancyKind.UnrecordedWithdrawal;
    public bool NeedsTransaction=>SelectedKind?.Kind==CashDiscrepancyKind.MissingMovement;
    public bool NeedsMovement=>SelectedKind?.Kind is CashDiscrepancyKind.DuplicateMovement or CashDiscrepancyKind.IncorrectMovementAmount or CashDiscrepancyKind.WrongCashPosition or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch;
    public bool NeedsDestination=>SelectedKind?.Kind is CashDiscrepancyKind.WrongCashPosition or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch;
    public bool NeedsCounterpart=>SelectedKind?.Kind is CashDiscrepancyKind.DuplicateMovement or CashDiscrepancyKind.TransferMismatch or CashDiscrepancyKind.HandoffMismatch;
    public bool NeedsHandoff=>SelectedKind?.Kind==CashDiscrepancyKind.HandoffMismatch;
    public bool NeedsExpense=>SelectedKind?.Kind==CashDiscrepancyKind.BankExpenseNotRecorded;
    public string Findings { get=>_findings; set { SetProperty(ref _findings,value); Invalidate(); } }
    public string Evidence { get=>_evidence; set { SetProperty(ref _evidence,value); Invalidate(); } }
    public string Amount { get=>_amount; set { SetProperty(ref _amount,value); Invalidate(); } }
    public string AccountId { get=>_account; set { SetProperty(ref _account,value); Invalidate(); } }
    public string TransactionId { get=>_transaction; set { SetProperty(ref _transaction,value); Invalidate(); } }
    public string MovementId { get=>_movement; set { SetProperty(ref _movement,value); Invalidate(); } }
    public string DestinationId { get=>_destination; set { SetProperty(ref _destination,value); Invalidate(); } }
    public string CounterpartMovementId { get=>_counterpart; set { SetProperty(ref _counterpart,value); Invalidate(); } }
    public string HandoffId { get=>_handoff; set { SetProperty(ref _handoff,value); Invalidate(); } }
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
        SearchSourcesCommand = new(() => RunAsync(() => LoadSourcesAsync(default)));
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
            await unified.SubmitAsync(ExceptionId.Value,_previewInputs,Preview.PreviewHash);
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
        if(NeedsAmount && !string.IsNullOrWhiteSpace(Amount)) { if(!decimal.TryParse(Amount,out decimal parsed)) { ErrorMessage="Enter a valid amount."; return null; } amount=parsed; }
        if((NeedsAccount && !long.TryParse(AccountId,out _)) || (NeedsTransaction && !long.TryParse(TransactionId,out _)) ||
            (NeedsMovement && !long.TryParse(MovementId,out _)) || (NeedsDestination && !long.TryParse(DestinationId,out _)) ||
            (NeedsCounterpart && !long.TryParse(CounterpartMovementId,out _)) || (NeedsHandoff && !long.TryParse(HandoffId,out _)))
        { ErrorMessage="Enter valid source identifiers for the selected discrepancy."; return null; }
        return new(SelectedKind!.Kind,Findings,Evidence,amount,NeedsAccount?long.Parse(AccountId):null,
            NeedsTransaction?long.Parse(TransactionId):null,NeedsMovement?long.Parse(MovementId):null,
            NeedsDestination?long.Parse(DestinationId):null,NeedsExpense?Payee:null,NeedsExpense?Purpose:null,
            NeedsExpense?_receiptName:null,NeedsExpense?_receiptType:null,NeedsExpense?_receiptBase64:null,NeedsExpense?MissingReceiptReason:null,NeedsCounterpart?long.Parse(CounterpartMovementId):null,NeedsHandoff?long.Parse(HandoffId):null);
    }
    public override Task InitializeAsync(CancellationToken ct=default)=>RunAsync(async()=>
    {
        await LoadSourcesAsync(ct);
        Kinds.Clear(); foreach(CashDiscrepancyDefinition kind in await _unified.DefinitionsAsync(ct)) Kinds.Add(kind);
        if(ExceptionId.HasValue)
        {
            ReconciliationExceptionDetailResponse detail=await Existing.GetExceptionByIdAsync(ExceptionId.Value,ct);
            Amount=Math.Abs(detail.Exception.Difference).ToString("0.00");
        }
    });
}
