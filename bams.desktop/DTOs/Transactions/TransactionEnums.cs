namespace bams.desktop.DTOs.Transactions;

// Mirrors of the server's transaction enums. Member names must match: enums travel as names in JSON.

/// <summary>Server: <c>Models/Transactions/TransactionType</c>.</summary>
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
    FdEarlyWithdrawal = 11,
    Reversal = 12
}

/// <summary>Server: <c>Models/Transactions/TransactionStatus</c>.</summary>
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

/// <summary>Server: <c>Models/Transactions/EntryType</c>.</summary>
public enum EntryType
{
    Debit = 1,
    Credit = 2
}

/// <summary>Server: <c>Models/Transactions/GatewayStatus</c>.</summary>
public enum GatewayStatus
{
    Pending = 1,
    Success = 2,
    Failed = 3,
    Rejected = 4,
    Timeout = 5,
    Reversed = 6
}
