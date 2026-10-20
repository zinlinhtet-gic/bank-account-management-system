using bams.server.DTO.Common;
using bams.server.DTO.Operations;

namespace bams.server.Services.Interfaces;

/// <summary>
/// Read-only, per-customer views of the scheduled operations: interest accruals, fees and fixed-deposit maturity.
/// </summary>
public interface IOperationQueryService
{
    Task<PagedResponse<InterestOperationResponse>> GetInterestOperationsAsync(
        InterestOperationQuery query,
        CancellationToken cancellationToken);

    Task<PagedResponse<FeeOperationResponse>> GetFeeOperationsAsync(
        FeeOperationQuery query,
        CancellationToken cancellationToken);

    Task<PagedResponse<FixedDepositMaturityResponse>> GetFixedDepositMaturitiesAsync(
        FixedDepositMaturityQuery query,
        CancellationToken cancellationToken);

    Task<InterestOperationDetailResponse> GetInterestOperationByIdAsync(long id, CancellationToken cancellationToken);

    Task<FeeOperationDetailResponse> GetFeeOperationByIdAsync(long id, CancellationToken cancellationToken);

    Task<FixedDepositMaturityDetailResponse> GetFixedDepositMaturityByIdAsync(long id, CancellationToken cancellationToken);
}
