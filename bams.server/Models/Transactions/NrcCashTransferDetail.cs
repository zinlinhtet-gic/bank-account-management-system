using bams.server.Models.Accounts;
using bams.server.Models.External;
using bams.server.Models.Organization;
using bams.server.Models.Security;

namespace bams.server.Models.Transactions;

public sealed class NrcCashTransferDetail
{
    public long Id { get; set; }

    public long TransactionId { get; set; }

    public Transaction? Transaction { get; set; }

    public string SenderName { get; set; } = string.Empty;

    public string SenderNrc { get; set; } = string.Empty;

    public string? SenderPhone { get; set; }

    public string ReceiverName { get; set; } = string.Empty;

    public string ReceiverNrc { get; set; } = string.Empty;

    public string? ReceiverPhone { get; set; }

    // Where the receiver collects the money: TransactionConstants.NrcDeliveryAtBranch or NrcDeliveryAtOtherBank.
    public string DeliveryType { get; set; } = string.Empty;

    // Set when DeliveryType is NrcDeliveryAtBranch: the branch of this bank that pays out.
    public long? PickupBranchId { get; set; }

    public Branch? PickupBranch { get; set; }

    // Set when DeliveryType is NrcDeliveryAtOtherBank: the bank that pays out.
    public long? PickupOtherBankId { get; set; }

    public OtherBank? PickupOtherBank { get; set; }

    // The receiver's account, when the payout at pickup went into an account instead of cash.
    public long? DestinationAccountId { get; set; }

    public Account? DestinationAccount { get; set; }

    public string? PickupCodeHash { get; set; }

    public DateTime? PickupExpiresAt { get; set; }

    // Wrong pickup codes entered so far; pickup is blocked once this reaches the configured maximum.
    public int FailedPickupAttempts { get; set; }

    public DateTime? PickedUpAt { get; set; }

    public long? PickupVerifiedBy { get; set; }

    public User? PickupVerifiedByUser { get; set; }

    public string Status { get; set; } = string.Empty;
}
