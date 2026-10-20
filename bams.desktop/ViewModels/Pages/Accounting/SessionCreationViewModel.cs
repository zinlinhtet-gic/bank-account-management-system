using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Creates additional schedules for today's already opened date.</summary>
public sealed class SessionCreationViewModel(ICashOperationsClientService client, IEndOfDayClientService dates) : SessionPageViewModel
{
    private DateOnly _today;
    private string _key = Guid.NewGuid().ToString("N");
    public SessionScheduleEditorViewModel Editor { get; } = new();
    public string DateLabel => _today.ToString("yyyy-MM-dd");
    public AsyncRelayCommand SubmitCommand => _submit ??= new(() => RunAsync(async () =>
    {
        if (!Editor.TryBuild(_today, out var rows, out var error)) { Message = error; return; }
        if (rows.Count == 0) { Message = "Add at least one session."; return; }
        await client.CreateSchedulesAsync(new(rows, _key, _today), CancellationToken.None);
        _key = Guid.NewGuid().ToString("N");
        Navigate(Constants.PageNames.CashReconciliation);
    }));
    private AsyncRelayCommand? _submit;
    public override Task InitializeAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        var day = await dates.GetCurrentBusinessDateAsync(cancellationToken);
        _today = day.Today ?? day.Date;
        OnPropertyChanged(nameof(DateLabel));
        await Editor.LoadAsync(client, cancellationToken);
    });
}
