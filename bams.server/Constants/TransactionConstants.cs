namespace bams.server.Constants;

/// <summary>
/// Stores transaction business invariants used by posting, validation and NRC pickup.
/// </summary>
public static class TransactionConstants
{
    // Transaction numbers: prefix + UTC timestamp + random suffix, so two postings in the same millisecond stay unique.
    public const string TransactionNumberPrefix = "TXN";
    public const string TransactionNumberTimestampFormat = "yyyyMMddHHmmssfff";
    public const int TransactionNumberSuffixExclusiveMaximum = 10_000;
    public const string TransactionNumberSuffixFormat = "D4";

    // Amounts are stored as decimal(18, 2), so more decimal places would be silently rounded by the database.
    public const int MaximumAmountDecimalPlaces = 2;

    // Status values stored in string status columns.
    public const string CompletedStatus = "completed";
    public const string PickupPendingStatus = "pickup_pending";
    public const string PickupCompletedStatus = "pickup_completed";
    public const string PickupCancelledStatus = "pickup_cancelled";

    // Where an NRC transfer is collected (NrcCashTransferDetail.DeliveryType).
    public const string NrcDeliveryAtBranch = "Branch";
    public const string NrcDeliveryAtOtherBank = "OtherBank";

    // NRC pickup codes are six-digit numbers valid for a limited time after the transfer is created.
    public const int NrcPickupCodeMinimumValue = 100_000;
    public const int NrcPickupCodeExclusiveMaximum = 1_000_000;
    public static readonly TimeSpan NrcPickupCodeValidity = TimeSpan.FromHours(24);

    // Wrong pickup codes allowed before the transfer is blocked; a blocked transfer can only be cancelled and refunded.
    public const int MaximumFailedPickupAttempts = 5;

    // HTTP header a client sends so that a retried request does not post the same transaction twice.
    public const string IdempotencyKeyHeaderName = "Idempotency-Key";
    public const int IdempotencyKeyMaximumLength = 64;

    // Paging for transaction lists and account statements.
    public const int FirstPageNumber = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    // Maximum field lengths; these match the column sizes in Data/Configurations/TransactionConfigurations.cs.
    public const int DescriptionMaximumLength = 300;
    public const int ReferenceNoMaximumLength = 100;
    public const int PersonNameMaximumLength = 150;
    public const int NrcMaximumLength = 32;
    public const int PhoneMaximumLength = 32;
    public const int DestinationAccountNoMaximumLength = 40;
    public const int GatewayReferenceMaximumLength = 100;
}
