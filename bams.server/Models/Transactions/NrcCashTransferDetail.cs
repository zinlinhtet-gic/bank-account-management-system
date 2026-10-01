using bams.server.Models.Accounts;
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

    public string DeliveryType { get; set; } = string.Empty;

    public long? DestinationAccountId { get; set; }

    public Account? DestinationAccount { get; set; }

    public string? PickupCodeHash { get; set; }

    public DateTime? PickupExpiresAt { get; set; }

    public DateTime? PickedUpAt { get; set; }

    public long? PickupVerifiedBy { get; set; }

    public User? PickupVerifiedByUser { get; set; }

    public string Status { get; set; } = string.Empty;
}
