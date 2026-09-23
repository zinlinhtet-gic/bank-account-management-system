namespace bams.server.Constants;

/// <summary>
/// Stores customer business invariants used by validation and customer-number generation.
/// </summary>
public static class CustomerConstants
{
    public const int FullNameMaximumLength = 150;
    public const int NrcNumberMaximumLength = 20;
    public const int PassportNumberMaximumLength = 20;
    public const int MinimumAgeYears = 18;
    public const string CustomerNumberPrefix = "CUS";
    public const string DefaultStatus = "Inactive";
    public const string DocumentUploadFolder = "customers";
    public const string CustomerEmailRegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    public const int CustomersPageSize = 10;
}
