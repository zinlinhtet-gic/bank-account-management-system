namespace bams.desktop.DTOs.Configuration;

/// <summary>Mirrors the server's <c>bams.server.Models.Products.FeeType</c> enum exactly.</summary>
public enum FeeType
{
    Maintenance = 1,
    Transfer = 2,
    InterbankTransfer = 3,
    EarlyWithdrawal = 4,
    DormantAccount = 5,
    CashTransfer = 6
}
