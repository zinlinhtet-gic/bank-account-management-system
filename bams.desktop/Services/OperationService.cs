using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Operations;

namespace bams.desktop.Services;

public sealed class OperationService : IOperationService
{
    private readonly ApiClient _apiClient;

    public OperationService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<PagedResponse<InterestOperationResponse>> GetInterestOperationsAsync(
        InterestOperationFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = new QueryString()
            .Add("search", filter.Search)
            .Add("status", filter.Status?.ToString())
            .Add("from", filter.From)
            .Add("to", filter.To)
            .Add("page", page)
            .Add("pageSize", pageSize);

        return _apiClient.GetAsync<PagedResponse<InterestOperationResponse>>(
            ApiConstants.OperationInterestEndpoint + query, cancellationToken);
    }

    public Task<PagedResponse<FeeOperationResponse>> GetFeeOperationsAsync(
        FeeOperationFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = new QueryString()
            .Add("search", filter.Search)
            .Add("feeType", filter.FeeType?.ToString())
            .Add("status", filter.Status?.ToString())
            .Add("from", filter.From)
            .Add("to", filter.To)
            .Add("page", page)
            .Add("pageSize", pageSize);

        return _apiClient.GetAsync<PagedResponse<FeeOperationResponse>>(
            ApiConstants.OperationFeesEndpoint + query, cancellationToken);
    }

    public Task<PagedResponse<FixedDepositMaturityResponse>> GetFixedDepositMaturitiesAsync(
        FixedDepositMaturityFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = new QueryString()
            .Add("search", filter.Search)
            .Add("status", filter.Status?.ToString())
            .Add("from", filter.From)
            .Add("to", filter.To)
            .Add("page", page)
            .Add("pageSize", pageSize);

        return _apiClient.GetAsync<PagedResponse<FixedDepositMaturityResponse>>(
            ApiConstants.OperationFixedDepositMaturityEndpoint + query, cancellationToken);
    }

    public Task<InterestOperationDetailResponse> GetInterestOperationByIdAsync(long id, CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<InterestOperationDetailResponse>(
            $"{ApiConstants.OperationInterestEndpoint}/{id}", cancellationToken);
    }

    public Task<FeeOperationDetailResponse> GetFeeOperationByIdAsync(long id, CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<FeeOperationDetailResponse>(
            $"{ApiConstants.OperationFeesEndpoint}/{id}", cancellationToken);
    }

    public Task<FixedDepositMaturityDetailResponse> GetFixedDepositMaturityByIdAsync(long id, CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<FixedDepositMaturityDetailResponse>(
            $"{ApiConstants.OperationFixedDepositMaturityEndpoint}/{id}", cancellationToken);
    }
}
