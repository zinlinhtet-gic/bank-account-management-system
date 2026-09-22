using bams.server.DTO.Products;
using bams.server.Models.Products;

namespace bams.server.Services.Interfaces;

public interface IAccountTypeService
{
    /// <summary>
    /// Gets all active account products available for account opening.
    /// </summary>
    Task<IReadOnlyList<AccountTypeResponse>> GetAvailableAccountTypesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets an account type by its identifier and rejects unknown identifiers.
    /// </summary>
    Task<AccountType> GetAccountTypeByIdAsync(
        long accountTypeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates the opening balance against the account type's configured minimum.
    /// </summary>
    void ValidateOpeningBalance(decimal openingBalance, AccountType accountType);

    /// <summary>
    /// Determines whether an account type represents a fixed-deposit product.
    /// </summary>
    bool IsFixedDeposit(AccountType accountType);
}
