namespace bams.desktop.Constants;

/// <summary>
/// Client-side copy of the staff user rules in <c>bams.server/Constants/UserConstants.cs</c>, used to show field
/// errors before calling the server. The server stays authoritative; keep both in sync.
/// </summary>
public static class UserFieldRules
{
    public const int FullNameMaximumLength = 150;
    public const int EmailMaximumLength = 256;

    public const string UsernamePattern = @"^[A-Za-z0-9._-]{3,64}$";
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    public const string PhonePattern = @"^[0-9+\-\s()]{6,32}$";
}
