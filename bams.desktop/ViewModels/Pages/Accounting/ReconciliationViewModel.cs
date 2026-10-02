using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Presentation state for account-versus-ledger reconciliation.</summary>
public sealed class ReconciliationViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IReconciliationService _service;
    private DateTime _fromDate = DateTime.Today;
    private DateTime _toDate = DateTime.Today;
    private string? _accountIdText;
    private string? _errorMessage;
    private bool _isBusy;
    private ReconciliationExceptionResponse? _selectedException;
    private string _investigationStatus = "UnderInvestigation";
    private string? _investigationNotes;
    private string _assignedToText = string.Empty;
    private string _correctionTransactionIdText = string.Empty;
    private ReconciliationExceptionDetailResponse? _exceptionDetail;
    private IReadOnlyList<string> _investigationStatuses = ["Open", "UnderInvestigation", "AdjustmentRequired"];
    private string _exceptionFilterStatus = "All";
    private string? _fromDateError;
    private string? _toDateError;
    private string? _accountIdError;
    private string? _investigationStatusError;
    private string? _investigationNotesError;
    private string? _assignedToError;
    private string? _correctionTransactionIdError;

    public ReconciliationViewModel(IReconciliationService service)
    {
        _service = service;
        RunCommand = new AsyncRelayCommand(async _ => await RunAsync());
        RefreshExceptionsCommand = new AsyncRelayCommand(async _ => await LoadExceptionsAsync());
        UpdateExceptionCommand = new AsyncRelayCommand(async _ => await UpdateSelectedExceptionAsync());
        LoadExceptionTimelineCommand = new AsyncRelayCommand(async _ => await LoadSelectedExceptionTimelineAsync());
    }

    public ObservableCollection<AccountReconciliationResultResponse> Results { get; } = [];
    public ObservableCollection<ReconciliationExceptionResponse> Exceptions { get; } = [];
    public IReadOnlyList<string> InvestigationStatuses { get => _investigationStatuses; private set => SetProperty(ref _investigationStatuses, value); }
    public IReadOnlyList<string> ExceptionFilterStatuses { get; } = ["All", "Open", "UnderInvestigation", "AdjustmentRequired", "Resolved"];
    public AsyncRelayCommand RunCommand { get; }
    public AsyncRelayCommand RefreshExceptionsCommand { get; }
    public AsyncRelayCommand UpdateExceptionCommand { get; }
    public AsyncRelayCommand LoadExceptionTimelineCommand { get; }
    public ReconciliationExceptionResponse? SelectedException { get => _selectedException; set { if (SetProperty(ref _selectedException, value)) { if (value is not null) { InvestigationStatuses = GetAllowedStatuses(value.Status); InvestigationStatus = value.Status; InvestigationNotes = value.Notes; AssignedToText = value.AssignedTo?.ToString() ?? string.Empty; CorrectionTransactionIdText = value.CorrectionTransactionId?.ToString() ?? string.Empty; } OnPropertyChanged(nameof(CanUpdateSelectedException)); } } }
    public bool CanUpdateSelectedException => SelectedException is not null && SelectedException.Status != "Resolved";
    public string InvestigationStatus { get => _investigationStatus; set => SetProperty(ref _investigationStatus, value); }
    public string? InvestigationNotes { get => _investigationNotes; set => SetProperty(ref _investigationNotes, value); }
    public string AssignedToText { get => _assignedToText; set => SetProperty(ref _assignedToText, value); }
    public string CorrectionTransactionIdText { get => _correctionTransactionIdText; set => SetProperty(ref _correctionTransactionIdText, value); }
    public ReconciliationExceptionDetailResponse? ExceptionDetail { get => _exceptionDetail; private set => SetProperty(ref _exceptionDetail, value); }
    public DateTime FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public string? AccountIdText { get => _accountIdText; set => SetProperty(ref _accountIdText, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string ExceptionFilterStatus { get => _exceptionFilterStatus; set => SetProperty(ref _exceptionFilterStatus, value); }
    public string? FromDateError { get => _fromDateError; private set => SetProperty(ref _fromDateError, value); }
    public string? ToDateError { get => _toDateError; private set => SetProperty(ref _toDateError, value); }
    public string? AccountIdError { get => _accountIdError; private set => SetProperty(ref _accountIdError, value); }
    public string? InvestigationStatusError { get => _investigationStatusError; private set => SetProperty(ref _investigationStatusError, value); }
    public string? InvestigationNotesError { get => _investigationNotesError; private set => SetProperty(ref _investigationNotesError, value); }
    public string? AssignedToError { get => _assignedToError; private set => SetProperty(ref _assignedToError, value); }
    public string? CorrectionTransactionIdError { get => _correctionTransactionIdError; private set => SetProperty(ref _correctionTransactionIdError, value); }

    public async Task InitializeAsync(CancellationToken cancellationToken) => await LoadExceptionsAsync(cancellationToken);

    private async Task RunAsync()
    {
        if (!ValidateDateRange()) return;
        AccountIdError = !string.IsNullOrWhiteSpace(AccountIdText) && (!long.TryParse(AccountIdText, out var id) || id <= 0)
            ? "Enter a positive account ID, or leave this blank to reconcile all accounts." : string.Empty;
        if (!string.IsNullOrEmpty(AccountIdError)) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var accountId = long.TryParse(AccountIdText, out var parsed) ? parsed : (long?)null;
            var result = await _service.ReconcileAccountsAsync(new AccountReconciliationRequest(
                DateOnly.FromDateTime(FromDate), DateOnly.FromDateTime(ToDate), accountId), CancellationToken.None);
            Results.Clear();
            foreach (var item in result.Results) Results.Add(item);
            await LoadExceptionsAsync();
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task LoadExceptionsAsync(CancellationToken cancellationToken = default)
    {
        if (!ValidateDateRange()) return;
        var status = ExceptionFilterStatus == "All" ? null : ExceptionFilterStatus;
        if (status is not null && !ExceptionFilterStatuses.Skip(1).Contains(status, StringComparer.Ordinal))
        {
            ErrorMessage = "Choose All or a listed exception status.";
            return;
        }
        try
        {
            var result = await _service.GetExceptionsAsync(DateOnly.FromDateTime(FromDate), DateOnly.FromDateTime(ToDate), status, cancellationToken);
            Exceptions.Clear();
            foreach (var item in result.Items) Exceptions.Add(item);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }

    private async Task UpdateSelectedExceptionAsync()
    {
        if (SelectedException is null) return;
        AssignedToError = !TryParseOptionalId(AssignedToText, out var assignedTo) ? "Enter a positive user ID or leave this blank." : string.Empty;
        CorrectionTransactionIdError = !TryParseOptionalId(CorrectionTransactionIdText, out var correctionId) ? "Enter a positive transaction ID or leave this blank." : string.Empty;
        InvestigationNotesError = (InvestigationNotes?.Length ?? 0) > 2000 ? "Investigation notes must be 2,000 characters or fewer." : string.Empty;
        InvestigationStatusError = !InvestigationStatuses.Contains(InvestigationStatus, StringComparer.Ordinal)
            ? "Choose a status allowed from the current exception state." : string.Empty;
        if (!string.IsNullOrEmpty(AssignedToError) || !string.IsNullOrEmpty(CorrectionTransactionIdError) ||
            !string.IsNullOrEmpty(InvestigationNotesError) || !string.IsNullOrEmpty(InvestigationStatusError)) return;
        try
        {
            var updated = await _service.UpdateExceptionAsync(SelectedException.Id,
                new UpdateReconciliationExceptionRequest(InvestigationStatus, InvestigationNotes, assignedTo, correctionId), CancellationToken.None);
            var index = Exceptions.IndexOf(SelectedException);
            if (index >= 0) Exceptions[index] = updated;
            SelectedException = updated;
            ErrorMessage = null;
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }

    private static bool TryParseOptionalId(string value, out long? id)
    {
        id = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!long.TryParse(value, out var parsed) || parsed <= 0) return false;
        id = parsed;
        return true;
    }

    private bool ValidateDateRange()
    {
        FromDateError = null;
        ToDateError = null;
        if (FromDate.Date > ToDate.Date)
        {
            ToDateError = "Choose a date on or after the start date.";
            return false;
        }
        if ((ToDate.Date - FromDate.Date).TotalDays + 1 > 366)
        {
            ToDateError = "Choose a range of 366 business dates or fewer.";
            return false;
        }
        return true;
    }

    private static IReadOnlyList<string> GetAllowedStatuses(string status) => status switch
    {
        "Open" => ["Open", "UnderInvestigation", "AdjustmentRequired"],
        "UnderInvestigation" => ["UnderInvestigation", "Open", "AdjustmentRequired"],
        "AdjustmentRequired" => ["AdjustmentRequired", "UnderInvestigation"],
        "Resolved" => ["Resolved"],
        _ => ["Open", "UnderInvestigation", "AdjustmentRequired"]
    };

    private async Task LoadSelectedExceptionTimelineAsync()
    {
        if (SelectedException is null) return;
        try { ExceptionDetail = await _service.GetExceptionByIdAsync(SelectedException.Id, CancellationToken.None); }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }
}
