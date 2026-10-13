using System.Windows;
using System.Windows.Input;
using bams.desktop.Constants;
using bams.desktop.Exceptions;
using bams.desktop.Utils;

namespace bams.desktop.Services;

/// <summary>
/// Sends the presence heartbeat while signed in, renews the token while the user is active (sliding expiration),
/// and on sign-out tells the server, clears the bearer token and <see cref="AuthContext"/>, then tells the shell
/// to show the sign-in screen.
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly AuthContext _authContext;
    private readonly IAuthenticationService _authenticationService;

    private CancellationTokenSource? _heartbeatCancellation;

    // Set while signing out, so the 401s that arrive meanwhile (other page requests, the logout call itself)
    // do not start a second sign-out.
    private bool _isEndingSession;

    // Last keyboard or mouse input in any window of the app, and when the current token was issued. The token
    // is renewed only if the user did something since then, so the heartbeat alone never keeps a session alive.
    private DateTime _lastActivityUtc;
    private DateTime _tokenIssuedUtc;

    public SessionService(AuthContext authContext, IAuthenticationService authenticationService)
    {
        _authContext = authContext;
        _authenticationService = authenticationService;

        // The server rejected the token (expired or revoked): sign out instead of leaving each page
        // showing "Authentication is required". The heartbeat makes this happen even while idle.
        _authContext.SessionExpired += HandleSessionExpired;

        // Class handlers see input in every window, including open dialogs. Registered once: this is a singleton.
        EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent,
            new KeyEventHandler((_, _) => RecordActivity()), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseDownEvent,
            new MouseButtonEventHandler((_, _) => RecordActivity()), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler((_, _) => RecordActivity()), handledEventsToo: true);
    }

    public event Action? SessionEnded;

    /// <inheritdoc />
    public void StartSession()
    {
        StopHeartbeat();

        // The token was issued by the sign-in (or password change) that just finished.
        _tokenIssuedUtc = DateTime.UtcNow;
        _lastActivityUtc = _tokenIssuedUtc;

        _heartbeatCancellation = new CancellationTokenSource();
        _ = SendHeartbeatsAsync(_heartbeatCancellation.Token);
    }

    /// <inheritdoc />
    public async Task EndSessionAsync()
    {
        if (_isEndingSession)
        {
            return;
        }

        _isEndingSession = true;
        try
        {
            await EndSessionCoreAsync();
        }
        finally
        {
            _isEndingSession = false;
        }
    }

    private void RecordActivity()
    {
        _lastActivityUtc = DateTime.UtcNow;
    }

    // The 401 may surface on a background continuation, so the sign-out runs on the UI thread.
    private void HandleSessionExpired()
    {
        if (_isEndingSession)
        {
            return;
        }

        Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            if (_isEndingSession || !_authContext.IsAuthenticated)
            {
                return;
            }

            _authContext.SignInNotice = MessageCatalog.GetMessage(MessageCode.AuthenticationRequired);
            await EndSessionAsync();
        });
    }

    private async Task EndSessionCoreAsync()
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

    // Sends a heartbeat every interval until the session ends, renewing the token when the user has been active.
    // Failures are ignored: a missed heartbeat only makes the user look offline for a while, a failed refresh is
    // retried on the next tick, and an expired token is handled through AuthContext.SessionExpired.
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
                    await RefreshTokenIfActiveAsync(cancellationToken);
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

    private async Task RefreshTokenIfActiveAsync(CancellationToken cancellationToken)
    {
        // Refresh at most every few minutes, but always before half the token's lifetime has passed,
        // so a short server expiry (e.g. a few minutes while testing) still gets renewed in time.
        var now = DateTime.UtcNow;
        var lifetime = _authContext.TokenExpiry.ToUniversalTime() - _tokenIssuedUtc;
        var refreshAfter = TimeSpan.FromTicks(Math.Min(PresenceConstants.TokenRefreshMinInterval.Ticks, lifetime.Ticks / 2));

        if (_lastActivityUtc <= _tokenIssuedUtc || now - _tokenIssuedUtc < refreshAfter)
        {
            return;
        }

        var response = await _authenticationService.RefreshTokenAsync(cancellationToken);

        // Signed out while the request was in flight: do not put a token back.
        cancellationToken.ThrowIfCancellationRequested();

        _authenticationService.SetAuthToken(response.Token);
        _authContext.Token = response.Token;
        _authContext.TokenExpiry = response.Expiration;
        _tokenIssuedUtc = now;
    }

    private void StopHeartbeat()
    {
        _heartbeatCancellation?.Cancel();
        _heartbeatCancellation?.Dispose();
        _heartbeatCancellation = null;
    }
}
