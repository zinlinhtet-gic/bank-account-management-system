namespace bams.desktop.ViewModels;

/// <summary>
/// The error text under one form field. Bind <c>t:ThemeAssist.HasError</c> to <see cref="HasError"/> and the
/// error line to <see cref="Message"/>. Useful for forms with many fields, where a pair of properties per field
/// would repeat the same code.
/// </summary>
public sealed class FieldError : ViewModelBase
{
    private string _message = string.Empty;

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(Message);

    /// <summary>Shows the message, or clears the error when it is empty.</summary>
    public void Set(string message) => Message = message;

    /// <summary>Removes the error, e.g. when the user edits the field.</summary>
    public void Clear() => Message = string.Empty;
}
