using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Loads histories by session identity and exposes officer cash-receipt decisions.</summary>
public sealed class SessionDetailViewModel : SessionPageViewModel
{
    private readonly ICashOperationsClientService _client;
    private readonly AuthContext _auth;
    private readonly IDialogService _dialogs;
    private CashPositionSessionResponse? _session;
    public CashPositionSessionResponse? Session { get => _session; private set => SetProperty(ref _session, value); }
    public ObservableCollection<SessionTransactionResponse> Transactions { get; } = [];
    public ObservableCollection<CashMovementHistoryResponse> Movements { get; } = [];
    public ObservableCollection<CashCountHistoryResponse> Counts { get; } = [];
    public ObservableCollection<CashProposalResponse> Requests { get; } = [];
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AcceptCommand { get; }
    public AsyncRelayCommand RejectCommand { get; }
    public AsyncRelayCommand RecountCommand { get; }
    public bool IsOwner => Session?.TellerId == _auth.UserId;
    public SessionDetailViewModel(ICashOperationsClientService client, AuthContext auth, IDialogService dialogs)
    {
        _client = client; _auth = auth; _dialogs = dialogs;
        RefreshCommand = new(() => InitializeAsync(CancellationToken.None));
        AcceptCommand = new(row => RunAsync(async () =>
        {
            var request = (CashProposalResponse)row!;
            await client.ReceiveCashProposalAsync(request.Id, new(request.Version, null), CancellationToken.None);
            await LoadAsync(CancellationToken.None);
        }), row => !IsBusy && IsOwner && row is CashProposalResponse { Status: "PendingReceipt" });
        RejectCommand = new(row => RunAsync(async () =>
        {
            var request = (CashProposalResponse)row!;
            await client.RejectCashProposalAsync(request.Id, new(request.Version, "Officer rejected cash receipt"), CancellationToken.None);
            await LoadAsync(CancellationToken.None);
        }), row => !IsBusy && IsOwner && row is CashProposalResponse { Status: "PendingReceipt" });
        RecountCommand = new(() => RunAsync(async () =>
        {
            var dialog = new CloseSessionDialogViewModel(client, Session!);
            await dialog.InitializeAsync(CancellationToken.None);
            if (dialogs.ShowDialog(dialog)) await LoadAsync(CancellationToken.None);
        }), () => !IsBusy && Session?.Status == "Closed" && (IsOwner || Session.PositionType == "Vault" && auth.HasPermission(PermissionCodes.EndOfDayApproval)));
    }
    protected override void OnBusyChanged()
    {
        AcceptCommand?.RaiseCanExecuteChanged();
        RejectCommand?.RaiseCanExecuteChanged();
        RecountCommand?.RaiseCanExecuteChanged();
    }
    public override Task InitializeAsync(CancellationToken cancellationToken) => RunAsync(() => LoadAsync(cancellationToken));
    // Keeps every history scoped to the same authorized server detail response.
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (SessionId is null) return;
        var detail = await _client.GetSessionDetailAsync(SessionId.Value, cancellationToken);
        Session = detail.Session; OnPropertyChanged(nameof(IsOwner));
        Transactions.Clear(); foreach (var row in detail.Transactions ?? []) Transactions.Add(row);
        Movements.Clear(); foreach (var row in detail.Movements) Movements.Add(row);
        Counts.Clear(); foreach (var row in detail.Counts) Counts.Add(row);
        Requests.Clear(); foreach (var row in detail.Requests ?? []) Requests.Add(row);
        AcceptCommand.RaiseCanExecuteChanged(); RejectCommand.RaiseCanExecuteChanged(); RecountCommand.RaiseCanExecuteChanged();
    }
}
