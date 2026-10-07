using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Constants;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Presentation state for account-versus-ledger reconciliation.</summary>
public sealed class ReconciliationViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IReconciliationService _service;
    private readonly IEndOfDayClientService _businessDateService;
    private readonly AuthContext _authContext;
    private DateTime _fromDate = DateTime.Today;
    private DateTime _toDate = DateTime.Today;
    private string _accountSearchText = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;
    private ReconciliationExceptionResponse? _selectedException;
    private string _investigationStatus = "UnderInvestigation";
    private string? _investigationNotes;
    private ReconciliationExceptionDetailResponse? _exceptionDetail;
    private IReadOnlyList<string> _investigationStatuses = ["Open", "UnderInvestigation", "AdjustmentRequired"];
    private string _exceptionFilterStatus = "All";
    private string? _fromDateError;
    private string? _toDateError;
    private string? _accountLookupError;
    private string? _investigationStatusError;
    private string? _investigationNotesError;
    private string _correctionReason = string.Empty;
    private string _externalRecoveryReference = string.Empty;
    private string _reviewNote = string.Empty;
    private string _recoveryAttestation = string.Empty;
    private CorrectionTransactionCandidateResponse? _selectedCorrectionCandidate;
    private ReconciliationAccountOptionResponse? _selectedAccountOption;
    private ReconciliationStaffOptionResponse? _selectedInvestigator;
    private string _investigatorSearchText = string.Empty;
    private IReadOnlyList<ReconciliationStaffOptionResponse> _allInvestigatorOptions = [];

    public ReconciliationViewModel(IReconciliationService service, IEndOfDayClientService businessDateService, AuthContext authContext)
    {
        _service = service;
        _businessDateService = businessDateService;
        _authContext = authContext;
        RunCommand = new AsyncRelayCommand(async _ => await RunAsync());
        RefreshExceptionsCommand = new AsyncRelayCommand(async _ => await LoadExceptionsAsync());
        UpdateExceptionCommand = new AsyncRelayCommand(async () => await UpdateSelectedExceptionAsync(), () => CanUpdateSelectedException);
        LoadExceptionTimelineCommand = new AsyncRelayCommand(async _ => await LoadSelectedExceptionTimelineAsync());
        RequestCorrectionCommand = new AsyncRelayCommand(RequestCorrectionAsync, () => CanRequestCorrection);
        ApproveCorrectionCommand = new AsyncRelayCommand(() => ReviewCorrectionAsync(true), () => CanReviewCorrection);
        RejectCorrectionCommand = new AsyncRelayCommand(() => ReviewCorrectionAsync(false), () => CanReviewCorrection);
        LoadCorrectionCandidatesCommand = new AsyncRelayCommand(LoadCorrectionCandidatesAsync);
        SearchAccountsCommand = new AsyncRelayCommand(SearchAccountsAsync);
        LoadInvestigatorOptionsCommand = new AsyncRelayCommand(LoadInvestigatorOptionsAsync);
        ClearAccountFilterCommand = new AsyncRelayCommand(ClearAccountFilterAsync);
    }

    public ObservableCollection<AccountReconciliationResultResponse> Results { get; } = [];
    public ObservableCollection<ReconciliationExceptionResponse> Exceptions { get; } = [];
    public ObservableCollection<CorrectionTransactionCandidateResponse> CorrectionCandidates { get; } = [];
    public ObservableCollection<ReconciliationAccountOptionResponse> AccountOptions { get; } = [];
    public ObservableCollection<ReconciliationStaffOptionResponse> InvestigatorOptions { get; } = [];
    public IReadOnlyList<string> InvestigationStatuses { get => _investigationStatuses; private set => SetProperty(ref _investigationStatuses, value); }
    public IReadOnlyList<string> ExceptionFilterStatuses { get; } = ["All", "Open", "UnderInvestigation", "AdjustmentRequired", "Resolved"];
    public AsyncRelayCommand RunCommand { get; }
    public AsyncRelayCommand RefreshExceptionsCommand { get; }
    public AsyncRelayCommand UpdateExceptionCommand { get; }
    public AsyncRelayCommand LoadExceptionTimelineCommand { get; }
    public AsyncRelayCommand RequestCorrectionCommand { get; }
    public AsyncRelayCommand ApproveCorrectionCommand { get; }
    public AsyncRelayCommand RejectCorrectionCommand { get; }
    public AsyncRelayCommand LoadCorrectionCandidatesCommand { get; }
    public AsyncRelayCommand SearchAccountsCommand { get; }
    public AsyncRelayCommand LoadInvestigatorOptionsCommand { get; }
    public AsyncRelayCommand ClearAccountFilterCommand { get; }
    public CorrectionTransactionCandidateResponse? SelectedCorrectionCandidate
    {
        get => _selectedCorrectionCandidate;
        set
        {
            if (!SetProperty(ref _selectedCorrectionCandidate, value)) return;
            OnPropertyChanged(nameof(CanRequestCorrection));
            RequestCorrectionCommand.RaiseCanExecuteChanged();
        }
    }
    public ReconciliationAccountOptionResponse? SelectedAccountOption { get => _selectedAccountOption; set => SetProperty(ref _selectedAccountOption, value); }
    public ReconciliationStaffOptionResponse? SelectedInvestigator { get => _selectedInvestigator; set => SetProperty(ref _selectedInvestigator, value); }
    public string AccountSearchText { get => _accountSearchText; set => SetProperty(ref _accountSearchText, value); }
    public string InvestigatorSearchText { get => _investigatorSearchText; set => SetProperty(ref _investigatorSearchText, value); }
    public string CorrectionReason { get => _correctionReason; set => SetProperty(ref _correctionReason, value); }
    public string ExternalRecoveryReference { get => _externalRecoveryReference; set => SetProperty(ref _externalRecoveryReference, value); }
    public string ReviewNote { get => _reviewNote; set => SetProperty(ref _reviewNote, value); }
    public string RecoveryAttestation { get => _recoveryAttestation; set => SetProperty(ref _recoveryAttestation, value); }
    public bool CanReviewCorrection => SelectedException?.CorrectionRequestStatus == "Pending" && HasCorrectionApprovalPermission &&
        _authContext.UserId != SelectedException.CorrectionRequestedBy;
    public bool CanRequestCorrection => SelectedException is not null && SelectedCorrectionCandidate is not null && SelectedException.CorrectionRequestStatus != "Pending" &&
        _authContext.HasPermission(PermissionCodes.ReconciliationInvestigation);
    public bool HasCorrectionApprovalPermission => _authContext.HasPermission(PermissionCodes.TransactionCorrectionApproval);
    public ReconciliationExceptionResponse? SelectedException
    {
        get => _selectedException;
        set
        {
            if (!SetProperty(ref _selectedException, value)) return;
            if (value is not null)
            {
                InvestigationStatuses = GetAllowedStatuses(value.Status);
                InvestigationStatus = value.Status;
                InvestigationNotes = value.Notes;
                SelectedInvestigator = InvestigatorOptions.FirstOrDefault(item => item.Id == value.AssignedTo);
                CorrectionCandidates.Clear();
                SelectedCorrectionCandidate = null;
                _ = LoadCorrectionCandidatesAsync();
                if (CanManageInvestigation) _ = LoadInvestigatorOptionsAsync();
            }
            OnPropertyChanged(nameof(CanUpdateSelectedException));
            OnPropertyChanged(nameof(CanReviewCorrection));
            OnPropertyChanged(nameof(CanRequestCorrection));
            UpdateExceptionCommand.RaiseCanExecuteChanged();
            RequestCorrectionCommand.RaiseCanExecuteChanged();
            ApproveCorrectionCommand.RaiseCanExecuteChanged();
            RejectCorrectionCommand.RaiseCanExecuteChanged();
        }
    }
    public bool CanUpdateSelectedException => SelectedException is not null && SelectedException.Status != "Resolved" && CanManageInvestigation;
    public bool CanManageInvestigation => _authContext.HasPermission(PermissionCodes.ReconciliationInvestigation);
    public string InvestigationStatus { get => _investigationStatus; set => SetProperty(ref _investigationStatus, value); }
    public string? InvestigationNotes { get => _investigationNotes; set => SetProperty(ref _investigationNotes, value); }
    public ReconciliationExceptionDetailResponse? ExceptionDetail { get => _exceptionDetail; private set => SetProperty(ref _exceptionDetail, value); }
    public DateTime FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string ExceptionFilterStatus { get => _exceptionFilterStatus; set => SetProperty(ref _exceptionFilterStatus, value); }
    public string? FromDateError { get => _fromDateError; private set => SetProperty(ref _fromDateError, value); }
    public string? ToDateError { get => _toDateError; private set => SetProperty(ref _toDateError, value); }
    public string? AccountLookupError { get => _accountLookupError; private set => SetProperty(ref _accountLookupError, value); }
    public string? InvestigationStatusError { get => _investigationStatusError; private set => SetProperty(ref _investigationStatusError, value); }
    public string? InvestigationNotesError { get => _investigationNotesError; private set => SetProperty(ref _investigationNotesError, value); }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var current = await _businessDateService.GetCurrentBusinessDateAsync(cancellationToken);
            await InitializeForBusinessDateAsync(current.Date, cancellationToken);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }

    /// <summary>Uses the parent workflow's date and loads its exception list without running reconciliation.</summary>
    public async Task InitializeForBusinessDateAsync(DateOnly businessDate, CancellationToken cancellationToken)
    {
        FromDate = businessDate.ToDateTime(TimeOnly.MinValue);
        ToDate = businessDate.ToDateTime(TimeOnly.MinValue);
        await LoadExceptionsAsync(cancellationToken);
    }

    private async Task RunAsync()
    {
        if (!ValidateDateRange()) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _service.ReconcileAccountsAsync(new AccountReconciliationRequest(
                DateOnly.FromDateTime(FromDate), DateOnly.FromDateTime(ToDate), SelectedAccountOption?.Id), CancellationToken.None);
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
        InvestigationNotesError = (InvestigationNotes?.Length ?? 0) > 2000 ? "Investigation notes must be 2,000 characters or fewer." : string.Empty;
        InvestigationStatusError = !InvestigationStatuses.Contains(InvestigationStatus, StringComparer.Ordinal)
            ? "Choose a status allowed from the current exception state." : string.Empty;
        if (!string.IsNullOrEmpty(InvestigationNotesError) || !string.IsNullOrEmpty(InvestigationStatusError)) return;
        try
        {
            var updated = await _service.UpdateExceptionAsync(SelectedException.Id,
                new UpdateReconciliationExceptionRequest(InvestigationStatus, InvestigationNotes, SelectedInvestigator?.Id, null), CancellationToken.None);
            var index = Exceptions.IndexOf(SelectedException);
            if (index >= 0) Exceptions[index] = updated;
            SelectedException = updated;
            ErrorMessage = null;
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
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

    private async Task RequestCorrectionAsync()
    {
        if (SelectedException is null || SelectedCorrectionCandidate is null || string.IsNullOrWhiteSpace(CorrectionReason))
        { ErrorMessage = "Select a posted transaction from the list and explain why it should be reversed."; return; }
        try
        {
            SelectedException = await _service.RequestTransactionCorrectionAsync(SelectedException.Id,
                new RequestTransactionCorrectionRequest(SelectedCorrectionCandidate.Id, CorrectionReason.Trim(), ExternalRecoveryReference), CancellationToken.None);
            await LoadSelectedExceptionTimelineAsync();
            ErrorMessage = null;
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }

    private async Task LoadCorrectionCandidatesAsync()
    {
        if (SelectedException is null || !CanManageInvestigation) return;
        try
        {
            var items = await _service.GetCorrectionCandidatesAsync(SelectedException.Id, CancellationToken.None);
            CorrectionCandidates.Clear();
            foreach (var item in items) CorrectionCandidates.Add(item);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }

    private async Task SearchAccountsAsync()
    {
        try
        {
            AccountLookupError = null;
            var matches = await _service.SearchAccountsAsync(AccountSearchText, CancellationToken.None);
            AccountOptions.Clear();
            foreach (var account in matches) AccountOptions.Add(account);
            if (matches.Count == 0) AccountLookupError = "No account matched that number. Try a different part of the account number.";
        }
        catch (AppException exception) { AccountLookupError = exception.Message; }
    }

    private Task ClearAccountFilterAsync()
    {
        SelectedAccountOption = null;
        return Task.CompletedTask;
    }

    private async Task LoadInvestigatorOptionsAsync()
    {
        if (!CanManageInvestigation) return;
        try
        {
            var assignedId = SelectedInvestigator?.Id ?? SelectedException?.AssignedTo;
            _allInvestigatorOptions = await _service.GetInvestigatorOptionsAsync(CancellationToken.None);
            var search = InvestigatorSearchText.Trim();
            var staff = _allInvestigatorOptions.Where(person => string.IsNullOrEmpty(search) ||
                person.FullName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                person.Username.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                person.Role.Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToList();
            var assigned = _allInvestigatorOptions.FirstOrDefault(item => item.Id == assignedId);
            if (assigned is not null && staff.All(item => item.Id != assigned.Id)) staff.Insert(0, assigned);
            InvestigatorOptions.Clear();
            foreach (var person in staff) InvestigatorOptions.Add(person);
            SelectedInvestigator = InvestigatorOptions.FirstOrDefault(item => item.Id == assignedId);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }

    private async Task ReviewCorrectionAsync(bool approve)
    {
        if (SelectedException is null || string.IsNullOrWhiteSpace(ReviewNote)) { ErrorMessage = "Enter a review note. Rejections require a reason."; return; }
        try
        {
            SelectedException = await _service.ReviewTransactionCorrectionAsync(SelectedException.Id,
                new ReviewTransactionCorrectionRequest(approve, ReviewNote.Trim(), RecoveryAttestation), CancellationToken.None);
            ErrorMessage = null;
            await LoadExceptionsAsync();
            await LoadSelectedExceptionTimelineAsync();
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
    }
}
