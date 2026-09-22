using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace bams.desktop.ViewModels;

/// <summary>
/// Base class for ViewModels that expose bindable property changes.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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
