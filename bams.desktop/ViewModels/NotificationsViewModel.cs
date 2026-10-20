using System.Collections.ObjectModel;
using System.Windows.Threading;
using bams.desktop.Commands;
using bams.desktop.DTOs.Notifications;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels;

/// <summary>
/// Owns the header bell: polls the signed-in user's unread task notifications, exposes the badge count,
/// and asks the shell to open the related page when a notification is clicked.
/// </summary>
public sealed class NotificationsViewModel : ViewModelBase, IDisposable
{
    // How often the bell checks for new notifications while the shell is open.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    // Larger counts are shown as "99+" so the badge stays small.
    private const int BadgeMaximum = 99;

    private readonly INotificationClientService _service;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isRefreshing;
    private int _unreadCount;
    private string? _errorMessage;
    private bool _isOpen;

    public NotificationsViewModel(INotificationClientService service)
    {
        _service = service;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenCommand = new AsyncRelayCommand(OpenAsync, parameter => parameter is NotificationResponse);
        DismissCommand = new AsyncRelayCommand(DismissAsync, parameter => parameter is NotificationResponse);
        MarkAllReadCommand = new AsyncRelayCommand(MarkAllReadAsync, () => HasUnread);
        _refreshTimer = new DispatcherTimer { Interval = RefreshInterval };
        _refreshTimer.Tick += OnRefreshTimerTick;
    }

    public ObservableCollection<NotificationResponse> Items { get; } = new();
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand OpenCommand { get; }
    public AsyncRelayCommand DismissCommand { get; }
    public AsyncRelayCommand MarkAllReadCommand { get; }

    /// <summary>Raised after a clicked notification is marked read; the shell navigates to its target.</summary>
    public event Action<NotificationResponse>? OpenRequested;

    /// <summary>Total unread notifications shown on the bell badge (may exceed the listed items).</summary>
    public int UnreadCount
    {
        get => _unreadCount;
        private set
        {
            if (!SetProperty(ref _unreadCount, value)) return;
            OnPropertyChanged(nameof(HasUnread));
            OnPropertyChanged(nameof(BadgeText));
            MarkAllReadCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasUnread => UnreadCount > 0;

    /// <summary>Whether the bell popup is open; it closes when a notification is opened.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            // Opening the popup fetches the latest list instead of waiting for the next poll.
            if (SetProperty(ref _isOpen, value) && value)
                _ = RefreshAsync();
        }
    }

    // Keeps the small badge readable when many notifications are waiting.
    public string BadgeText => UnreadCount > BadgeMaximum ? $"{BadgeMaximum}+" : UnreadCount.ToString();

    public bool HasNoItems => Items.Count == 0 && string.IsNullOrWhiteSpace(ErrorMessage);

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasNoItems));
        }
    }

    /// <summary>Loads notifications immediately and then keeps them fresh on a timer.</summary>
    public void Start()
    {
        if (_refreshTimer.IsEnabled) return;
        _refreshTimer.Start();
        _ = RefreshAsync();
    }

    /// <summary>Replaces the list with the server's current unread notifications.</summary>
    public async Task RefreshAsync()
    {
        // The timer and the Refresh button must not run overlapping requests.
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
            var result = await _service.GetUnreadAsync(CancellationToken.None);
            ApplyNotifications(result);
            ErrorMessage = null;
        }
        catch (AppException exception)
        {
            ErrorMessage = MessageCatalog.GetMessage(MessageCode.NotificationsUnavailable);
            LogError("Could not refresh notifications.", exception);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>Shows a message in the bell, e.g. when the user cannot open a notification's page.</summary>
    public void ShowMessage(MessageCode code) => ErrorMessage = MessageCatalog.GetMessage(code);

    // Marks the notification read, removes it locally, then asks the shell to open its page.
    private async Task OpenAsync(object? parameter)
    {
        if (parameter is not NotificationResponse notification) return;
        if (!await TryMarkReadAsync(notification)) return;
        IsOpen = false;
        OpenRequested?.Invoke(notification);
    }

    // Clears one notification without navigating.
    private async Task DismissAsync(object? parameter)
    {
        if (parameter is NotificationResponse notification)
            await TryMarkReadAsync(notification);
    }

    private async Task MarkAllReadAsync()
    {
        try
        {
            await _service.MarkAllReadAsync(CancellationToken.None);
            ApplyNotifications(new NotificationListResponse(0, []));
            ErrorMessage = null;
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
            LogError("Could not mark all notifications as read.", exception);
        }
    }

    private async Task<bool> TryMarkReadAsync(NotificationResponse notification)
    {
        try
        {
            await _service.MarkReadAsync(notification.Id, CancellationToken.None);
            Items.Remove(notification);
            UnreadCount = Math.Max(0, UnreadCount - 1);
            OnPropertyChanged(nameof(HasNoItems));
            ErrorMessage = null;
            return true;
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
            LogError("Could not mark a notification as read.", exception);
            return false;
        }
    }

    private void ApplyNotifications(NotificationListResponse result)
    {
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
        UnreadCount = result.UnreadCount;
        OnPropertyChanged(nameof(HasNoItems));
    }

    private async void OnRefreshTimerTick(object? sender, EventArgs eventArgs) => await RefreshAsync();

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        OpenRequested = null;
    }
}
