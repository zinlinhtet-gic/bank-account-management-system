namespace bams.desktop.Services;

/// <summary>
/// Ends the signed-in session from anywhere in the app (logout button, or a page that just removed the
/// current user's own access). <c>MainWindow</c> listens to <see cref="SessionEnded"/> and returns to sign-in.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Raised after the token and session have been cleared.
    /// </summary>
    event Action? SessionEnded;

    /// <summary>
    /// Clears the token and the signed-in user, then raises <see cref="SessionEnded"/>. Does not ask for confirmation.
    /// </summary>
    void EndSession();
}
