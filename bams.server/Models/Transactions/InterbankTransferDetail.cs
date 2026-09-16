using bams.server.Models.External;

namespace bams.server.Models.Transactions;

public sealed class InterbankTransferDetail
{
    public long Id { get; set; }

    public long TransactionId { get; set; }

    public Transaction? Transaction { get; set; }

    public long OtherBankId { get; set; }

    public OtherBank? OtherBank { get; set; }

    public string DestinationAccountNo { get; set; } = string.Empty;

    public string BeneficiaryName { get; set; } = string.Empty;

    public string? GatewayReference { get; set; }

    public GatewayStatus GatewayStatus { get; set; } = GatewayStatus.Pending;

    public DateTime? RequestAt { get; set; }

    public DateTime? ResponseAt { get; set; }

    public string? SettlementReference { get; set; }
}
