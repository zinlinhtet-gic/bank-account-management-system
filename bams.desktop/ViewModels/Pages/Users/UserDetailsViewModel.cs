using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Users;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Users;

/// <summary>
/// Read-only detail card for one user, opened by clicking a table row. Closes with <c>true</c> when the
/// manager chooses "Edit" (the page then opens the edit form), <c>false</c> on Close.
/// </summary>
public sealed class UserDetailsViewModel : ViewModelBase, IDialogViewModel
{
    public UserDetailsViewModel(UserResponse user, bool isCurrentUser)
    {
        User = user;
        IsCurrentUser = isCurrentUser;

        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        EditCommand = new RelayCommand(_ => CloseRequested?.Invoke(true));
    }

    public event Action<bool>? CloseRequested;

    // Nothing to lose on a detail card, so clicking outside closes it.
    public bool CanCloseOnBackdropClick => true;

    public bool CanCancel => true;

    public UserResponse User { get; }

    public bool IsCurrentUser { get; }

    public RelayCommand CloseCommand { get; }

    public RelayCommand EditCommand { get; }

    public string RoleName => RoleCodes.ToDisplayName(User.Role);

    public string PhoneText => string.IsNullOrWhiteSpace(User.Phone) ? DisplayFormats.EmptyValue : User.Phone;

    public bool IsActive => User.Status == UserStatus.Active;

    public string StatusText => User.Status.ToString();

    /// <summary>e.g. "Online · Active now" or "Offline · Last seen 12 min ago".</summary>
    public string PresenceText =>
        $"{(User.IsOnline ? "Online" : "Offline")} · " + PresenceFormatter.FormatLastSeen(
            User.LastSeenAt is null ? null : DateTimeDisplay.ToLocal(User.LastSeenAt.Value),
            User.IsOnline);

    public string LastLoginText => User.LastLoginAt is null
        ? "Never signed in"
        : DateTimeDisplay.ToLocal(User.LastLoginAt.Value).ToString(DisplayFormats.DateTime);

    public string PasswordText => User.MustChangePassword
        ? "Default password: must be changed at next sign-in"
        : "Set by the user";

    public string CreatedText => DateTimeDisplay.ToLocal(User.CreatedAt).ToString(DisplayFormats.DateTime);

    public string UpdatedText => DateTimeDisplay.ToLocal(User.UpdatedAt).ToString(DisplayFormats.DateTime);
}
