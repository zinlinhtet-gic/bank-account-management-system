using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Auditor-only submission of physically held cash to a Manager.</summary>
public sealed class CashTransferViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ICashOperationsClientService _service;
    private readonly AuthContext _auth;
    private readonly IDialogService _dialogs;
    private bool _busy;
    private string _message = string.Empty;
    private string _note = string.Empty;
    private CashHandoffRecipientResponse? _manager;
    public bool HasAccess => string.Equals(_auth.Role, RoleCodes.Auditor, StringComparison.OrdinalIgnoreCase) && _auth.HasPermission(PermissionCodes.Audit);
    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) { SendCommand.RaiseCanExecuteChanged(); RefreshCommand.RaiseCanExecuteChanged(); } } }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string Note { get => _note; set => SetProperty(ref _note, value); }
    public CashHandoffRecipientResponse? SelectedManager { get => _manager; set => SetProperty(ref _manager, value); }
    public ObservableCollection<CashHandoffCenterViewModel.ReturnSelection> Handoffs { get; } = [];
    public ObservableCollection<CashHandoffRecipientResponse> Managers { get; } = [];
    public ObservableCollection<AuditorCashReturnResponse> Returns { get; } = [];
    public decimal CashHeld => Handoffs.Sum(x => x.Handoff.Amount);
    public AsyncRelayCommand SendCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    // Keeps the send command connected and leaves validation failures visible to the user.
    public CashTransferViewModel(ICashOperationsClientService service, AuthContext auth, IDialogService dialogs)
    {
        _service = service;
        _auth = auth;
        _dialogs = dialogs;
        SendCommand = new AsyncRelayCommand(SendAsync, () => HasAccess && !IsBusy);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None), () => HasAccess && !IsBusy);
    }

    // Enforces access even when navigation is invoked directly.
    public Task InitializeAsync(CancellationToken cancellationToken) => RefreshAsync(cancellationToken);

    // Loads custody, reserved handoffs and eligible manager recipients as one refresh.
    private async Task LoadAsync(CancellationToken ct)
    {
        var returns = await _service.GetAuditorReturnsAsync(null, ct);
        var handoffs = await _service.GetCashHandoffsAsync(null, ct);
        var managers = await _service.GetAuditorReturnManagersAsync(ct);
        var reserved = returns.Where(x => x.Status == "PendingAcceptance").SelectMany(x => x.HandoffIds).ToHashSet();
        Handoffs.Clear();
        foreach (var h in handoffs.Where(x => x.RecipientUserId == _auth.UserId && x.Status == "Accepted" && x.DestinationSessionId == null))
            Handoffs.Add(new CashHandoffCenterViewModel.ReturnSelection { Handoff = h, IsAvailable = !reserved.Contains(h.Id) });
        Returns.Clear();
        foreach (var row in returns.Where(x => x.AuditorId == _auth.UserId)) Returns.Add(row);
        var selected = SelectedManager?.UserId;
        Managers.Clear();
        foreach (var manager in managers) Managers.Add(manager);
        SelectedManager = Managers.FirstOrDefault(x => x.UserId == selected);
        OnPropertyChanged(nameof(CashHeld));
    }

    // Shows refresh errors beside the controls and in a themed alert.
    private async Task RefreshAsync(CancellationToken ct)
    {
        if (!HasAccess) { ShowError("Only Auditors can access Cash Transfer."); return; }
        IsBusy = true;
        try { await LoadAsync(ct); Message = Handoffs.Count == 0 ? "No accepted cash handoffs are currently held by you." : "Select whole handoffs and a receiving Manager."; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (AppException ex) { ShowError(ex.Message); }
        catch (Exception) { ShowError("Cash Transfer could not be loaded. Please refresh and try again."); }
        finally { IsBusy = false; }
    }

    // Validates the selection before calling the linked cash-return API exactly once.
    private async Task SendAsync()
    {
        if (!HasAccess) { ShowError("Only Auditors can send cash to a Manager."); return; }
        if (SelectedManager is null) { ShowError("Select the receiving Manager."); return; }
        var selected = Handoffs.Where(x => x.IsSelected && x.IsAvailable).ToArray();
        if (selected.Length == 0) { ShowError("Select at least one available cash handoff."); return; }
        if (selected.Select(x => x.Handoff.BusinessDate).Distinct().Count() != 1) { ShowError("Select handoffs from the same business date."); return; }
        IsBusy = true;
        var sent = false;
        try
        {
            await _service.CreateAuditorReturnAsync(new CreateAuditorCashReturnRequest(SelectedManager.UserId, selected.Select(x => x.Handoff.Id).ToArray(), Note), CancellationToken.None);
            sent = true;
            await LoadAsync(CancellationToken.None);
            Message = "Cash transfer sent successfully. Cash remains with you until the Manager acknowledges receipt.";
            _dialogs.ShowMessage("Cash transfer sent", Message);
        }
        catch (Exception ex)
        {
            ShowError(sent ? "The transfer was sent, but the screen could not refresh. Refresh before sending again." :
                ex is AppException app ? app.Message : "Cash transfer could not be sent. Refresh to check its status before trying again.");
        }
        finally { IsBusy = false; }
    }

    // Keeps feedback next to the send button and makes failures immediately noticeable.
    private void ShowError(string message)
    {
        Message = message;
        _dialogs.ShowMessage("Cash Transfer", message);
    }
}
