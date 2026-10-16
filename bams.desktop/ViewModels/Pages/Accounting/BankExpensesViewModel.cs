using System.Collections.ObjectModel;
using System.IO;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

public sealed class BankExpensesViewModel:ReconciliationOperationViewModel
{
    private readonly UnifiedReconciliationClient _unified;
    private readonly ICashOperationsClientService _cash;
    private CashPositionSessionResponse? _session;
    private string _amount="",_payee="",_purpose="",_missing="";
    private string? _receiptName,_receiptType,_receipt;
    public ObservableCollection<CashPositionSessionResponse> Sessions { get; }=[];
    public ObservableCollection<BankExpenseResponse> Expenses { get; }=[];
    public CashPositionSessionResponse? SelectedSession { get=>_session; set=>SetProperty(ref _session,value); }
    public string Amount { get=>_amount; set=>SetProperty(ref _amount,value); }
    public string Payee { get=>_payee; set=>SetProperty(ref _payee,value); }
    public string Purpose { get=>_purpose; set=>SetProperty(ref _purpose,value); }
    public string MissingReceiptReason { get=>_missing; set=>SetProperty(ref _missing,value); }
    public string? ReceiptName=>_receiptName;
    public bool CanReview=>Auth.HasPermission(PermissionCodes.EndOfDayApproval);
    public AsyncRelayCommand SubmitCommand { get; }
    public AsyncRelayCommand AttachCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AcceptCommand { get; }
    public AsyncRelayCommand RejectCommand { get; }
    public AsyncRelayCommand ReceiptCommand { get; }
    public BankExpensesViewModel(ReconciliationWorkflowClient c,IReconciliationService e,AuthContext a,
        UnifiedReconciliationClient unified,ICashOperationsClientService cash,IDialogService dialogs):base(c,e,a)
    {
        _unified=unified; _cash=cash;
        ReceiptCommand=new(value=>RunAsync(async()=>
        {
            if(value is not BankExpenseResponse row || row.ReceiptFileName==null) return;
            string? path=dialogs.PickSaveFile("Save expense receipt",row.ReceiptFileName,"Receipt|*.pdf;*.jpg;*.jpeg;*.png");
            if(path==null) return;
            try { await unified.DownloadExpenseReceiptAsync(row.Id,path); }
            catch(IOException error) { ErrorMessage=error.Message; }
            catch(UnauthorizedAccessException error) { ErrorMessage=error.Message; }
        }),value=>value is BankExpenseResponse {ReceiptFileName:not null});
        SubmitCommand=new(()=>RunAsync(async()=>
        {
            if(SelectedSession==null || !decimal.TryParse(Amount,out decimal amount)) { ErrorMessage="Choose a cash position and enter the expense amount."; return; }
            await unified.SubmitExpenseAsync(new(SelectedSession.Id,amount,Payee,Purpose,_receiptName,_receiptType,_receipt,MissingReceiptReason));
            Amount=""; Payee=""; Purpose=""; MissingReceiptReason=""; _receipt=null; _receiptName=null; _receiptType=null;
            OnPropertyChanged(nameof(ReceiptName)); await LoadAsync(default);
        }));
        RefreshCommand=new(()=>InitializeAsync());
        AttachCommand=new(()=>RunAsync(async()=>
        {
            string? path=dialogs.PickFile("Expense receipt","Receipts|*.pdf;*.jpg;*.jpeg;*.png");
            if(path==null) return;
            try
            {
                FileInfo info=new(path);
                if(info.Length>5*1024*1024) { ErrorMessage="Receipt must be no larger than 5 MB."; return; }
                byte[] bytes=await File.ReadAllBytesAsync(path); _receipt=Convert.ToBase64String(bytes);
                _receiptName=Path.GetFileName(path); _receiptType=Path.GetExtension(path).ToLowerInvariant() switch { ".pdf"=>"application/pdf", ".png"=>"image/png", _=>"image/jpeg" };
                OnPropertyChanged(nameof(ReceiptName));
            }
            catch(IOException error) { ErrorMessage=error.Message; }
            catch(UnauthorizedAccessException error) { ErrorMessage=error.Message; }
        }));
        AcceptCommand=new(value=>ReviewAsync(value,true,dialogs),value=>CanReview && value is BankExpenseResponse {Status:"PendingApproval"});
        RejectCommand=new(value=>ReviewAsync(value,false,dialogs),value=>CanReview && value is BankExpenseResponse {Status:"PendingApproval"});
    }
    private Task ReviewAsync(object? value,bool approve,IDialogService dialogs)=>RunAsync(async()=>
    {
        if(value is not BankExpenseResponse row) return;
        ReconciliationActionDialog dialog=new(approve?"Approve bank expense":"Reject bank expense",false);
        if(dialogs.ShowDialog(dialog)) { await _unified.ReviewExpenseAsync(row.Id,approve,dialog.Note); await LoadAsync(default); }
    });
    private async Task LoadAsync(CancellationToken ct)
    {
        Sessions.Clear();
        foreach(CashPositionSessionResponse session in await _cash.GetSessionsAsync(null,ct))
            if(session.Status=="Open" && (session.TellerId==Auth.UserId || CanReview && session.PositionType=="Vault")) Sessions.Add(session);
        SelectedSession=Sessions.FirstOrDefault();
        Expenses.Clear(); foreach(BankExpenseResponse expense in await _unified.ExpensesAsync(ct)) Expenses.Add(expense);
    }
    public override Task InitializeAsync(CancellationToken ct=default)=>RunAsync(()=>LoadAsync(ct));
}
