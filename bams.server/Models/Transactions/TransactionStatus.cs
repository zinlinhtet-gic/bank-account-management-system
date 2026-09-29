namespace bams.server.Models.Transactions;

public enum TransactionStatus
{
    Pending = 1,
    Authorized = 2,
    Posted = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6,
    Reversed = 7
}
