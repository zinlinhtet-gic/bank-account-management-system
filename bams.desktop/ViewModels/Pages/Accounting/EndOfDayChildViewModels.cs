using System.Collections.ObjectModel;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

public sealed class EndOfDaySummaryViewModel : ViewModelBase
{
    private BusinessDateResponse? _businessDate;
    public BusinessDateResponse? BusinessDate { get => _businessDate; set => SetProperty(ref _businessDate, value); }
}

public sealed class PreCloseChecksViewModel : ViewModelBase
{
    public ObservableCollection<EndOfDayStageResponse> Stages { get; } = [];
    public void Update(IReadOnlyList<EndOfDayStageResponse> stages)
    {
        Stages.Clear();
        foreach (var stage in stages) Stages.Add(stage);
    }
}

public sealed class LedgerReconciliationViewModel : ViewModelBase
{
    private string _status = "Locked";
    public string Status { get => _status; set => SetProperty(ref _status, value); }
}

public sealed class AccountReconciliationViewModel : ViewModelBase
{
    private string _status = "Locked";
    public AccountReconciliationViewModel(IReconciliationService service)
    {
        Results = new ReconciliationViewModel(service);
    }
    public ReconciliationViewModel Results { get; }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
}

public sealed class ExceptionCenterViewModel : ViewModelBase
{
    private int _unresolvedCount;
    public ExceptionCenterViewModel(IReconciliationService service)
    {
        Investigation = new ReconciliationViewModel(service);
    }
    public int UnresolvedCount { get => _unresolvedCount; set => SetProperty(ref _unresolvedCount, value); }
    public ReconciliationViewModel Investigation { get; }
}

public sealed class FinalReviewViewModel : ViewModelBase
{
    private EndOfDayRunResponse? _run;
    public EndOfDayRunResponse? Run { get => _run; set => SetProperty(ref _run, value); }
}
