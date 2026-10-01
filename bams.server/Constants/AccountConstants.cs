namespace bams.server.Constants;

/// <summary>
/// Stores account business invariants used by validation and account-number generation.
/// </summary>
public static class AccountConstants
{
    public const int AccountNameMaximumLength = 100;
    public const int AccountNumberMaximumLength = 32;
    public const decimal MinimumOpeningBalance = 0m;
    public const string AccountNumberPrefix = "ACC";
    public const string AccountNumberTimestampFormat = "yyyyMMddHHmmssfff";
}
