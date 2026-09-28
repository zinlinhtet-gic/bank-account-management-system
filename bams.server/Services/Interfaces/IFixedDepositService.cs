using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
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
    /// Updates the payout account and renewal instruction exposed by the API.
    /// </summary>
    Task<FixedDepositResponse> UpdateFixedDepositAsync(
        long fixedDepositId,
        UpdateFixedDepositRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the current principal through an internal application workflow.
    /// </summary>
    Task<FixedDepositResponse> UpdateFixedDepositCurrentPrincipalAsync(
        long fixedDepositId,
        decimal currentPrincipal,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the status through an internal application workflow and applies renewal rules.
    /// </summary>
    Task<FixedDepositResponse> UpdateFixedDepositStatusAsync(
        long fixedDepositId,
        FixedDepositStatus status,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically updates the current principal and status through an internal application workflow.
    /// </summary>
    Task<FixedDepositResponse> UpdateFixedDepositCurrentPrincipalAndStatusAsync(
        long fixedDepositId,
        decimal currentPrincipal,
        FixedDepositStatus status,
        long expectedVersion,
        CancellationToken cancellationToken);
}
