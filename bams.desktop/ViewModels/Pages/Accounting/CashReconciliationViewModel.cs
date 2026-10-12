using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Constants;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Presentation state for teller and vault cash sessions and physical counts.</summary>
public sealed class CashReconciliationViewModel : ViewModelBase, IAsyncInitializable
{
    private enum CashSessionTab { Sessions, CloseSession, SessionHistory, CashHandoffs, Adjustments }

    private readonly ICashOperationsClientService _service;
    private readonly IEndOfDayClientService _businessDateService;
    private string _positionType = "Teller";
    private string _openingCashText = "0";
    private string _actualCashText = string.Empty;
    private string _transferAmountText = string.Empty;
    private string _errorMessage = string.Empty;
    private CashPositionSessionResponse? _selectedSession;
    private CashPositionSessionResponse? _destinationSession;
    private DateOnly? _businessDateFilter;
    private DateTime? _selectedBusinessDate;
    private DateTime? _activeBusinessDate;
    private bool _isBusy;
    private readonly AuthContext _authContext;
    private readonly OfficerCashSessionContext _officerCashSessionContext;
    private CashSessionTab _selectedTab = CashSessionTab.Sessions;
    private string _adjustmentAmountText = string.Empty;
    private string _correctionTransactionIdText = string.Empty;
    private string? _adjustmentNote;
    private CashAdjustmentResponse? _selectedAdjustment;
    private CashPositionSessionDetailResponse? _sessionDetail;
    private string _countNotes = string.Empty;
    private string _transferNote = string.Empty;
    private string _openingCashError = string.Empty;
    private string _actualCashError = string.Empty;
    private string _countNotesError = string.Empty;
    private string _handoffRecipientError = string.Empty;
    private CashHandoffRecipientResponse? _selectedHandoffRecipient;
    private string _openSessionIdempotencyKey = Guid.NewGuid().ToString("N");
    private string _countIdempotencyKey = Guid.NewGuid().ToString("N");
    private CashHandoffResponse? _selectedHandoff;
    private string _handoffNote = string.Empty;
    private string _transferAmountError = string.Empty;
    private string _transferNoteError = string.Empty;
    private string _adjustmentAmountError = string.Empty;
    private string _correctionTransactionIdError = string.Empty;
    private string _adjustmentNoteError = string.Empty;
    private string _adjustmentRejectionReason = string.Empty;

    public ObservableCollection<CashPositionSessionResponse> Sessions { get; } = [];
    public ObservableCollection<CashHandoffRecipientResponse> HandoffRecipients { get; } = [];
    public ObservableCollection<CashHandoffResponse> Handoffs { get; } = [];
    public ObservableCollection<CashAdjustmentResponse> Adjustments { get; } = [];
    public ObservableCollection<CashMovementHistoryResponse> Movements { get; } = [];
    public ObservableCollection<CashCountHistoryResponse> Counts { get; } = [];
    public IReadOnlyList<string> PositionTypes { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand OpenSessionCommand { get; }
    public AsyncRelayCommand CountCashCommand { get; }
    public AsyncRelayCommand TransferCashCommand { get; }
    public AsyncRelayCommand RequestAdjustmentCommand { get; }
    public AsyncRelayCommand ApproveAdjustmentCommand { get; }
    public AsyncRelayCommand RejectAdjustmentCommand { get; }
    public AsyncRelayCommand ViewSessionHistoryCommand { get; }
    public AsyncRelayCommand AcceptHandoffCommand { get; }
    public AsyncRelayCommand DeclineHandoffCommand { get; }
    public AsyncRelayCommand ReassignHandoffCommand { get; }
    public bool IsSessionsTabSelected { get => _selectedTab == CashSessionTab.Sessions; set { if (value) SelectTab(CashSessionTab.Sessions); } }
    public bool IsCloseSessionTabSelected { get => _selectedTab == CashSessionTab.CloseSession; set { if (value) SelectTab(CashSessionTab.CloseSession); } }
    public bool IsSessionHistoryTabSelected { get => _selectedTab == CashSessionTab.SessionHistory; set { if (value) SelectTab(CashSessionTab.SessionHistory); } }
    public bool IsCashHandoffsTabSelected { get => _selectedTab == CashSessionTab.CashHandoffs; set { if (value) SelectTab(CashSessionTab.CashHandoffs); } }
    public bool IsAdjustmentsTabSelected { get => _selectedTab == CashSessionTab.Adjustments; set { if (value) SelectTab(CashSessionTab.Adjustments); } }

    public CashReconciliationViewModel(ICashOperationsClientService service, IEndOfDayClientService businessDateService,
        AuthContext authContext, OfficerCashSessionContext officerCashSessionContext)
    {
        _service = service;
        _businessDateService = businessDateService;
        _authContext = authContext;
        _officerCashSessionContext = officerCashSessionContext;
        PositionTypes = authContext.HasPermission(PermissionCodes.EndOfDayApproval)
            ? ["Teller", "Vault"]
            : ["Teller"];
        OpenSessionCommand = new AsyncRelayCommand(OpenSessionAsync, () => HasCashOperationsPermission);
        CountCashCommand = new AsyncRelayCommand(SubmitCountAsync, () => HasCashOperationsPermission);
        TransferCashCommand = new AsyncRelayCommand(TransferCashAsync, () => HasCashOperationsPermission);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(), () => CanViewCashSessions);
        RequestAdjustmentCommand = new AsyncRelayCommand(RequestAdjustmentAsync, () => HasCashOperationsPermission);
        ApproveAdjustmentCommand = new AsyncRelayCommand(ApproveAdjustmentAsync, () => CanApproveAdjustments);
        RejectAdjustmentCommand = new AsyncRelayCommand(RejectAdjustmentAsync, () => CanApproveAdjustments && SelectedAdjustment?.Status == "PendingApproval");
        ViewSessionHistoryCommand = new AsyncRelayCommand(LoadSessionHistoryAsync, () => CanViewCashSessions);
        AcceptHandoffCommand = new AsyncRelayCommand(AcceptHandoffAsync, () => CanReviewHandoffs && SelectedHandoff?.Status == "PendingAcceptance");
        DeclineHandoffCommand = new AsyncRelayCommand(DeclineHandoffAsync, () => CanReviewHandoffs && SelectedHandoff?.Status == "PendingAcceptance");
        ReassignHandoffCommand = new AsyncRelayCommand(ReassignHandoffAsync, () => CanReviewHandoffs && SelectedHandoff is not null && SelectedHandoff.Status != "Accepted" && SelectedHandoffRecipient is not null);
    }

    // Selects the active cash-operation tab and notifies the four themed tab buttons of the new selection.
    private void SelectTab(CashSessionTab tab)
    {
        if (_selectedTab == tab) return;
        _selectedTab = tab;
        OnPropertyChanged(nameof(IsSessionsTabSelected));
        OnPropertyChanged(nameof(IsCloseSessionTabSelected));
        OnPropertyChanged(nameof(IsSessionHistoryTabSelected));
        OnPropertyChanged(nameof(IsCashHandoffsTabSelected));
        OnPropertyChanged(nameof(IsAdjustmentsTabSelected));
    }

    public string PositionType { get => _positionType; set { if (SetProperty(ref _positionType, value)) _openSessionIdempotencyKey = Guid.NewGuid().ToString("N"); } }
    public string OpeningCashText { get => _openingCashText; set { if (SetProperty(ref _openingCashText, value)) { OpeningCashError = string.Empty; _openSessionIdempotencyKey = Guid.NewGuid().ToString("N"); } } }
    public string ActualCashText { get => _actualCashText; set { if (SetProperty(ref _actualCashText, value)) { ActualCashError = string.Empty; _countIdempotencyKey = Guid.NewGuid().ToString("N"); } } }
    public string TransferAmountText { get => _transferAmountText; set { if (SetProperty(ref _transferAmountText, value)) TransferAmountError = string.Empty; } }
    public string ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool HasCashOperationsPermission => _authContext.HasPermission(PermissionCodes.CashOperations);
    public bool CanViewCashSessions => HasCashOperationsPermission || _authContext.HasPermission(PermissionCodes.Audit) ||
        _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool CanReviewHandoffs => CanViewCashSessions;
    public bool CanApproveAdjustments => HasCashOperationsPermission && _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public string AdjustmentAmountText { get => _adjustmentAmountText; set { if (SetProperty(ref _adjustmentAmountText, value)) AdjustmentAmountError = string.Empty; } }
    public string CorrectionTransactionIdText { get => _correctionTransactionIdText; set { if (SetProperty(ref _correctionTransactionIdText, value)) CorrectionTransactionIdError = string.Empty; } }
    public string? AdjustmentNote { get => _adjustmentNote; set { if (SetProperty(ref _adjustmentNote, value)) AdjustmentNoteError = string.Empty; } }
    public CashAdjustmentResponse? SelectedAdjustment
    {
        get => _selectedAdjustment;
        set { if (SetProperty(ref _selectedAdjustment, value)) RejectAdjustmentCommand.RaiseCanExecuteChanged(); }
    }
    public string AdjustmentRejectionReason { get => _adjustmentRejectionReason; set => SetProperty(ref _adjustmentRejectionReason, value); }
    public CashPositionSessionDetailResponse? SessionDetail { get => _sessionDetail; private set => SetProperty(ref _sessionDetail, value); }
    public string CountNotes { get => _countNotes; set { if (SetProperty(ref _countNotes, value)) { CountNotesError = string.Empty; _countIdempotencyKey = Guid.NewGuid().ToString("N"); } } }
    public string TransferNote { get => _transferNote; set { if (SetProperty(ref _transferNote, value)) TransferNoteError = string.Empty; } }
    public string OpeningCashError { get => _openingCashError; private set => SetProperty(ref _openingCashError, value); }
    public string ActualCashError { get => _actualCashError; private set => SetProperty(ref _actualCashError, value); }
    public string CountNotesError { get => _countNotesError; private set => SetProperty(ref _countNotesError, value); }
    public string HandoffRecipientError { get => _handoffRecipientError; private set => SetProperty(ref _handoffRecipientError, value); }
    public CashHandoffRecipientResponse? SelectedHandoffRecipient
    {
        get => _selectedHandoffRecipient;
        set { if (SetProperty(ref _selectedHandoffRecipient, value)) { ReassignHandoffCommand.RaiseCanExecuteChanged(); _countIdempotencyKey = Guid.NewGuid().ToString("N"); } }
    }
    public CashHandoffResponse? SelectedHandoff
    {
        get => _selectedHandoff;
        set { if (SetProperty(ref _selectedHandoff, value)) { AcceptHandoffCommand.RaiseCanExecuteChanged(); DeclineHandoffCommand.RaiseCanExecuteChanged(); ReassignHandoffCommand.RaiseCanExecuteChanged(); } }
    }
    public string HandoffNote { get => _handoffNote; set => SetProperty(ref _handoffNote, value); }
    public string TransferAmountError { get => _transferAmountError; private set => SetProperty(ref _transferAmountError, value); }
    public string TransferNoteError { get => _transferNoteError; private set => SetProperty(ref _transferNoteError, value); }
    public string AdjustmentAmountError { get => _adjustmentAmountError; private set => SetProperty(ref _adjustmentAmountError, value); }
    public string CorrectionTransactionIdError { get => _correctionTransactionIdError; private set => SetProperty(ref _correctionTransactionIdError, value); }
    public string AdjustmentNoteError { get => _adjustmentNoteError; private set => SetProperty(ref _adjustmentNoteError, value); }
    public CashPositionSessionResponse? SelectedSession
    {
        get => _selectedSession;
        set
        {
            var previousId = _selectedSession?.Id;
            if (SetProperty(ref _selectedSession, value) && previousId != value?.Id)
                _countIdempotencyKey = Guid.NewGuid().ToString("N");
        }
    }
    public CashPositionSessionResponse? DestinationSession { get => _destinationSession; set => SetProperty(ref _destinationSession, value); }
    public DateTime? SelectedBusinessDate
    {
        get => _selectedBusinessDate;
        set
        {
            if (SetProperty(ref _selectedBusinessDate, value))
                _businessDateFilter = value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
        }
    }
    public DateTime? ActiveBusinessDate { get => _activeBusinessDate; private set => SetProperty(ref _activeBusinessDate, value); }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!CanViewCashSessions) return;
        try
        {
            var activeBusinessDate = await _businessDateService.GetCurrentBusinessDateAsync(cancellationToken);
            ActiveBusinessDate = activeBusinessDate.Date.ToDateTime(TimeOnly.MinValue);
            SelectedBusinessDate = ActiveBusinessDate;
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
            return;
        }

        await RefreshAsync(cancellationToken);
    }

    /// <summary>Loads cash sessions for the business date selected by the parent End of Day workflow.</summary>
    public Task InitializeForBusinessDateAsync(DateOnly businessDate, CancellationToken cancellationToken)
    {
        if (!CanViewCashSessions) return Task.CompletedTask;
        SelectedBusinessDate = businessDate.ToDateTime(TimeOnly.MinValue);
        return RefreshAsync(cancellationToken);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!CanViewCashSessions) return;
        try
        {
            IsBusy = true;
            var sessions = await _service.GetSessionsAsync(_businessDateFilter, cancellationToken);
            if (string.Equals(_authContext.Role, "officer", StringComparison.OrdinalIgnoreCase))
            {
                var currentBusinessDate = await _businessDateService.GetCurrentBusinessDateAsync(cancellationToken);
                if (_businessDateFilter == currentBusinessDate.Date)
                    _officerCashSessionContext.SetStatus(sessions.Any(session =>
                        session.PositionType == "Teller" && session.Status == "Open" && session.TellerId == _authContext.UserId));
            }
            var selectedSessionId = SelectedSession?.Id;
            var destinationSessionId = DestinationSession?.Id;
            Sessions.Clear();
            foreach (var item in sessions) Sessions.Add(item);
            if (!_businessDateFilter.HasValue && Sessions.Count > 0)
                SelectedBusinessDate = Sessions[0].BusinessDate.ToDateTime(TimeOnly.MinValue);
            SelectedSession = Sessions.FirstOrDefault(item => item.Id == selectedSessionId)
                ?? Sessions.FirstOrDefault(item => item.Status == "Open")
                ?? Sessions.FirstOrDefault();
            DestinationSession = Sessions.FirstOrDefault(item => item.Id == destinationSessionId && item.Id != SelectedSession?.Id)
                ?? Sessions.FirstOrDefault(item => item.Status == "Open" && item.Id != SelectedSession?.Id);
            if (CanApproveAdjustments)
            {
                var adjustments = await _service.GetAdjustmentsAsync(_businessDateFilter, null, cancellationToken);
                Adjustments.Clear();
                foreach (var adjustment in adjustments) Adjustments.Add(adjustment);
            }
            var recipients = await _service.GetHandoffRecipientsAsync(cancellationToken);
            HandoffRecipients.Clear();
            foreach (var recipient in recipients) HandoffRecipients.Add(recipient);
            var handoffs = await _service.GetCashHandoffsAsync(_businessDateFilter, cancellationToken);
            Handoffs.Clear();
            foreach (var handoff in handoffs) Handoffs.Add(handoff);
            SelectedHandoff = Handoffs.FirstOrDefault(item => item.Id == SelectedHandoff?.Id) ?? Handoffs.FirstOrDefault();
            ErrorMessage = string.Empty;
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task OpenSessionAsync()
    {
        if (!HasCashOperationsPermission || (PositionType == "Vault" && !CanApproveAdjustments)) return;
        OpeningCashError = !decimal.TryParse(OpeningCashText, out var opening) || opening < 0m || !HasTwoDecimalPlaces(opening)
            ? "Enter opening cash of 0 or more, using no more than 2 decimal places." : string.Empty;
        if (OpeningCashError.Length > 0) return;
        try
        {
            IsBusy = true;
            var activeBusinessDate = await _businessDateService.GetCurrentBusinessDateAsync(CancellationToken.None);
            ActiveBusinessDate = activeBusinessDate.Date.ToDateTime(TimeOnly.MinValue);
            await _service.OpenSessionAsync(new OpenCashSessionRequest(PositionType, opening), _openSessionIdempotencyKey, CancellationToken.None);
            SelectedBusinessDate = ActiveBusinessDate;
            if (PositionType == "Teller" && string.Equals(_authContext.Role, "officer", StringComparison.OrdinalIgnoreCase))
                _officerCashSessionContext.SetStatus(true);
            _openSessionIdempotencyKey = Guid.NewGuid().ToString("N");
            await RefreshAsync();
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task SubmitCountAsync()
    {
        if (!HasCashOperationsPermission) return;
        var sessionId = SelectedSession?.Id ?? 0;
        var sessionIdError = sessionId <= 0 ? "Select a cash session from the list above." : string.Empty;
        ActualCashError = !decimal.TryParse(ActualCashText, out var actual) || actual < 0m || !HasTwoDecimalPlaces(actual)
            ? "Enter a physical count of 0 or more, using no more than 2 decimal places." : string.Empty;
        CountNotesError = CountNotes.Length > 2000 ? "Count notes must be 2,000 characters or fewer." : string.Empty;
        HandoffRecipientError = SelectedSession?.Status == "Open" && decimal.TryParse(ActualCashText, out var parsedActual) && parsedActual > 0m && SelectedHandoffRecipient is null
            ? "Select the teller or manager who will receive the counted cash." : string.Empty;
        if (sessionIdError.Length > 0 || ActualCashError.Length > 0 || CountNotesError.Length > 0 || HandoffRecipientError.Length > 0) { ErrorMessage = sessionIdError; return; }
        try
        {
            IsBusy = true;
            var closingSession = SelectedSession?.Status == "Open";
            var recipientName = SelectedHandoffRecipient?.FullName;
            var count = await _service.SubmitCountAsync(sessionId,
                new SubmitCashCountRequest(actual, CountNotes, SelectedHandoffRecipient?.UserId, SelectedSession!.Version),
                _countIdempotencyKey, CancellationToken.None);
            _countIdempotencyKey = Guid.NewGuid().ToString("N");
            SelectedHandoffRecipient = null;
            await RefreshAsync();
            ErrorMessage = count.HandoffId.HasValue
                ? $"Session closed. Cash handoff sent to {recipientName} for receipt."
                : closingSession ? $"Session closed. Count {count.Status}: difference {count.Difference:N2}."
                : $"Recount recorded. Count {count.Status}: difference {count.Difference:N2}.";
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task AcceptHandoffAsync()
    {
        if (!CanReviewHandoffs || SelectedHandoff is null) return;
        try { await _service.AcceptCashHandoffAsync(SelectedHandoff.Id, new CashHandoffActionRequest(HandoffNote, SelectedHandoff.Version), CancellationToken.None); await RefreshAsync(); ErrorMessage = "Cash receipt acknowledged."; }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }

    private async Task DeclineHandoffAsync()
    {
        if (!CanReviewHandoffs || SelectedHandoff is null) return;
        if (string.IsNullOrWhiteSpace(HandoffNote)) { ErrorMessage = "Enter a note to decline the cash handoff."; return; }
        try { await _service.DeclineCashHandoffAsync(SelectedHandoff.Id, new CashHandoffActionRequest(HandoffNote, SelectedHandoff.Version), CancellationToken.None); await RefreshAsync(); ErrorMessage = "Handoff declined; it remains an End of Day blocker."; }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }

    private async Task ReassignHandoffAsync()
    {
        if (!CanReviewHandoffs || SelectedHandoff is null || SelectedHandoffRecipient is null) return;
        try { await _service.ReassignCashHandoffAsync(SelectedHandoff.Id, new ReassignCashHandoffRequest(SelectedHandoffRecipient.UserId, HandoffNote, SelectedHandoff.Version), CancellationToken.None); await RefreshAsync(); ErrorMessage = "Handoff reassigned and awaiting recipient acknowledgement."; }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }

    private async Task TransferCashAsync()
    {
        if (!HasCashOperationsPermission) return;
        var sourceId = SelectedSession?.Id ?? 0;
        var destinationId = DestinationSession?.Id ?? 0;
        var sessionSelectionError = sourceId <= 0 || destinationId <= 0
            ? "Select both the source session in the table and a destination session." : string.Empty;
        TransferAmountError = !decimal.TryParse(TransferAmountText, out var amount) || amount <= 0m || !HasTwoDecimalPlaces(amount)
            ? "Enter a transfer amount greater than 0, using no more than 2 decimal places." : string.Empty;
        TransferNoteError = TransferNote.Length > 500 ? "Transfer notes must be 500 characters or fewer." : string.Empty;
        if (sessionSelectionError.Length > 0 || TransferAmountError.Length > 0 || TransferNoteError.Length > 0) { ErrorMessage = sessionSelectionError; return; }
        try { IsBusy = true; await _service.TransferCashAsync(sourceId, new TransferCashRequest(destinationId, amount, TransferNote), CancellationToken.None); await RefreshAsync(); ErrorMessage = string.Empty; }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task LoadSessionHistoryAsync()
    {
        if (!CanViewCashSessions) return;
        var sessionId = SelectedSession?.Id ?? 0;
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
        if (!HasCashOperationsPermission) return;
        var sessionId = SelectedSession?.Id ?? 0;
        AdjustmentAmountError = !decimal.TryParse(AdjustmentAmountText, out var amount) || amount == 0m || !HasTwoDecimalPlaces(amount)
            ? "Enter a non-zero signed amount, using no more than 2 decimal places." : string.Empty;
        CorrectionTransactionIdError = !long.TryParse(CorrectionTransactionIdText, out var correctionId) || correctionId <= 0
            ? "Enter a positive posted correction transaction ID." : string.Empty;
        AdjustmentNoteError = (AdjustmentNote?.Length ?? 0) > 500 ? "Adjustment notes must be 500 characters or fewer." : string.Empty;
        if (sessionId <= 0 || AdjustmentAmountError.Length > 0 || CorrectionTransactionIdError.Length > 0 || AdjustmentNoteError.Length > 0)
        {
            ErrorMessage = sessionId <= 0 ? "Select a cash session from the list above." : string.Empty;
            return;
        }
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
        if (!HasCashOperationsPermission || !CanApproveAdjustments || SelectedAdjustment is null) return;
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

    private async Task RejectAdjustmentAsync()
    {
        if (!CanApproveAdjustments || SelectedAdjustment is null || string.IsNullOrWhiteSpace(AdjustmentRejectionReason))
        {
            ErrorMessage = "Select a pending request and enter a rejection reason.";
            return;
        }
        try
        {
            IsBusy = true;
            await _service.RejectAdjustmentAsync(SelectedAdjustment.Id,
                new RejectCashAdjustmentRequest(AdjustmentRejectionReason.Trim()), CancellationToken.None);
            await RefreshAsync();
            ErrorMessage = string.Empty;
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private static bool HasTwoDecimalPlaces(decimal value) => decimal.Round(value, 2) == value;
}
