namespace bams.server.Constants;

/// Operations views (per-customer interest, fees and fixed-deposit maturity).
public static class OperationConstants
{
    // Interest accrual statuses written by InterestAccumulationService.
    public const string InterestAccruedStatus = "Accrued";
    public const string InterestPostedStatus = "Posted";

    // Annual rates are stored as percentages; fixed deposits accrue on an actual/365 day count
    public const decimal PercentageDivisor = 100m;
    public const decimal DaysPerYear = 365m;
}
