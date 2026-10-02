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
    private bool _isBusy;
    private readonly AuthContext _authContext;
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
    private string _transferAmountError = string.Empty;
    private string _transferNoteError = string.Empty;
    private string _adjustmentAmountError = string.Empty;
    private string _correctionTransactionIdError = string.Empty;
    private string _adjustmentNoteError = string.Empty;

    public ObservableCollection<CashPositionSessionResponse> Sessions { get; } = [];
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
    public AsyncRelayCommand ViewSessionHistoryCommand { get; }

    public CashReconciliationViewModel(ICashOperationsClientService service, IEndOfDayClientService businessDateService, AuthContext authContext)
    {
        _service = service;
        _businessDateService = businessDateService;
        _authContext = authContext;
        PositionTypes = authContext.HasPermission(PermissionCodes.EndOfDayApproval)
            ? ["Teller", "Vault"]
            : ["Teller"];
        OpenSessionCommand = new AsyncRelayCommand(OpenSessionAsync, () => HasCashOperationsPermission);
        CountCashCommand = new AsyncRelayCommand(SubmitCountAsync, () => HasCashOperationsPermission);
        TransferCashCommand = new AsyncRelayCommand(TransferCashAsync, () => HasCashOperationsPermission);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(), () => HasCashOperationsPermission);
        RequestAdjustmentCommand = new AsyncRelayCommand(RequestAdjustmentAsync, () => HasCashOperationsPermission);
        ApproveAdjustmentCommand = new AsyncRelayCommand(ApproveAdjustmentAsync, () => CanApproveAdjustments);
        ViewSessionHistoryCommand = new AsyncRelayCommand(LoadSessionHistoryAsync, () => HasCashOperationsPermission);
    }

    public string PositionType { get => _positionType; set => SetProperty(ref _positionType, value); }
    public string OpeningCashText { get => _openingCashText; set { if (SetProperty(ref _openingCashText, value)) OpeningCashError = string.Empty; } }
    public string ActualCashText { get => _actualCashText; set { if (SetProperty(ref _actualCashText, value)) ActualCashError = string.Empty; } }
    public string TransferAmountText { get => _transferAmountText; set { if (SetProperty(ref _transferAmountText, value)) TransferAmountError = string.Empty; } }
    public string ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool HasCashOperationsPermission => _authContext.HasPermission(PermissionCodes.CashOperations);
    public bool CanApproveAdjustments => HasCashOperationsPermission && _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public string AdjustmentAmountText { get => _adjustmentAmountText; set { if (SetProperty(ref _adjustmentAmountText, value)) AdjustmentAmountError = string.Empty; } }
    public string CorrectionTransactionIdText { get => _correctionTransactionIdText; set { if (SetProperty(ref _correctionTransactionIdText, value)) CorrectionTransactionIdError = string.Empty; } }
    public string? AdjustmentNote { get => _adjustmentNote; set { if (SetProperty(ref _adjustmentNote, value)) AdjustmentNoteError = string.Empty; } }
    public CashAdjustmentResponse? SelectedAdjustment { get => _selectedAdjustment; set => SetProperty(ref _selectedAdjustment, value); }
    public CashPositionSessionDetailResponse? SessionDetail { get => _sessionDetail; private set => SetProperty(ref _sessionDetail, value); }
    public string CountNotes { get => _countNotes; set { if (SetProperty(ref _countNotes, value)) CountNotesError = string.Empty; } }
    public string TransferNote { get => _transferNote; set { if (SetProperty(ref _transferNote, value)) TransferNoteError = string.Empty; } }
    public string OpeningCashError { get => _openingCashError; private set => SetProperty(ref _openingCashError, value); }
    public string ActualCashError { get => _actualCashError; private set => SetProperty(ref _actualCashError, value); }
    public string CountNotesError { get => _countNotesError; private set => SetProperty(ref _countNotesError, value); }
    public string TransferAmountError { get => _transferAmountError; private set => SetProperty(ref _transferAmountError, value); }
    public string TransferNoteError { get => _transferNoteError; private set => SetProperty(ref _transferNoteError, value); }
    public string AdjustmentAmountError { get => _adjustmentAmountError; private set => SetProperty(ref _adjustmentAmountError, value); }
    public string CorrectionTransactionIdError { get => _correctionTransactionIdError; private set => SetProperty(ref _correctionTransactionIdError, value); }
    public string AdjustmentNoteError { get => _adjustmentNoteError; private set => SetProperty(ref _adjustmentNoteError, value); }
    public CashPositionSessionResponse? SelectedSession { get => _selectedSession; set => SetProperty(ref _selectedSession, value); }
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

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!HasCashOperationsPermission) return;
        try
        {
            var activeBusinessDate = await _businessDateService.GetCurrentBusinessDateAsync(cancellationToken);
            SelectedBusinessDate = activeBusinessDate.Date.ToDateTime(TimeOnly.MinValue);
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
        if (!HasCashOperationsPermission) return Task.CompletedTask;
        SelectedBusinessDate = businessDate.ToDateTime(TimeOnly.MinValue);
        return RefreshAsync(cancellationToken);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!HasCashOperationsPermission) return;
        try
        {
            IsBusy = true;
            var sessions = await _service.GetSessionsAsync(_businessDateFilter, cancellationToken);
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
            await _service.OpenSessionAsync(new OpenCashSessionRequest(PositionType, opening), CancellationToken.None);
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
        if (sessionIdError.Length > 0 || ActualCashError.Length > 0 || CountNotesError.Length > 0) { ErrorMessage = sessionIdError; return; }
        try { IsBusy = true; var count = await _service.SubmitCountAsync(sessionId, new SubmitCashCountRequest(actual, CountNotes), CancellationToken.None); ErrorMessage = $"Count {count.Status}: difference {count.Difference:N2}."; await RefreshAsync(); }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
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
        if (!HasCashOperationsPermission) return;
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

    private static bool HasTwoDecimalPlaces(decimal value) => decimal.Round(value, 2) == value;
}
