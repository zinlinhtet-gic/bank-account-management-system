namespace bams.desktop.Models.Operations;

/// <summary>One customer account's monthly interest accrual, formatted for the Interest operations table.</summary>
public sealed class InterestOperationRowModel
{
    /// <summary>Server id of the record, used to open its details.</summary>
    public long Id { get; init; }

    public string Customer { get; init; } = string.Empty;
    public string CustomerNo { get; init; } = string.Empty;
    public string AccountNo { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public string Balance { get; init; } = string.Empty;
    public string Rate { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string PostedAt { get; init; } = string.Empty;
}

/// <summary>One fee charged to a customer account, formatted for the Fees operations table.</summary>
public sealed class FeeOperationRowModel
{
    /// <summary>Server id of the record, used to open its details.</summary>
    public long Id { get; init; }

    public string Customer { get; init; } = string.Empty;
    public string CustomerNo { get; init; } = string.Empty;
    public string AccountNo { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
    public string FeeType { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty;
    public string Tax { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string PostedAt { get; init; } = string.Empty;
}

/// <summary>One customer's fixed deposit, formatted for the Fixed Deposit Maturity table.</summary>
public sealed class FixedDepositMaturityRowModel
{
    /// <summary>Server id of the record, used to open its details.</summary>
    public long Id { get; init; }

    public string Customer { get; init; } = string.Empty;
    public string CustomerNo { get; init; } = string.Empty;
    public string AccountNo { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
    public string Principal { get; init; } = string.Empty;
    public string Rate { get; init; } = string.Empty;
    public string StartDate { get; init; } = string.Empty;
    public string MaturityDate { get; init; } = string.Empty;
    public string DaysLeft { get; init; } = string.Empty;
    public string InterestAccrued { get; init; } = string.Empty;
    public string MaturityInterest { get; init; } = string.Empty;
    public string RenewalInstruction { get; init; } = string.Empty;
    public string PayoutAccountNo { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>True while the deposit is active and matures within the "due soon" window (highlighted).</summary>
    public bool IsDueSoon { get; init; }
}
