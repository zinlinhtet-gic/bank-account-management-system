namespace bams.desktop.Services;

/// <summary>
/// Clears the bearer token and <see cref="AuthContext"/>, then tells the shell to show the sign-in screen.
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly AuthContext _authContext;
    private readonly IAuthenticationService _authenticationService;

    public SessionService(AuthContext authContext, IAuthenticationService authenticationService)
    {
        _authContext = authContext;
        _authenticationService = authenticationService;
    }

    public event Action? SessionEnded;

    /// <inheritdoc />
    public void EndSession()
    {
        _authenticationService.ClearAuthToken();
        _authContext.ClearSession();
        SessionEnded?.Invoke();
    }
}
