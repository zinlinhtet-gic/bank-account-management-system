using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Operations;

namespace bams.desktop.Services;


/// Operations API calls per-customer interest, fees and fixed-deposit maturity.
public interface IOperationService
{
    ///  one page of interest accruals, newest period first.</summary>
    Task<PagedResponse<InterestOperationResponse>> GetInterestOperationsAsync(
        InterestOperationFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// Loads one page of fees, newest period first.</summary>
    Task<PagedResponse<FeeOperationResponse>> GetFeeOperationsAsync(
        FeeOperationFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// Loads one page of fixed deposits, nearest maturity first.</summary>
    Task<PagedResponse<FixedDepositMaturityResponse>> GetFixedDepositMaturitiesAsync(
        FixedDepositMaturityFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Loads one interest record with its account, customer and linked transactions.</summary>
    Task<InterestOperationDetailResponse> GetInterestOperationByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>Loads one fee record with its account, customer, fee rule and linked transactions.</summary>
    Task<FeeOperationDetailResponse> GetFeeOperationByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>Loads one fixed deposit with its account, customer and monthly interest schedule.</summary>
    Task<FixedDepositMaturityDetailResponse> GetFixedDepositMaturityByIdAsync(long id, CancellationToken cancellationToken);
}
