using System.ComponentModel;
using System.Runtime.CompilerServices;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels;

/// <summary>
/// Base class for ViewModels that expose bindable property changes.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    protected ViewModelBase()
    {
        LogInformation("ViewModel created.");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Writes an informational entry associated with this view model.</summary>
    protected void LogInformation(string message)
    {
        AppLog.WriteInformation($"{GetType().Name}: {message}");
    }

    /// <summary>Writes an error entry associated with this view model.</summary>
    protected void LogError(string message, Exception exception)
    {
        AppLog.WriteError($"{GetType().Name}: {message}", exception);
    }

    // Updates a backing field and notifies WPF only when the value actually changes.
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);

        return true;
    }

    // Raises the PropertyChanged event for the specified property.
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
