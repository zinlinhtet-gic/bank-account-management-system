namespace bams.server.Models.Transactions;

public enum TransactionType
{
    CashDeposit = 1,
    CashWithdrawal = 2,
    InternalTransfer = 3,
    InterbankTransfer = 4,
    NrcTransfer = 5,
    NrcPickup = 6,
    InterestCredit = 7,
    MaintenanceFee = 8,
    Penalty = 9,
    FdMaturity = 10,
    FdEarlyWithdrawal = 11
}
