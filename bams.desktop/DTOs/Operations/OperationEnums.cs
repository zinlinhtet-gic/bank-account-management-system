namespace bams.desktop.DTOs.Operations;

/// <summary>Interest accrual statuses written by the server's interest job (sent as text).</summary>
public enum InterestAccrualStatus
{
    Accrued = 1,
    Posted = 2
}

/// <summary>Mirrors the server's <c>bams.server.Models.InterestFees.FeeAccrualStatus</c> enum exactly.</summary>
public enum FeeAccrualStatus
{
    Accrued = 1,
    Posted = 2,
    Waived = 3,
    Cancelled = 4
}

/// <summary>Mirrors the server's <c>bams.server.Models.Accounts.Enums.FixedDepositStatus</c> enum exactly.</summary>
public enum FixedDepositStatus
{
    Active = 1,
    Matured = 2,
    Closed = 3,
    Cancelled = 4
}

/// <summary>Mirrors the server's <c>bams.server.Models.Accounts.Enums.AccountStatus</c> enum exactly.</summary>
public enum AccountStatus
{
    Active = 1,
    Dormant = 2,
    Suspended = 3,
    Closed = 4,
    Frozen = 5
}

/// <summary>Mirrors the server's <c>bams.server.Models.Accounts.Enums.RenewalInstruction</c> enum exactly.</summary>
public enum RenewalInstruction
{
    NoRenewal = 1,
    PrincipalOnly = 2,
    PrincipalAndInterest = 3
}
