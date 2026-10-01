using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;

/// Interest Rate API calls (<c>api/interest-rates</c>)
public interface IInterestRateService
{

    /// Loads one page of interest rate rules, 10 per page.
    Task<PagedResponse<InterestRateResponse>> GetInterestRatesAsync(int page, CancellationToken cancellationToken);


    /// Loads the account types selectable in the form.
    Task<IReadOnlyList<AccountTypeOptionResponse>> GetAccountTypeOptionsAsync(CancellationToken cancellationToken);


    /// Creates a new interest rate rule and returns the saved record.
    Task<InterestRateResponse> CreateInterestRateAsync(CreateInterestRateRequest request, CancellationToken cancellationToken);


    /// Updates an interest rate rule and returns the saved record.
    Task<InterestRateResponse> UpdateInterestRateAsync(long id, UpdateInterestRateRequest request, CancellationToken cancellationToken);
}
