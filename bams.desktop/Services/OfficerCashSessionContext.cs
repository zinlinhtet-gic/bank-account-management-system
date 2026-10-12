using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace bams.desktop.Services;

/// <summary>Shares the signed-in officer's current-business-date cash-session state across desktop services and pages.</summary>
public sealed class OfficerCashSessionContext : INotifyPropertyChanged
{
    private bool _hasOpenSession;
    private bool _isChecked;

    public bool HasOpenSession
    {
        get => _hasOpenSession;
        private set { if (_hasOpenSession == value) return; _hasOpenSession = value; OnPropertyChanged(); }
    }

    public bool IsChecked
    {
        get => _isChecked;
        private set { if (_isChecked == value) return; _isChecked = value; OnPropertyChanged(); }
    }

    /// <summary>Updates the shared session gate after checking the current business date.</summary>
    public void SetStatus(bool hasOpenSession)
    {
        HasOpenSession = hasOpenSession;
        IsChecked = true;
    }

    /// <summary>Resets the session gate when the authenticated user changes.</summary>
    public void Reset()
    {
        HasOpenSession = false;
        IsChecked = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
