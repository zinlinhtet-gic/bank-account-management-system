namespace bams.server.Constants;

/// <summary>Calendar and text constants used when accruing and crediting savings and fixed-deposit interest.</summary>
public static class InterestAccrualConstants
{
    // Savings interest is quoted per year and accrued once per calendar month.
    public const decimal MonthsPerYear = 12m;

    // Accrued interest is credited to the account after every calendar quarter.
    public const int MonthsPerQuarter = 3;

    public const string AccrualDescription = "Monthly account interest accrued";
    public const string QuarterlySavingsCreditDescription = "Quarterly savings interest credit";
    public const string QuarterlyFixedDepositCreditDescription = "Quarterly fixed-deposit interest credit";
}
