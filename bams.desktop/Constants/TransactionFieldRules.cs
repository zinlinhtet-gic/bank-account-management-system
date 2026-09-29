namespace bams.desktop.Constants;

/// <summary>
/// Client-side copy of the transaction limits in <c>bams.server/Constants/TransactionConstants.cs</c>, used to show
/// field errors before calling the server. The server stays authoritative; keep both in sync.
/// </summary>
public static class TransactionFieldRules
{
    public const int MaximumAmountDecimalPlaces = 2;

    public const int DescriptionMaximumLength = 300;
    public const int ReferenceNoMaximumLength = 100;
    public const int PersonNameMaximumLength = 150;
    public const int NrcMaximumLength = 32;
    public const int PhoneMaximumLength = 32;
    public const int DestinationAccountNoMaximumLength = 40;
    public const int GatewayReferenceMaximumLength = 100;

    // NRC pickup codes are six digits.
    public const int PickupCodeLength = 6;

    /// <summary>Items per page of the transaction table (the server allows up to 100).</summary>
    public const int PageSize = 20;

    // Where an NRC transfer is collected; must match the server's TransactionConstants.NrcDeliveryAt* values.
    public const string NrcDeliveryAtBranch = "Branch";
    public const string NrcDeliveryAtOtherBank = "OtherBank";
}
