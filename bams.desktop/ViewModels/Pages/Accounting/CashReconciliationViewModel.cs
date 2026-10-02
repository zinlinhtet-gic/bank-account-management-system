using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Constants;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Presentation state for branch teller and vault cash sessions and physical counts.</summary>
public sealed class CashReconciliationViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ICashOperationsClientService _service;
    private string _branchIdText = string.Empty;
    private string _positionType = "Teller";
    private string _openingCashText = "0";
    private string _selectedSessionIdText = string.Empty;
    private string _actualCashText = string.Empty;
    private string _destinationSessionIdText = string.Empty;
    private string _transferAmountText = string.Empty;
    private string _errorMessage = string.Empty;
    private CashPositionSessionResponse? _selectedSession;
    private bool _isBusy;
    private readonly AuthContext _authContext;
    private string _adjustmentAmountText = string.Empty;
    private string _correctionTransactionIdText = string.Empty;
    private string? _adjustmentNote;
    private CashAdjustmentResponse? _selectedAdjustment;
    private CashPositionSessionDetailResponse? _sessionDetail;
    private string _countNotes = string.Empty;
    private string _transferNote = string.Empty;
    private string _branchIdError = string.Empty;
    private string _openingCashError = string.Empty;
    private string _sessionIdError = string.Empty;
    private string _actualCashError = string.Empty;
    private string _countNotesError = string.Empty;
    private string _destinationSessionIdError = string.Empty;
    private string _transferAmountError = string.Empty;
    private string _transferNoteError = string.Empty;
    private string _adjustmentAmountError = string.Empty;
    private string _correctionTransactionIdError = string.Empty;
    private string _adjustmentNoteError = string.Empty;

    public ObservableCollection<CashPositionSessionResponse> Sessions { get; } = [];
    public ObservableCollection<CashAdjustmentResponse> Adjustments { get; } = [];
    public ObservableCollection<CashMovementHistoryResponse> Movements { get; } = [];
    public ObservableCollection<CashCountHistoryResponse> Counts { get; } = [];
    public IReadOnlyList<string> PositionTypes { get; } = ["Teller", "Vault"];
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand OpenSessionCommand { get; }
    public AsyncRelayCommand CountCashCommand { get; }
    public AsyncRelayCommand TransferCashCommand { get; }
    public AsyncRelayCommand RequestAdjustmentCommand { get; }
    public AsyncRelayCommand ApproveAdjustmentCommand { get; }
    public AsyncRelayCommand ViewSessionHistoryCommand { get; }

    public CashReconciliationViewModel(ICashOperationsClientService service, AuthContext authContext)
    {
        _service = service;
        _authContext = authContext;
        OpenSessionCommand = new AsyncRelayCommand(async _ => await OpenSessionAsync());
        CountCashCommand = new AsyncRelayCommand(async _ => await SubmitCountAsync());
        TransferCashCommand = new AsyncRelayCommand(async _ => await TransferCashAsync());
        RefreshCommand = new AsyncRelayCommand(async _ => await RefreshAsync());
        RequestAdjustmentCommand = new AsyncRelayCommand(async _ => await RequestAdjustmentAsync());
        ApproveAdjustmentCommand = new AsyncRelayCommand(async _ => await ApproveAdjustmentAsync());
        ViewSessionHistoryCommand = new AsyncRelayCommand(async _ => await LoadSessionHistoryAsync());
    }

    public string BranchIdText { get => _branchIdText; set { if (SetProperty(ref _branchIdText, value)) BranchIdError = string.Empty; } }
    public string PositionType { get => _positionType; set => SetProperty(ref _positionType, value); }
    public string OpeningCashText { get => _openingCashText; set { if (SetProperty(ref _openingCashText, value)) OpeningCashError = string.Empty; } }
    public string SelectedSessionIdText { get => _selectedSessionIdText; set { if (SetProperty(ref _selectedSessionIdText, value)) SessionIdError = string.Empty; } }
    public string ActualCashText { get => _actualCashText; set { if (SetProperty(ref _actualCashText, value)) ActualCashError = string.Empty; } }
    public string DestinationSessionIdText { get => _destinationSessionIdText; set { if (SetProperty(ref _destinationSessionIdText, value)) DestinationSessionIdError = string.Empty; } }
    public string TransferAmountText { get => _transferAmountText; set { if (SetProperty(ref _transferAmountText, value)) TransferAmountError = string.Empty; } }
    public string ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool CanApproveAdjustments => _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public string AdjustmentAmountText { get => _adjustmentAmountText; set { if (SetProperty(ref _adjustmentAmountText, value)) AdjustmentAmountError = string.Empty; } }
    public string CorrectionTransactionIdText { get => _correctionTransactionIdText; set { if (SetProperty(ref _correctionTransactionIdText, value)) CorrectionTransactionIdError = string.Empty; } }
    public string? AdjustmentNote { get => _adjustmentNote; set { if (SetProperty(ref _adjustmentNote, value)) AdjustmentNoteError = string.Empty; } }
    public CashAdjustmentResponse? SelectedAdjustment { get => _selectedAdjustment; set => SetProperty(ref _selectedAdjustment, value); }
    public CashPositionSessionDetailResponse? SessionDetail { get => _sessionDetail; private set => SetProperty(ref _sessionDetail, value); }
    public string CountNotes { get => _countNotes; set { if (SetProperty(ref _countNotes, value)) CountNotesError = string.Empty; } }
    public string TransferNote { get => _transferNote; set { if (SetProperty(ref _transferNote, value)) TransferNoteError = string.Empty; } }
    public string BranchIdError { get => _branchIdError; private set => SetProperty(ref _branchIdError, value); }
    public string OpeningCashError { get => _openingCashError; private set => SetProperty(ref _openingCashError, value); }
    public string SessionIdError { get => _sessionIdError; private set => SetProperty(ref _sessionIdError, value); }
    public string ActualCashError { get => _actualCashError; private set => SetProperty(ref _actualCashError, value); }
    public string CountNotesError { get => _countNotesError; private set => SetProperty(ref _countNotesError, value); }
    public string DestinationSessionIdError { get => _destinationSessionIdError; private set => SetProperty(ref _destinationSessionIdError, value); }
    public string TransferAmountError { get => _transferAmountError; private set => SetProperty(ref _transferAmountError, value); }
    public string TransferNoteError { get => _transferNoteError; private set => SetProperty(ref _transferNoteError, value); }
    public string AdjustmentAmountError { get => _adjustmentAmountError; private set => SetProperty(ref _adjustmentAmountError, value); }
    public string CorrectionTransactionIdError { get => _correctionTransactionIdError; private set => SetProperty(ref _correctionTransactionIdError, value); }
    public string AdjustmentNoteError { get => _adjustmentNoteError; private set => SetProperty(ref _adjustmentNoteError, value); }
    public CashPositionSessionResponse? SelectedSession { get => _selectedSession; set { if (SetProperty(ref _selectedSession, value) && value is not null) SelectedSessionIdText = value.Id.ToString(); } }

    public Task InitializeAsync(CancellationToken cancellationToken) => RefreshAsync(cancellationToken);

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(BranchIdText, out var branchId) || branchId <= 0) branchId = 0;
        try
        {
            IsBusy = true;
            var sessions = await _service.GetSessionsAsync(null, branchId == 0 ? null : branchId, cancellationToken);
            Sessions.Clear();
            foreach (var item in sessions) Sessions.Add(item);
            if (CanApproveAdjustments)
            {
                var adjustments = await _service.GetAdjustmentsAsync(null, branchId == 0 ? null : branchId, null, cancellationToken);
                Adjustments.Clear();
                foreach (var adjustment in adjustments) Adjustments.Add(adjustment);
            }
            ErrorMessage = string.Empty;
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task OpenSessionAsync()
    {
        BranchIdError = !long.TryParse(BranchIdText, out var branchId) || branchId <= 0 ? "Enter a positive branch ID." : string.Empty;
        OpeningCashError = !decimal.TryParse(OpeningCashText, out var opening) || opening < 0m || !HasTwoDecimalPlaces(opening)
            ? "Enter opening cash of 0 or more, using no more than 2 decimal places." : string.Empty;
        if (BranchIdError.Length > 0 || OpeningCashError.Length > 0) return;
        try
        {
            IsBusy = true;
            await _service.OpenSessionAsync(new OpenCashSessionRequest(branchId, PositionType, opening), CancellationToken.None);
            await RefreshAsync();
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task SubmitCountAsync()
    {
        SessionIdError = !long.TryParse(SelectedSessionIdText, out var sessionId) || sessionId <= 0 ? "Enter a valid positive session ID." : string.Empty;
        ActualCashError = !decimal.TryParse(ActualCashText, out var actual) || actual < 0m || !HasTwoDecimalPlaces(actual)
            ? "Enter a physical count of 0 or more, using no more than 2 decimal places." : string.Empty;
        CountNotesError = CountNotes.Length > 2000 ? "Count notes must be 2,000 characters or fewer." : string.Empty;
        if (SessionIdError.Length > 0 || ActualCashError.Length > 0 || CountNotesError.Length > 0) return;
        try { IsBusy = true; var count = await _service.SubmitCountAsync(sessionId, new SubmitCashCountRequest(actual, CountNotes), CancellationToken.None); ErrorMessage = $"Count {count.Status}: difference {count.Difference:N2}."; await RefreshAsync(); }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task TransferCashAsync()
    {
        SessionIdError = !long.TryParse(SelectedSessionIdText, out var sourceId) || sourceId <= 0 ? "Enter a valid positive source session ID." : string.Empty;
        DestinationSessionIdError = !long.TryParse(DestinationSessionIdText, out var destinationId) || destinationId <= 0
            ? "Enter a valid positive destination session ID." : string.Empty;
        TransferAmountError = !decimal.TryParse(TransferAmountText, out var amount) || amount <= 0m || !HasTwoDecimalPlaces(amount)
            ? "Enter a transfer amount greater than 0, using no more than 2 decimal places." : string.Empty;
        TransferNoteError = TransferNote.Length > 500 ? "Transfer notes must be 500 characters or fewer." : string.Empty;
        if (SessionIdError.Length > 0 || DestinationSessionIdError.Length > 0 || TransferAmountError.Length > 0 || TransferNoteError.Length > 0) return;
        try { IsBusy = true; await _service.TransferCashAsync(sourceId, new TransferCashRequest(destinationId, amount, TransferNote), CancellationToken.None); await RefreshAsync(); ErrorMessage = string.Empty; }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task LoadSessionHistoryAsync()
    {
        var sessionId = SelectedSession?.Id ?? (long.TryParse(SelectedSessionIdText, out var parsed) ? parsed : 0);
        if (sessionId <= 0) { ErrorMessage = "Select a cash session to view its history."; return; }
        try
        {
            IsBusy = true;
            SessionDetail = await _service.GetSessionDetailAsync(sessionId, CancellationToken.None);
            Movements.Clear();
            foreach (var movement in SessionDetail.Movements) Movements.Add(movement);
            Counts.Clear();
            foreach (var count in SessionDetail.Counts) Counts.Add(count);
            ErrorMessage = string.Empty;
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task RequestAdjustmentAsync()
    {
        var sessionId = SelectedSession?.Id ?? (long.TryParse(SelectedSessionIdText, out var parsedSessionId) ? parsedSessionId : 0);
        SessionIdError = sessionId <= 0 ? "Select a valid cash session." : string.Empty;
        AdjustmentAmountError = !decimal.TryParse(AdjustmentAmountText, out var amount) || amount == 0m || !HasTwoDecimalPlaces(amount)
            ? "Enter a non-zero signed amount, using no more than 2 decimal places." : string.Empty;
        CorrectionTransactionIdError = !long.TryParse(CorrectionTransactionIdText, out var correctionId) || correctionId <= 0
            ? "Enter a positive posted correction transaction ID." : string.Empty;
        AdjustmentNoteError = (AdjustmentNote?.Length ?? 0) > 500 ? "Adjustment notes must be 500 characters or fewer." : string.Empty;
        if (SessionIdError.Length > 0 || AdjustmentAmountError.Length > 0 || CorrectionTransactionIdError.Length > 0 || AdjustmentNoteError.Length > 0) return;
        try
        {
            IsBusy = true;
            var adjustment = await _service.RequestAdjustmentAsync(sessionId,
                new RequestCashAdjustmentRequest(amount, correctionId, AdjustmentNote), CancellationToken.None);
            ErrorMessage = $"Adjustment {adjustment.Id} submitted for independent approval.";
            await RefreshAsync();
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task ApproveAdjustmentAsync()
    {
        if (!CanApproveAdjustments || SelectedAdjustment is null) return;
        try
        {
            IsBusy = true;
            await _service.ApproveAdjustmentAsync(SelectedAdjustment.Id, CancellationToken.None);
            await RefreshAsync();
            ErrorMessage = string.Empty;
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private static bool HasTwoDecimalPlaces(decimal value) => decimal.Round(value, 2) == value;
}
