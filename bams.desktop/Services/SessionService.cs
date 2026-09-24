using bams.desktop.Constants;
using bams.desktop.Exceptions;

namespace bams.desktop.Services;

/// <summary>
/// Sends the presence heartbeat while signed in, and on sign-out tells the server, clears the bearer token and
/// <see cref="AuthContext"/>, then tells the shell to show the sign-in screen.
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly AuthContext _authContext;
    private readonly IAuthenticationService _authenticationService;

    private CancellationTokenSource? _heartbeatCancellation;

    public SessionService(AuthContext authContext, IAuthenticationService authenticationService)
    {
        _authContext = authContext;
        _authenticationService = authenticationService;
    }

    public event Action? SessionEnded;

    /// <inheritdoc />
    public void StartSession()
    {
        StopHeartbeat();

        _heartbeatCancellation = new CancellationTokenSource();
        _ = SendHeartbeatsAsync(_heartbeatCancellation.Token);
    }

    /// <inheritdoc />
    public async Task EndSessionAsync()
    {
        StopHeartbeat();

        // Best effort: if the server cannot be reached (or already refuses this user, e.g. after self-delete),
        // sign out locally anyway; the server shows the user offline once heartbeats stop.
        try
        {
            using var timeout = new CancellationTokenSource(PresenceConstants.LogoutRequestTimeout);
            await _authenticationService.LogoutAsync(timeout.Token);
        }
        catch (AppException)
        {
        }
        catch (OperationCanceledException)
        {
        }

        _authenticationService.ClearAuthToken();
        _authContext.ClearSession();
        SessionEnded?.Invoke();
    }

    // Sends a heartbeat every interval until the session ends. Failures are ignored: a missed heartbeat only
    // makes the user look offline for a while, and real errors surface on the next page action.
    private async Task SendHeartbeatsAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PresenceConstants.HeartbeatInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await _authenticationService.SendHeartbeatAsync(cancellationToken);
                }
                catch (AppException)
                {
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Session ended.
        }
    }

    private void StopHeartbeat()
    {
        _heartbeatCancellation?.Cancel();
        _heartbeatCancellation?.Dispose();
        _heartbeatCancellation = null;
    }
}
