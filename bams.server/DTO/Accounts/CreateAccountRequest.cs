using System.ComponentModel.DataAnnotations;
using bams.server.Constants;
using bams.server.Models;

namespace bams.server.DTO.Accounts;

public sealed record CreateAccountRequest(
    [Required]
    [MaxLength(AccountConstants.AccountNameMaximumLength)]
    string Name,
    AccountType Type,
    decimal OpeningBalance);
