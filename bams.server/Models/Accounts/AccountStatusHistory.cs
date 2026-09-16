using bams.server.Models.Security;

namespace bams.server.Models.Accounts;

public sealed class AccountStatusHistory
{
    public long Id { get; set; }

    public long AccountId { get; set; }

    public Account? Account { get; set; }

    public AccountStatus OldStatus { get; set; }

    public AccountStatus NewStatus { get; set; }

    public string? Reason { get; set; }

    public long ChangedBy { get; set; }

    public User? ChangedByUser { get; set; }

    public DateTime ChangedAt { get; set; }
}
