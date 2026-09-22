using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;
using bams.server.Models.Customers;
using bams.server.Models.Products;

namespace bams.server.Services.Interfaces;

public interface IFixedDepositService
{
    /// <summary>
    /// Creates and persists fixed-deposit details for a newly opened account.
    /// </summary>
    Task<FixedDeposit> CreateFixedDepositAsync(
        Account account,
        AccountType accountType,
        Customer primaryHolder,
        CreateAccountRequest request,
        DateTime createdAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the supplied fixed-deposit fields and applies maturity renewal rules.
    /// </summary>
    Task<FixedDepositResponse> UpdateFixedDepositAsync(
        long fixedDepositId,
        UpdateFixedDepositRequest request,
        long changedBy,
        CancellationToken cancellationToken);
}
