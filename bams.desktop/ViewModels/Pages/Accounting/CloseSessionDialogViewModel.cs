using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Collects count and eligible handover recipient before one atomic close request.</summary>
public sealed class CloseSessionDialogViewModel : ViewModelBase, IDialogViewModel, IAsyncInitializable
{
    private readonly ICashOperationsClientService _client;
    private readonly CashPositionSessionResponse _session;
    private readonly string _key = Guid.NewGuid().ToString("N");
    private string _amount = "", _notes = "", _error = "";
    private bool _busy;
    private CashHandoffRecipientResponse? _recipient;
    public event Action<bool>? CloseRequested;
    public bool CanCloseOnBackdropClick => false;
    public bool CanCancel => !_busy;
    public string Title => $"Close Session #{_session.Id}";
    public decimal ExpectedCash => _session.ExpectedClosingCash;
    public string Amount { get => _amount; set => SetProperty(ref _amount, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public bams.desktop.Utils.MessageCode? ErrorCode => string.IsNullOrEmpty(Error) ? null : bams.desktop.Utils.MessageCode.ValidationFailed;
    public string Error { get => _error; private set { SetProperty(ref _error, value); OnPropertyChanged(nameof(ErrorCode)); } }
    public ObservableCollection<CashHandoffRecipientResponse> Recipients { get; } = [];
    public CashHandoffRecipientResponse? Recipient { get => _recipient; set => SetProperty(ref _recipient, value); }
    public AsyncRelayCommand SubmitCommand { get; }
    public RelayCommand CancelCommand { get; }
    public CloseSessionDialogViewModel(ICashOperationsClientService client, CashPositionSessionResponse session)
    {
        _client = client; _session = session;
        SubmitCommand = new(SubmitAsync, () => !_busy);
        CancelCommand = new(_ => CloseRequested?.Invoke(false), _ => !_busy);
    }
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        foreach (var recipient in await _client.GetHandoffRecipientsAsync(cancellationToken, _session.Id)) Recipients.Add(recipient);
    }
    // The server revalidates recipient sessions and writes count, closure and handoff together.
    private async Task SubmitAsync()
    {
        if (!decimal.TryParse(Amount, out var cash) || cash < 0 || decimal.Round(cash, 2) != cash)
        { Error = "Enter non-negative actual cash with at most two decimal places."; return; }
        if (_session.Status == "Open" && cash > 0 && Recipient is null) { Error = "Select a handover recipient."; return; }
        try
        {
            _busy = true; CancelCommand.RaiseCanExecuteChanged();
            await _client.SubmitCountAsync(_session.Id, new(cash, Notes, cash > 0 && _session.Status == "Open" ? Recipient?.UserId : null, _session.Version), _key, CancellationToken.None);
            CloseRequested?.Invoke(true);
        }
        catch (AppException error) { Error = error.Message; }
        finally { _busy = false; CancelCommand.RaiseCanExecuteChanged(); }
    }
}
