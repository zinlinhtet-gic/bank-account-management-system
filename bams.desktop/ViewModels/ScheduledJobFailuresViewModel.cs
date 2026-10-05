using System.Collections.ObjectModel;
using System.Windows.Threading;
using bams.desktop.Commands;
using bams.desktop.DTOs.Jobs;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels;

/// <summary>Owns manager alerts for scheduled operations that exhausted automatic retries.</summary>
public sealed class ScheduledJobFailuresViewModel : ViewModelBase, IDisposable
{
    private readonly IScheduledJobClientService _service;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isEnabled;
    private string? _errorMessage;

    public ScheduledJobFailuresViewModel(IScheduledJobClientService service)
    {
        _service = service;
        RetryCommand = new AsyncRelayCommand(RetryAsync, parameter => parameter is FailedScheduledJobResponse);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _refreshTimer.Tick += OnRefreshTimerTick;
        OpenEndOfDayCommand = new RelayCommand(_ => OpenEndOfDay());
    }

    public ObservableCollection<FailedScheduledJobResponse> Failures { get; } = new();
    public AsyncRelayCommand RetryCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand OpenEndOfDayCommand { get; }
    public event Action? EndOfDayRequested;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasNoFailures));
        }
    }

    public bool HasFailures => Failures.Count > 0;
    public bool HasNoFailures => Failures.Count == 0 && string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEnabled
    {
        get => _isEnabled;
        private set => SetProperty(ref _isEnabled, value);
    }

    public void Start()
    {
        if (IsEnabled)
            return;
        IsEnabled = true;
        _refreshTimer.Start();
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var failures = await _service.GetFailuresAsync(CancellationToken.None);
            Failures.Clear();
            foreach (var failure in failures)
                Failures.Add(failure);
            OnPropertyChanged(nameof(HasFailures));
            OnPropertyChanged(nameof(HasNoFailures));
            ErrorMessage = null;
        }
        catch (AppException exception)
        {
            ErrorMessage = MessageCatalog.GetMessage(exception.Code);
            LogError("Could not refresh scheduled operation alerts.", exception);
        }
        catch (Exception exception)
        {
            ErrorMessage = MessageCatalog.GetMessage(MessageCode.ScheduledJobAlertsUnavailable);
            LogError("Could not refresh scheduled operation alerts.", exception);
        }
    }

    public void OpenEndOfDay() => EndOfDayRequested?.Invoke();

    private async Task RetryAsync(object? parameter)
    {
        if (parameter is not FailedScheduledJobResponse failure)
            return;
        try
        {
            await _service.RetryAsync(failure.ExecutionId, CancellationToken.None);
            await RefreshAsync();
        }
        catch (AppException exception)
        {
            ErrorMessage = MessageCatalog.GetMessage(exception.Code);
            LogError("Could not queue scheduled operation retry.", exception);
        }
    }

    private async void OnRefreshTimerTick(object? sender, EventArgs eventArgs) => await RefreshAsync();

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        EndOfDayRequested = null;
    }
}
