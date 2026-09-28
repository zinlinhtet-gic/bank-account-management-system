using bams.server.DTO.Configuration;

namespace bams.server.Services.Interfaces;

public interface IInterestRateService
{

    /// Gets all interest rate rules.
    Task<IReadOnlyList<InterestRateResponse>> GetInterestRatesAsync(
        CancellationToken cancellationToken);

    /// Gets the account types selectable in the interest rate form.
    Task<IReadOnlyList<AccountTypeOptionResponse>> GetAccountTypeOptionsAsync(
        CancellationToken cancellationToken);

    /// Creates a new interest rate rule.
    Task<InterestRateResponse> CreateInterestRateAsync(
        CreateInterestRateRequest request,
        CancellationToken cancellationToken);

    /// Updates an existing interest rate rule.
    Task<InterestRateResponse> UpdateInterestRateAsync(
        long id,
        UpdateInterestRateRequest request,
        CancellationToken cancellationToken);
}
