using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Presentation state and commands for acknowledging or reassigning session cash handoffs.</summary>
public sealed class CashHandoffCenterViewModel : ViewModelBase
{
    private readonly ICashOperationsClientService _service;
    private readonly AuthContext _auth;
    private DateOnly? _businessDate;
    private CashHandoffResponse? _selectedHandoff;
    private CashHandoffRecipientResponse? _selectedRecipient;
    private string _note = string.Empty;
    private string _message = string.Empty;

    public ObservableCollection<CashHandoffResponse> Handoffs { get; } = [];
    public ObservableCollection<CashHandoffRecipientResponse> Recipients { get; } = [];
    public ObservableCollection<CashHandoffHistoryResponse> History { get; } = [];
    public CashHandoffResponse? SelectedHandoff
    {
        get => _selectedHandoff;
        set
        {
            if (SetProperty(ref _selectedHandoff, value))
            {
                AcceptCommand.RaiseCanExecuteChanged();
                DeclineCommand.RaiseCanExecuteChanged();
                ReassignCommand.RaiseCanExecuteChanged();
                LoadHistoryCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public CashHandoffRecipientResponse? SelectedRecipient
    {
        get => _selectedRecipient;
        set { if (SetProperty(ref _selectedRecipient, value)) ReassignCommand.RaiseCanExecuteChanged(); }
    }
    public string Note { get => _note; set => SetProperty(ref _note, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand LoadHistoryCommand { get; }
    public AsyncRelayCommand AcceptCommand { get; }
    public AsyncRelayCommand DeclineCommand { get; }
    public AsyncRelayCommand ReassignCommand { get; }

    public CashHandoffCenterViewModel(ICashOperationsClientService service, AuthContext auth)
    {
        _service = service;
        _auth = auth;
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(), () => HasAccess);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync, () => HasAccess && SelectedHandoff is not null);
        AcceptCommand = new AsyncRelayCommand(AcceptAsync, () => HasAccess && SelectedHandoff?.Status == "PendingAcceptance");
        DeclineCommand = new AsyncRelayCommand(DeclineAsync, () => HasAccess && SelectedHandoff?.Status == "PendingAcceptance");
        ReassignCommand = new AsyncRelayCommand(ReassignAsync, () => HasAccess && SelectedHandoff?.Status != "Accepted" && SelectedRecipient is not null);
    }

    public bool HasAccess => _auth.HasPermission(PermissionCodes.CashOperations) ||
        _auth.HasPermission(PermissionCodes.Audit) || _auth.HasPermission(PermissionCodes.EndOfDayApproval);

    public async Task LoadForBusinessDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        _businessDate = date;
        if (HasAccess) await RefreshAsync(cancellationToken);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!HasAccess) return;
        try
        {
            Handoffs.Clear();
            foreach (var handoff in await _service.GetCashHandoffsAsync(_businessDate, cancellationToken)) Handoffs.Add(handoff);
            Recipients.Clear();
            foreach (var recipient in await _service.GetHandoffRecipientsAsync(cancellationToken)) Recipients.Add(recipient);
            SelectedHandoff = Handoffs.FirstOrDefault(item => item.Id == SelectedHandoff?.Id) ?? Handoffs.FirstOrDefault();
            Message = string.Empty;
        }
        catch (AppException exception) { Message = exception.Message; }
    }

    private async Task AcceptAsync()
    {
        if (!HasAccess || SelectedHandoff is null) return;
        await ExecuteAsync(() => _service.AcceptCashHandoffAsync(SelectedHandoff.Id, new CashHandoffActionRequest(Note, SelectedHandoff.Version), CancellationToken.None), "Cash receipt acknowledged.");
    }

    private async Task LoadHistoryAsync()
    {
        if (!HasAccess || SelectedHandoff is null) return;
        try
        {
            var detail = await _service.GetCashHandoffDetailAsync(SelectedHandoff.Id, CancellationToken.None);
            History.Clear();
            foreach (var item in detail.History) History.Add(item);
            Message = string.Empty;
        }
        catch (AppException exception) { Message = exception.Message; }
    }

    private async Task DeclineAsync()
    {
        if (!HasAccess || SelectedHandoff is null) return;
        if (string.IsNullOrWhiteSpace(Note)) { Message = "Enter a note explaining why the cash handoff is declined."; return; }
        await ExecuteAsync(() => _service.DeclineCashHandoffAsync(SelectedHandoff.Id, new CashHandoffActionRequest(Note, SelectedHandoff.Version), CancellationToken.None), "Cash handoff declined. It will continue to block business-date closing.");
    }

    private async Task ReassignAsync()
    {
        if (!HasAccess || SelectedHandoff is null || SelectedRecipient is null) return;
        await ExecuteAsync(() => _service.ReassignCashHandoffAsync(SelectedHandoff.Id,
            new ReassignCashHandoffRequest(SelectedRecipient.UserId, Note, SelectedHandoff.Version), CancellationToken.None), "Cash handoff reassigned and awaiting recipient acknowledgement.");
    }

    private async Task ExecuteAsync(Func<Task<CashHandoffResponse>> action, string success)
    {
        try { await action(); await RefreshAsync(); Message = success; }
        catch (AppException exception) { Message = exception.Message; }
    }
}
