using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>One editable Myanmar-time schedule row, shared by opening and later creation.</summary>
public sealed class SessionScheduleRow : ViewModelBase
{
    private CashSessionAssigneeResponse? _officer;
    private int _startHour = 9, _startMinute, _endHour = 17, _endMinute;
    private string _cash = "";
    private bool _last;
    public CashSessionAssigneeResponse? Officer { get => _officer; set => SetProperty(ref _officer, value); }
    public int StartHour { get => _startHour; set { if (SetProperty(ref _startHour, value)) OnPropertyChanged(nameof(Start)); } }
    public int StartMinute { get => _startMinute; set { if (SetProperty(ref _startMinute, value)) OnPropertyChanged(nameof(Start)); } }
    public int EndHour { get => _endHour; set { if (SetProperty(ref _endHour, value)) OnPropertyChanged(nameof(End)); } }
    public int EndMinute { get => _endMinute; set { if (SetProperty(ref _endMinute, value)) OnPropertyChanged(nameof(End)); } }
    public string Start => $"{StartHour:D2}:{StartMinute:D2}";
    public string End => $"{EndHour:D2}:{EndMinute:D2}";
    public string Cash { get => _cash; set { if (SetProperty(ref _cash, value)) OnPropertyChanged(nameof(DisplayCash)); } }
    public string DisplayCash => MoneyText.TryParse(Cash, out var amount) ? MoneyText.Format(amount) : Cash;
    public bool IsLast { get => _last; set => SetProperty(ref _last, value); }
}

/// <summary>Owns repeatable schedule rows and local input validation.</summary>
public sealed class SessionScheduleEditorViewModel : ViewModelBase
{
    public ObservableCollection<SessionScheduleRow> Rows { get; } = [];
    public ObservableCollection<CashSessionAssigneeResponse> Officers { get; } = [];
    public IReadOnlyList<int> Hours { get; } = Enumerable.Range(0, 24).ToArray();
    public IReadOnlyList<int> Minutes { get; } = Enumerable.Range(0, 60).ToArray();
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
            var start = new TimeOnly(row.StartHour, row.StartMinute);
            var end = new TimeOnly(row.EndHour, row.EndMinute);
            if (row.Officer is null || end <= start || !MoneyText.TryParse(row.Cash, out var cash) || cash <= 0m || decimal.Round(cash, 2) != cash)
            { error = "Each row needs an officer, a valid start/end period, and positive starting cash with at most two decimal places."; return false; }
            var item = new ScheduledCashSessionRequest(row.Officer.UserId, today.ToDateTime(start), today.ToDateTime(end), cash);
            if (result.Any(other => other.OfficerId == item.OfficerId && other.StartAtMyanmar < item.EndAtMyanmar && other.EndAtMyanmar > item.StartAtMyanmar))
            { error = "An officer's scheduled periods cannot overlap."; return false; }
            result.Add(item);
        }
        return true;
    }
}
