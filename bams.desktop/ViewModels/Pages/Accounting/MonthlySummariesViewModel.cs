using System.Collections.ObjectModel;
using System.Globalization;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>
/// "Monthly Summaries" tab: one row per accounting month with its total debit and credit. Clicking a row raises
/// <see cref="MonthSelected"/> so the host page can show that month's accounting entries.
/// </summary>
public sealed class MonthlySummariesViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IAccountingService _accountingService;
    private bool _isLoading;
    private bool _isRefreshing;
    private string? _errorMessage;

    /// <summary>One month row: totals across every GL account summarised for that month.</summary>
    public sealed record MonthRow(int Year, int Month, string MonthLabel, int GlAccountCount, decimal TotalDebit, decimal TotalCredit);

    public MonthlySummariesViewModel(IAccountingService accountingService)
    {
        _accountingService = accountingService;
        RefreshCommand = new AsyncRelayCommand(async _ => await LoadSummariesAsync(CancellationToken.None));
        SelectMonthCommand = new RelayCommand(row => { if (row is MonthRow month) MonthSelected?.Invoke(month.Year, month.Month); });
    }

    /// <summary>Raised with (year, month) when the user clicks a month row.</summary>
    public event Action<int, int>? MonthSelected;

    public ObservableCollection<MonthRow> Months { get; } = [];
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand SelectMonthCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set { if (SetProperty(ref _isLoading, value)) OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsRefreshing { get => _isRefreshing; private set => SetProperty(ref _isRefreshing, value); }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEmpty => !IsLoading && !HasError && Months.Count == 0;
    public string CountText => Months.Count == 1 ? "1 month" : $"{Months.Count} months";

    public Task InitializeAsync(CancellationToken cancellationToken) => LoadSummariesAsync(cancellationToken);

    // Loads every stored monthly summary and replaces the rows only after the request succeeds.
    private async Task LoadSummariesAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        IsRefreshing = true;
        try
        {
            ErrorMessage = null;
            var summaries = await _accountingService.GetMonthlySummariesAsync(null, null, null, cancellationToken);
            ReplaceMonths(GroupByMonth(summaries));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    // Collapses per-GL-account summaries into one row per month, newest month first.
    private static List<MonthRow> GroupByMonth(IReadOnlyList<MonthlySummaryResponse> summaries)
    {
        return summaries
            .GroupBy(summary => (summary.Year, summary.Month))
            .OrderByDescending(group => group.Key.Year)
            .ThenByDescending(group => group.Key.Month)
            .Select(group => new MonthRow(
                group.Key.Year,
                group.Key.Month,
                $"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(group.Key.Month)} {group.Key.Year}",
                group.Count(),
                group.Sum(summary => summary.TotalDebit),
                group.Sum(summary => summary.TotalCredit)))
            .ToList();
    }

    // Swaps the collection contents and refreshes the count badge.
    private void ReplaceMonths(List<MonthRow> rows)
    {
        Months.Clear();
        foreach (var row in rows)
        {
            Months.Add(row);
        }
        OnPropertyChanged(nameof(CountText));
    }
}
