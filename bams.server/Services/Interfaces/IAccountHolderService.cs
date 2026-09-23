using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;
using bams.server.Models.Customers;
using bams.server.Models.Products;

namespace bams.server.Services.Interfaces;

public interface IAccountHolderService
{
    /// <summary>
    /// Resolves and validates the customers requested as account holders.
    /// </summary>
    Task<IReadOnlyList<Customer>> ResolveAndValidateHoldersAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates and returns the ownership percentages for a new account.
    /// </summary>
    IReadOnlyList<decimal> ValidateOwnershipPercentages(CreateAccountRequest request);

    /// <summary>
    /// Ensures the requested holders own any product required by the new account type.
    /// </summary>
    Task ValidateRequiredProductsAsync(
        AccountType accountType,
        IReadOnlyList<Customer> customers,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates and tracks holder relationships for a new account.
    /// </summary>
    Task CreateAccountHoldersAsync(
        Account account,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<decimal> ownershipPercentages,
        CreateAccountRequest request,
        DateTime createdAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the editable details of an account's existing joint holders.
    /// </summary>
    Task<IReadOnlyList<AccountHolderResponse>> UpdateHoldersOfAccountAsync(
        Account account,
        UpdateAccountHoldersRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds the primary customer's active individual account required by an account type.
    /// </summary>
    Task<Account> FindRequiredIndividualAccountAsync(
        AccountType requestedAccountType,
        Customer primaryHolder,
        CancellationToken cancellationToken);
}
