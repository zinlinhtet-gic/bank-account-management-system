using bams.server.Models.Products;

namespace bams.server.Models.Accounts;

public sealed class Account
{
    public long Id { get; set; }

    public string AccountNo { get; set; } = string.Empty;

    public long AccountTypeId { get; set; }

    public AccountType? AccountType { get; set; }

    public AccountStatus Status { get; set; } = AccountStatus.Active;

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public decimal AvailableBalance { get; set; }

    public decimal LedgerBalance { get; set; }

    public DateTime? LastActivityAt { get; set; }

    public DateTime? DormantAt { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
