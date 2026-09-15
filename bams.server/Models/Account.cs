using System.ComponentModel.DataAnnotations;
using bams.server.Constants;

namespace bams.server.Models;

public sealed class Account
{
    public long Id { get; set; }

    [MaxLength(AccountConstants.AccountNumberMaximumLength)]
    public string AccountNumber { get; set; } = string.Empty;

    [MaxLength(AccountConstants.AccountNameMaximumLength)]
    public string Name { get; set; } = string.Empty;

    public AccountType Type { get; set; }

    public AccountStatus Status { get; set; }

    public decimal Balance { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
