namespace bams.server.Constants;

/// <summary>
/// Audit-log action names and column limits. Action names are stable identifiers; filter the audit log by them.
/// </summary>
public static class AuditConstants
{
    public const string TransactionEntityType = "Transaction";

    public const string DepositAction = "transaction.deposit";
    public const string WithdrawalAction = "transaction.withdrawal";
    public const string InternalTransferAction = "transaction.internal_transfer";
    public const string InterbankTransferAction = "transaction.interbank_transfer";
    public const string InterbankCompletedAction = "transaction.interbank_completed";
    public const string InterbankFailedAction = "transaction.interbank_failed";
    public const string NrcTransferAction = "transaction.nrc_transfer";
    public const string NrcPickupAction = "transaction.nrc_pickup";
    public const string NrcPickupFailedAction = "transaction.nrc_pickup_failed";
    public const string NrcCancelledAction = "transaction.nrc_cancelled";
    public const string NrcPaidOutByOtherBankAction = "transaction.nrc_paid_out_other_bank";

    // These match the column sizes in Data/Configurations/AuditConfigurations.cs.
    public const int IpAddressMaximumLength = 64;
    public const int DeviceInfoMaximumLength = 300;
}
