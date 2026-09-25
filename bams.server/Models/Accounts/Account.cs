using bams.server.Models.Products;
using bams.server.Models.Accounts.Enums;
namespace bams.server.Models.Accounts;

public sealed class Account : IConcurrencyTracked
{
    public long Id { get; set; }

    public string AccountNo { get; set; } = string.Empty;

    public long AccountTypeId { get; set; }

    public AccountType? AccountType { get; set; }

    public ICollection<AccountHolder> AccountHolders { get; set; } = [];


    public AccountStatus Status { get; set; } = AccountStatus.Active;

    public DateTime OpenedAt { get; set; }

    public DateTime? ActiveAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public decimal AvailableBalance { get; set; }

    public decimal LedgerBalance { get; set; }

    public DateTime? LastActivityAt { get; set; }

    public DateTime? DormantAt { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public DateTime? FrozenAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long Version { get; set; } = 1;
}
