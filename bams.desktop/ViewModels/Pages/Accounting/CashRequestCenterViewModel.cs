using System.Collections.ObjectModel;
using bams.desktop.Constants;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Manager queue for reviewing officers' requests for cash.</summary>
public sealed class CashRequestCenterViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ICashOperationsClientService _cashOperations;
    private readonly IEndOfDayClientService _businessDates;
    private readonly AuthContext _authContext;
    private readonly IDialogService _dialogs;
    private CashProposalResponse? _selectedRequest;
    private DateTime _selectedDate = DateTime.Today;
    private string _approvedAmountText = string.Empty;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private bool _isBusy;

    private string _status = "All";
    public IReadOnlyList<string> StatusOptions { get; } = ["All", "PendingApproval", "PendingReceipt", "Received", "Rejected"];
    public string SelectedStatus { get => _status; set => SetProperty(ref _status, value); }
    public AsyncRelayCommand AcceptRowCommand { get; }
    public AsyncRelayCommand RejectRowCommand { get; }
    public ObservableCollection<CashProposalResponse> Requests { get; } = [];
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ApproveCommand { get; }
    public AsyncRelayCommand RejectCommand { get; }

    public CashRequestCenterViewModel(ICashOperationsClientService cashOperations,
        IEndOfDayClientService businessDates, AuthContext authContext, IDialogService dialogs)
    {
        _cashOperations = cashOperations;
        _businessDates = businessDates;
        _authContext = authContext;
        _dialogs = dialogs;
        RefreshCommand = new AsyncRelayCommand(() => LoadRequestsAsync(), () => !IsBusy);
        ApproveCommand = new AsyncRelayCommand(() => ApproveAsync(), () => !IsBusy && CanReviewSelectedRequest);
        RejectCommand = new AsyncRelayCommand(() => RejectAsync(), () => !IsBusy && CanReviewSelectedRequest);
        AcceptRowCommand = new(async row => { SelectedRequest = row as CashProposalResponse; await ApproveAsync(); }, row => !IsBusy && IsManager && row is CashProposalResponse { Status: "PendingApproval" });
        RejectRowCommand = new(async row => { SelectedRequest = row as CashProposalResponse; await RejectAsync(); }, row => !IsBusy && IsManager && row is CashProposalResponse { Status: "PendingApproval" });
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set { if (SetProperty(ref _selectedDate, value)) ErrorMessage = string.Empty; }
    }
    public CashProposalResponse? SelectedRequest
    {
        get => _selectedRequest;
        set
        {
            if (!SetProperty(ref _selectedRequest, value)) return;
            ApprovedAmountText = value?.RequestedAmount?.ToString("0.00") ?? string.Empty;
            OnPropertyChanged(nameof(CanReviewSelectedRequest));
            ApproveCommand.RaiseCanExecuteChanged();
            RejectCommand.RaiseCanExecuteChanged();
        }
    }
    public string ApprovedAmountText { get => _approvedAmountText; set => SetProperty(ref _approvedAmountText, value); }
    public string ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public string SuccessMessage { get => _successMessage; private set { if (SetProperty(ref _successMessage, value)) OnPropertyChanged(nameof(HasSuccess)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);
    public bool IsManager => _authContext.HasPermission(PermissionCodes.EndOfDayApproval);
    public bool CanReviewSelectedRequest => IsManager && SelectedRequest?.Status == "PendingApproval";
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            RefreshCommand.RaiseCanExecuteChanged();
            AcceptRowCommand?.RaiseCanExecuteChanged();
            RejectRowCommand?.RaiseCanExecuteChanged();
            ApproveCommand.RaiseCanExecuteChanged();
            RejectCommand.RaiseCanExecuteChanged();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!IsManager) { ErrorMessage = "You do not have permission to review cash requests."; return; }
        try
        {
            var businessDate = await _businessDates.GetCurrentBusinessDateAsync(cancellationToken);
            SelectedDate = (businessDate.Today ?? businessDate.Date).ToDateTime(TimeOnly.MinValue);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        await LoadRequestsAsync(cancellationToken);
    }

    private async Task LoadRequestsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            var date = DateOnly.FromDateTime(SelectedDate);
            var requests = await _cashOperations.GetCashProposalsAsync(date, cancellationToken);
            Requests.Clear();
            foreach (var item in requests.Where(item => SelectedStatus == "All" || item.Status == SelectedStatus)) Requests.Add(item);
            SelectedRequest = requests.FirstOrDefault(item => item.Status == "PendingApproval");
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task ApproveAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedRequest is not { Status: "PendingApproval" } request) return;
        decimal amount;
        if (request.RequestedAmount.HasValue) amount = request.RequestedAmount.Value;
        else
        {
            var dialog = new CashAmountDialogViewModel();
            if (!_dialogs.ShowDialog(dialog)) return;
            amount = dialog.Amount;
        }
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await _cashOperations.ApproveCashProposalAsync(request.Id, new ApproveCashProposalRequest(amount, request.Version), cancellationToken);
            SuccessMessage = $"Cash request #{request.Id} approved. The cash is reserved until the officer signs the receipt or the request is rejected.";
            await LoadRequestsAsync(cancellationToken);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task RejectAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedRequest is not { Status: "PendingApproval" } request) return;
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await _cashOperations.RejectCashProposalAsync(request.Id,
                new CashProposalActionRequest(request.Version, "Manager rejected cash request"), cancellationToken);
            SuccessMessage = $"Cash request #{request.Id} rejected.";
            await LoadRequestsAsync(cancellationToken);
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }
}
