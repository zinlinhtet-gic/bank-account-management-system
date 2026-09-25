using bams.desktop.DTOs.Accounts;
using bams.desktop.Utils;

namespace bams.desktop.Models;

/// <summary>
/// An account in a transaction form's account picker.
/// </summary>
public sealed record AccountOption(long Id, string AccountNo, string AccountTypeCode, decimal AvailableBalance)
{
    /// <summary>"ACC20260925… · SAV".</summary>
    public string DisplayName => $"{AccountNo} · {AccountTypeCode}";

    /// <summary>"Available MMK 12,000.00", shown under the picker.</summary>
    public string BalanceText => $"Available {TransactionDisplay.FormatMoney(AvailableBalance)}";

    /// <summary>
    /// Whether the account can take part in a transaction. Closed, frozen and suspended accounts are left out of
    /// the pickers; the server rejects them anyway.
    /// </summary>
    public static bool IsUsable(AccountSummaryResponse account)
    {
        return account.Status is AccountStatus.Active or AccountStatus.Dormant;
    }

    /// <summary>
    /// Builds an option from a server account summary.
    /// </summary>
    public static AccountOption FromResponse(AccountSummaryResponse account)
    {
        return new AccountOption(account.Id, account.AccountNo, account.AccountTypeCode, account.AvailableBalance);
    }

    // ComboBox shows ToString() when no template is set.
    public override string ToString() => DisplayName;
}
