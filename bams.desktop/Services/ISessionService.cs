namespace bams.desktop.Services;

/// <summary>
/// Owns the signed-in session's lifetime: keeps the user shown as online while the app is open, and ends the
/// session from anywhere (logout button, or a page that just removed the current user's own access).
/// <c>MainWindow</c> listens to <see cref="SessionEnded"/> and returns to sign-in.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Raised after the token and session have been cleared.
    /// </summary>
    event Action? SessionEnded;

    /// <summary>
    /// Starts the presence heartbeat. Call once the main application is shown after sign-in.
    /// </summary>
    void StartSession();

    /// <summary>
    /// Stops the heartbeat, tells the server the user signed out (best effort), clears the token and the
    /// signed-in user, then raises <see cref="SessionEnded"/>. Does not ask for confirmation.
    /// </summary>
    Task EndSessionAsync();
}
