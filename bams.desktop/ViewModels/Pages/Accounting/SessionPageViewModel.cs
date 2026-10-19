using bams.desktop.Commands;
using bams.desktop.Exceptions;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Explicit session navigation and shared asynchronous presentation state.</summary>
public abstract class SessionPageViewModel : ViewModelBase, IAsyncInitializable
{
    private bool _busy;
    private string _message = "";
    public event Action<string, long?>? NavigateRequested;
    public bool IsBusy { get => _busy; protected set { if (SetProperty(ref _busy, value)) OnBusyChanged(); } }
    public MessageCode? MessageCode { get; protected set; }
    public string Message { get => _message; protected set { MessageCode = string.IsNullOrEmpty(value) ? null : Utils.MessageCode.ValidationFailed; SetProperty(ref _message, value); OnPropertyChanged(nameof(MessageCode)); } }
    public long? SessionId { get; set; }
    public RelayCommand BackCommand { get; }
    protected SessionPageViewModel() => BackCommand = new(_ => Navigate(Constants.PageNames.CashReconciliation));
    /// <summary>Requests a page with its explicit session identifier.</summary>
    protected void Navigate(string page, long? sessionId = null) => NavigateRequested?.Invoke(page, sessionId);
    /// <summary>Prevents overlapping page actions and exposes typed server failures.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        try { IsBusy = true; Message = ""; await action(); }
        catch (OperationCanceledException) { }
        catch (AppException error) { Message = error.Message; MessageCode = error.Code; OnPropertyChanged(nameof(MessageCode)); }
        finally { IsBusy = false; }
    }
    protected virtual void OnBusyChanged() { }
    public abstract Task InitializeAsync(CancellationToken cancellationToken);
}
