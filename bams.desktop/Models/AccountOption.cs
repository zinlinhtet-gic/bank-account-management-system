using bams.desktop.DTOs.Accounts;
using bams.desktop.Utils;

namespace bams.desktop.Models;

/// <summary>
/// An account in a transaction form's account picker.
/// </summary>
public sealed record AccountOption(
    long Id,
    string AccountNo,
    string AccountTypeCode,
    decimal AvailableBalance,
    string OwnerName = "",
    string? OwnerNrc = null,
    bool AllowDeposit = true,
    bool AllowWithdrawal = true,
    bool AllowTransfer = true)
{
    /// <summary>"ACC20260925… · SAV".</summary>
    public string DisplayName => $"{AccountNo} · {AccountTypeCode}";

    /// <summary>"U Ba · 12/ABC(N)123456", shown under the account number in the picker's results.</summary>
    public string OwnerText => string.IsNullOrWhiteSpace(OwnerNrc) ? OwnerName : $"{OwnerName} · {OwnerNrc}";

    /// <summary>Text the picker matches typed input against: account number, owner name and NRC.</summary>
    public string SearchText => $"{AccountNo} {OwnerName} {OwnerNrc}";

    /// <summary>"Available MMK 12,000.00", shown under the picker.</summary>
    public string BalanceText => $"Available {TransactionDisplay.FormatMoney(AvailableBalance)}";

    /// <summary>
    /// Whether the account can take part in a transaction. Closed, frozen and suspended accounts are left out of
    /// the pickers; the server rejects them anyway.
    /// </summary>
    public static bool IsUsable(AccountSummaryResponse account)
    {
        return account.Status is "Active" or "Dormant";
    }

    /// <summary>
    /// Loads the accounts that can take part in a transaction, as picker options.
    /// </summary>
    /// <exception cref="Exceptions.AppException">Server or network failure; the caller shows the message.</exception>
    public static async Task<IReadOnlyList<AccountOption>> LoadUsableAsync(
        Services.IAccountService accountService,
        CancellationToken cancellationToken)
    {
        var accounts = await accountService.GetAccountsAsync(cancellationToken);

        return accounts.Where(IsUsable).Select(FromResponse).ToList();
    }

    /// <summary>
    /// Builds an option from a server account summary.
    /// </summary>
    public static AccountOption FromResponse(AccountSummaryResponse account)
    {
        // Primary holder; joint accounts show "+N" for the other holders.
        var holders = account.Customers ?? [];
        var owner = holders.FirstOrDefault(holder => holder.IsPrimary) ?? holders.FirstOrDefault();
        var ownerName = owner is null ? string.Empty : owner.FullName;

        if (holders.Count > 1)
        {
            ownerName += $" +{holders.Count - 1}";
        }

        var ownerId = owner is null ? null : owner.NrcNumber ?? owner.PassportNumber;

        return new AccountOption(account.Id, account.AccountNo, account.AccountTypeCode, account.AvailableBalance,
            ownerName, ownerId, account.AllowDeposit, account.AllowWithdrawal, account.AllowTransfer);
    }

    // ComboBox shows ToString() when no template is set.
    public override string ToString() => DisplayName;
}
