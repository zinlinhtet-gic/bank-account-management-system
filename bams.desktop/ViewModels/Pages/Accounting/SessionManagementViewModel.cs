using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Role-aware session register; financial eligibility remains server-owned.</summary>
public sealed class SessionManagementViewModel : SessionPageViewModel
{
    private readonly ICashOperationsClientService _client;
    private readonly IEndOfDayClientService _dates;
    private readonly AuthContext _auth;
    private readonly IDialogService _dialogs;
    private readonly OfficerCashSessionContext _context;
    private DateTime _date;
    private DateOnly _today;
    public DateTime SelectedDate { get => _date; set => SetProperty(ref _date, value); }
    public ObservableCollection<CashPositionSessionResponse> Sessions { get; } = [];
    public bool IsManager => _auth.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool IsOfficer => string.Equals(_auth.Role, "officer", StringComparison.OrdinalIgnoreCase);
    public RelayCommand CreateCommand { get; }
    public RelayCommand DetailCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AcceptCommand { get; }
    public AsyncRelayCommand RequestCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public SessionManagementViewModel(ICashOperationsClientService client, IEndOfDayClientService dates,
        AuthContext auth, IDialogService dialogs, OfficerCashSessionContext context)
    {
        _client = client; _dates = dates; _auth = auth; _dialogs = dialogs; _context = context;
        CreateCommand = new(_ => Navigate(PageNames.SessionCreation), _ => IsManager && !IsBusy);
        DetailCommand = new(row => { if (row is CashPositionSessionResponse session) Navigate(PageNames.SessionDetail, session.Id); });
        RefreshCommand = new(() => RunAsync(() => LoadAsync(CancellationToken.None)));
        AcceptCommand = new(row => RunAsync(async () =>
        {
            var session = (CashPositionSessionResponse)row!;
            await _client.ActivateSessionAsync(session.Id, new(session.Version), CancellationToken.None);
            _context.SetStatus(true);
            await LoadAsync(CancellationToken.None);
        }), row => !IsBusy && IsOfficer && row is CashPositionSessionResponse { Status: "Scheduled" } session && session.BusinessDate == _today && session.CanAccept);
        RequestCommand = new(row => RunAsync(async () =>
        {
            var session = (CashPositionSessionResponse)row!;
            _dialogs.ShowDialog(new CashRequestDialogViewModel(_client, session.Id, $"Session #{session.Id}"));
            await LoadAsync(CancellationToken.None);
        }), row => !IsBusy && IsOfficer && row is CashPositionSessionResponse { Status: "Open" } session && session.BusinessDate == _today && session.CanRequestCash);
        CloseCommand = new(row => RunAsync(async () =>
        {
            var session = (CashPositionSessionResponse)row!;
            var dialog = new CloseSessionDialogViewModel(_client, session);
            await dialog.InitializeAsync(CancellationToken.None);
            if (_dialogs.ShowDialog(dialog)) await LoadAsync(CancellationToken.None);
        }), row => !IsBusy && IsOfficer && row is CashPositionSessionResponse { Status: "Open", CanClose: true });
        CancelCommand = new(row => RunAsync(async () =>
        {
            await _client.CancelScheduledSessionAsync(((CashPositionSessionResponse)row!).Id, CancellationToken.None);
            await LoadAsync(CancellationToken.None);
        }), row => !IsBusy && IsManager && row is CashPositionSessionResponse { Status: "Scheduled", ActivatedAtUtc: null, CanCancel: true });
    }

    protected override void OnBusyChanged()
    {
        CreateCommand?.RaiseCanExecuteChanged();
        AcceptCommand?.RaiseCanExecuteChanged();
        RequestCommand?.RaiseCanExecuteChanged();
        CloseCommand?.RaiseCanExecuteChanged();
        CancelCommand?.RaiseCanExecuteChanged();
    }
    public override Task InitializeAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        var day = await _dates.GetCurrentBusinessDateAsync(cancellationToken);
        _today = day.Today ?? day.Date;
        SelectedDate = _today.ToDateTime(TimeOnly.MinValue);
        await LoadAsync(cancellationToken);
    });
    // Reloads the filtered register and refreshes the current officer's transaction gate independently of the filter.
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Sessions.Clear();
        foreach (var item in await _client.GetSessionsAsync(DateOnly.FromDateTime(SelectedDate), cancellationToken)) Sessions.Add(item);
        if (IsOfficer)
        {
            var todaySessions = await _client.GetSessionsAsync(_today, cancellationToken);
            _context.SetStatus(todaySessions.Any(item => item.Status == "Open" && item.TellerId == _auth.UserId));
        }
        AcceptCommand.RaiseCanExecuteChanged(); RequestCommand.RaiseCanExecuteChanged(); CloseCommand.RaiseCanExecuteChanged(); CancelCommand.RaiseCanExecuteChanged();
    }
}
