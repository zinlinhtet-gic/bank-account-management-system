using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Recovers interrupted or failed verification for posted account and cash corrections without reposting.</summary>
public sealed class ReconciliationVerificationViewModel : ReconciliationOperationViewModel
{
    private readonly UnifiedReconciliationClient _unified;
    public AsyncRelayCommand PollCommand { get; }
    private bool _accountTabSelected = true;
    private bool _cashTabSelected;
    private string? _verificationMessage;
    private ReconciliationVerificationTask? _selectedTask;

    public ReconciliationVerificationTask? SelectedTask { get => _selectedTask; set => SetProperty(ref _selectedTask, value); }

    public ObservableCollection<ReconciliationVerificationTask> AccountTasks { get; } = [];
    public ObservableCollection<ReconciliationVerificationTask> CashTasks { get; } = [];
    public ObservableCollection<ReconciliationVerificationTask> Tasks => IsCashTabSelected ? CashTasks : AccountTasks;
    public bool IsAccountTabSelected
    {
        get => _accountTabSelected;
        set
        {
            if (!SetProperty(ref _accountTabSelected, value)) return;
            if (value) IsCashTabSelected = false;
            OnPropertyChanged(nameof(Tasks));
        }
    }
    public bool IsCashTabSelected
    {
        get => _cashTabSelected;
        set
        {
            if (!SetProperty(ref _cashTabSelected, value)) return;
            if (value) IsAccountTabSelected = false;
            OnPropertyChanged(nameof(Tasks));
        }
    }
    public string? VerificationMessage { get => _verificationMessage; private set => SetProperty(ref _verificationMessage, value); }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand VerifyCommand { get; }

    public ReconciliationVerificationViewModel(ReconciliationWorkflowClient client, IReconciliationService existing,
        AuthContext auth, UnifiedReconciliationClient unified) : base(client, existing, auth)
    {
        _unified = unified;
        PollCommand = new(() => InitializeAsync(), () => !IsBusy);
        RefreshCommand = new(() => InitializeAsync());
        VerifyCommand = new(value => RunAsync(() => VerifyAsync(value)), value =>
            (CanInvestigate || CanApprove) && value is ReconciliationVerificationTask task && task.Verification != VerificationStatus.Passed);
    }

    // Dispatch only to verification endpoints; successful verification removes the task from this recovery queue.
    private async Task VerifyAsync(object? value)
    {
        if (value is not ReconciliationVerificationTask task) return;
        VerificationMessage = null;
        if (task.IsCash)
        {
            CashCorrectionProposalResponse result = await _unified.VerifyCashAsync(task.ProposalId);
            VerificationMessage = result.VerificationNote;
        }
        else
        {
            CorrectionProposalResponse result = await Client.VerifyAsync(task.ProposalId, default);
            VerificationMessage = result.VerificationNote;
        }
        await LoadAsync(default);
    }

    // The server scopes visibility; exception status alone does not hide a posted but unverified proposal.
    private async Task LoadAsync(CancellationToken ct)
    {
        IReadOnlyList<ReconciliationVerificationTask> tasks = await _unified.VerificationTasksAsync(ct);
        // Account and cash proposal ids overlap, so a selection is matched on both id and kind.
        bool focusIsCash = FocusIsCash;
        long? focusId = TakeFocusId();
        (long Id, bool IsCash)? selected = focusId.HasValue ? (focusId.Value, focusIsCash)
            : SelectedTask is null ? null : (SelectedTask.ProposalId, SelectedTask.IsCash);
        AccountTasks.Clear();
        CashTasks.Clear();
        foreach (ReconciliationVerificationTask task in tasks)
        {
            if (task.IsCash) CashTasks.Add(task);
            else AccountTasks.Add(task);
        }
        if (focusId.HasValue)
        {
            if (focusIsCash) IsCashTabSelected = true;
            else IsAccountTabSelected = true;
        }
        SelectedTask = selected is null ? null
            : Tasks.FirstOrDefault(task => task.ProposalId == selected.Value.Id && task.IsCash == selected.Value.IsCash);
    }

    public override Task InitializeAsync(CancellationToken ct = default) => RunAsync(() => LoadAsync(ct));
}
