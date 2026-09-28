namespace bams.desktop.Constants;

/// <summary>
/// Client-side copy of the customer rules in <c>bams.server/Constants/CustomerConstants.cs</c>, used to show
/// field errors before calling the server. The server stays authoritative; keep both in sync.
/// </summary>
public static class CustomerFieldRules
{
    public const int FullNameMaximumLength = 150;
    public const int NrcNumberMaximumLength = 20;
    public const int PassportNumberMaximumLength = 20;
    public const int MinimumAgeYears = 18;
}
