using bams.desktop.Models;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// One account choice on a transaction form (the source or the destination): the officer picks an account type
/// first, then searches the accounts of that type. Shown by the AccountSelectionTemplate in TransactionFormView.
/// </summary>
public sealed class AccountSelectionViewModel : ViewModelBase
{
    private readonly IReadOnlyList<AccountOption> _pool;
    private IReadOnlyList<AccountOption> _accounts = [];
    private string? _accountType;
    private AccountOption? _account;

    /// <summary>
    /// Creates the selection over <paramref name="pool"/>, the accounts this side of the form may use.
    /// </summary>
    /// <param name="label">Field label above the account search, e.g. "FROM ACCOUNT".</param>
    public AccountSelectionViewModel(IReadOnlyList<AccountOption> pool, string label)
    {
        _pool = pool;
        Label = label;
        AccountTypes = pool.Select(account => account.AccountTypeCode).Distinct()
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToList();

        // Nothing to choose when only one account type qualifies.
        if (AccountTypes.Count == 1)
        {
            _accountType = AccountTypes[0];
            _accounts = AccountsOfType(_accountType);
        }
    }

    public string Label { get; }

    /// <summary>Account types present in the pool, sorted by code.</summary>
    public IReadOnlyList<string> AccountTypes { get; }

    /// <summary>Chosen first; the account search then lists only accounts of this type.</summary>
    public string? AccountType
    {
        get => _accountType;
        set
        {
            if (SetProperty(ref _accountType, value))
            {
                _accounts = AccountsOfType(value);
                OnPropertyChanged(nameof(Accounts));
                OnPropertyChanged(nameof(HasAccountType));

                // An account of another type no longer belongs to the list.
                if (Account is not null && Account.AccountTypeCode != value)
                {
                    Account = null;
                }
            }
        }
    }

    public bool HasAccountType => AccountType is not null;

    /// <summary>Accounts of the chosen type; empty until a type is chosen.</summary>
    public IReadOnlyList<AccountOption> Accounts => _accounts;

    public AccountOption? Account
    {
        get => _account;
        set
        {
            if (SetProperty(ref _account, value))
            {
                Error.Clear();
                OnPropertyChanged(nameof(BalanceText));
            }
        }
    }

    /// <summary>"Available MMK 12,000.00" of the chosen account, or empty.</summary>
    public string BalanceText => Account?.BalanceText ?? string.Empty;

    public FieldError Error { get; } = new();

    /// <summary>The "required" message for a missing choice, or empty when an account is chosen.</summary>
    public string RequiredMessage => Account is not null
        ? string.Empty
        : AccountType is null ? "Please choose the account type first." : "Please choose the account.";

    private IReadOnlyList<AccountOption> AccountsOfType(string? accountTypeCode)
    {
        return accountTypeCode is null
            ? []
            : _pool.Where(account => account.AccountTypeCode == accountTypeCode).ToList();
    }
}
