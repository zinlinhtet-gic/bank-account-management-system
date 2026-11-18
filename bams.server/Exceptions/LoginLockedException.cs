using bams.server.Messages;

namespace bams.server.Exceptions;

/// <summary>Login is temporarily blocked after repeated incorrect passwords.</summary>
public sealed class LoginLockedException : AppException
{
    public int RetryAfterSeconds { get; }

    // Preserve the remaining lock duration for the HTTP Retry-After header.
    public LoginLockedException(TimeSpan remaining) : base(MessageCode.LoginTemporarilyLocked)
    {
        RetryAfterSeconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
    }
}
