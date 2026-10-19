using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>One editable Myanmar-time schedule row, shared by opening and later creation.</summary>
public sealed class SessionScheduleRow : ViewModelBase
{
    private CashSessionAssigneeResponse? _officer;
    private string _start = "09:00", _end = "17:00", _cash = "";
    private bool _last;
    public CashSessionAssigneeResponse? Officer { get => _officer; set => SetProperty(ref _officer, value); }
    public string Start { get => _start; set => SetProperty(ref _start, value); }
    public string End { get => _end; set => SetProperty(ref _end, value); }
    public string Cash { get => _cash; set => SetProperty(ref _cash, value); }
    public bool IsLast { get => _last; set => SetProperty(ref _last, value); }
}

/// <summary>Owns repeatable schedule rows and local input validation.</summary>
public sealed class SessionScheduleEditorViewModel : ViewModelBase
{
    public ObservableCollection<SessionScheduleRow> Rows { get; } = [];
    public ObservableCollection<CashSessionAssigneeResponse> Officers { get; } = [];
    public RelayCommand AddCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public bool HasNoRows => Rows.Count == 0;
    public SessionScheduleEditorViewModel()
    {
        AddCommand = new(_ => { Rows.Add(new()); UpdateRows(); });
        RemoveCommand = new(row => { if (row is SessionScheduleRow item) Rows.Remove(item); UpdateRows(); });
    }
    // Recomputes the latest-row Add action after every edit to the collection.
    private void UpdateRows()
    {
        foreach (var row in Rows) row.IsLast = row == Rows.LastOrDefault();
        OnPropertyChanged(nameof(HasNoRows));
    }
    /// <summary>Loads assignable active officers without starting work from the constructor.</summary>
    public async Task LoadAsync(ICashOperationsClientService client, CancellationToken cancellationToken)
    {
        Officers.Clear();
        foreach (var officer in await client.GetSessionAssigneesAsync(cancellationToken)) Officers.Add(officer);
    }
    /// <summary>Builds server inputs after validating row fields and overlapping periods.</summary>
    public bool TryBuild(DateOnly today, out IReadOnlyList<ScheduledCashSessionRequest> schedules, out string error)
    {
        List<ScheduledCashSessionRequest> result = [];
        schedules = result; error = "";
        foreach (var row in Rows)
        {
            if (row.Officer is null || !TimeOnly.TryParse(row.Start, out var start) || !TimeOnly.TryParse(row.End, out var end) || end <= start ||
                !decimal.TryParse(row.Cash, out var cash) || cash <= 0m || decimal.Round(cash, 2) != cash)
            { error = "Each row needs an officer, a valid start/end period, and positive starting cash with at most two decimal places."; return false; }
            var item = new ScheduledCashSessionRequest(row.Officer.UserId, today.ToDateTime(start), today.ToDateTime(end), cash);
            if (result.Any(other => other.OfficerId == item.OfficerId && other.StartAtMyanmar < item.EndAtMyanmar && other.EndAtMyanmar > item.StartAtMyanmar))
            { error = "An officer's scheduled periods cannot overlap."; return false; }
            result.Add(item);
        }
        return true;
    }
}
