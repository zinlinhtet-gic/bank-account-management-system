namespace bams.server.Constants;

/// <summary>
/// Stores account business invariants used by validation and account-number generation.
/// </summary>
public static class AccountConstants
{
    public const int AccountNameMaximumLength = 100;
    public const int AccountNumberMaximumLength = 32;
    public const int AccountStatusReasonMaximumLength = 300;
    public const int AccountHolderSigningRuleMaximumLength = 100;
    public const int RequiredJointAccountHolderCount = 2;
    public const int AccountNumberRequiredLength = 16;
    public const int AccountTypeIdentifierWidth = 2;
    public const int MaximumAccountTypeIdentifier = 99;
    public const int AccountNumberSequenceWidth = 4;
    public const int MaximumHourlyAccountNumberSequence = 9_999;
    public const int MinimumAccountPageSize = 1;
    public const int DefaultAccountPageSize = 20;
    public const int MaximumAccountPageSize = 100;
    public const decimal MinimumOwnershipPercentage = 0m;
    public const decimal FullOwnershipPercentage = 100m;
    public const string AccountNumberTimestampFormat = "yyyyMMddHH";
    public const string AccountCursorVersion = "v1";
    public const char AccountCursorSeparator = ':';
}
