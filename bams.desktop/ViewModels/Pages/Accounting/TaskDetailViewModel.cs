using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;
namespace bams.desktop.ViewModels.Pages.Accounting;
/// <summary>Read-only evidence for an authorized reconciliation task.</summary>
public sealed class TaskDetailViewModel : ReconciliationOperationViewModel
{
    private ReconciliationExceptionDetailResponse? _detail;
    public ReconciliationExceptionDetailResponse? Detail { get => _detail; private set => SetProperty(ref _detail, value); }
    public RelayCommand BackCommand { get; }
    public RelayCommand PerformCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public TaskDetailViewModel(ReconciliationWorkflowClient c, IReconciliationService e, AuthContext a) : base(c,e,a)
    {
        BackCommand = new(_ => Navigate(PageNames.AssignedReconciliationTasks, ExceptionId));
        PerformCommand = new(_ => Navigate(Detail?.Exception.PositionSessionId.HasValue == true ? PageNames.CashProposalEditor : PageNames.ReconciliationProposalEditor, ExceptionId),
            _ => Detail?.Exception.AssignedTo == Auth.UserId && CanInvestigate);
        RefreshCommand = new(() => InitializeAsync());
    }
    public override Task InitializeAsync(CancellationToken ct = default) => RunAsync(async () =>
    {
        Detail = null;
        if (ExceptionId is long id) Detail = await Existing.GetExceptionByIdAsync(id, ct);
        PerformCommand.RaiseCanExecuteChanged();
    });
}
