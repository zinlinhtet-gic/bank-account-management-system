namespace bams.desktop.Constants;

/// <summary>
/// Client-side copy of the customer rules in <c>bams.server/Constants/CustomerConstants.cs</c>, used to show
/// field errors before calling the server. The server stays authoritative; keep both in sync.
/// </summary>
public static class CustomerFieldRules
{
    public const int FullNameMaximumLength = 150;
    public const int NrcNumberMaximumLength = 20;
    // The digits-only serial segment shown in parentheses on the NRC number, e.g. the "123456" in "9/AHMAYA(N)123456".
    public const int NrcSerialNumberLength = 6;
    public const int PassportNumberMaximumLength = 20;
    public const int MinimumAgeYears = 18;
    public const string CustomerEmailRegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    public const string NrcNumberRegexPattern =
       @"^(0[1-9]|1[0-4])/[A-Z]{3,10}\([NEP]\)\d{6}$";

    // General passport format: 6-9 alphanumeric characters
    public const string PassportNumberRegexPattern =
        @"^[A-Z0-9]{6,9}$"; // eg. A123456, AB1234567, 123456789

    public const string PhoneNumberRegexPattern = @"^\d{11,15}$"; // International format, 11-15 digits 
}
