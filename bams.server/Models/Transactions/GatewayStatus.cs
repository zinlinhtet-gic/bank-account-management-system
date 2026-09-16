namespace bams.server.Models.Transactions;

public enum GatewayStatus
{
    Pending = 1,
    Success = 2,
    Failed = 3,
    Rejected = 4,
    Timeout = 5,
    Reversed = 6
}
