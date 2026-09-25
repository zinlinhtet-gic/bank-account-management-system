using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Transactions;

namespace bams.desktop.Services;

/// <summary>
/// Transaction API calls. Transport, token and error translation are handled by <see cref="ApiClient"/>.
/// </summary>
public sealed class TransactionService : ITransactionService
{
    private readonly ApiClient _apiClient;

    public TransactionService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<PagedResponse<TransactionSummaryResponse>> GetTransactionsAsync(
        TransactionListFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = new QueryString()
            .Add("accountNo", filter.AccountNo)
            .Add("type", filter.Type?.ToString())
            .Add("status", filter.Status?.ToString())
            .Add("from", filter.From)
            .Add("before", filter.Before)
            .Add("page", page)
            .Add("pageSize", pageSize);

        return _apiClient.GetAsync<PagedResponse<TransactionSummaryResponse>>(
            ApiConstants.TransactionsEndpoint + query,
            cancellationToken);
    }

    public Task<TransactionDetailResponse> GetTransactionByIdAsync(long id, CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<TransactionDetailResponse>($"{ApiConstants.TransactionsEndpoint}/{id}", cancellationToken);
    }

    public Task<PagedResponse<AccountStatementLineResponse>> GetAccountStatementAsync(
        long accountId,
        DateTimeOffset? from,
        DateTimeOffset? before,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = new QueryString()
            .Add("from", from)
            .Add("before", before)
            .Add("page", page)
            .Add("pageSize", pageSize);

        return _apiClient.GetAsync<PagedResponse<AccountStatementLineResponse>>(
            $"{ApiConstants.TransactionAccountsEndpoint}/{accountId}/{ApiConstants.StatementSegment}{query}",
            cancellationToken);
    }

    public Task<IReadOnlyList<BranchResponse>> GetBranchesAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<BranchResponse>>(ApiConstants.BranchesEndpoint, cancellationToken);
    }

    public Task<IReadOnlyList<OtherBankResponse>> GetOtherBanksAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<OtherBankResponse>>(ApiConstants.OtherBanksEndpoint, cancellationToken);
    }

    public Task<TransactionResponse> DepositAsync(DepositRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        return PostIdempotentAsync(ApiConstants.DepositEndpoint, request, idempotencyKey, cancellationToken);
    }

    public Task<TransactionResponse> WithdrawAsync(WithdrawalRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        return PostIdempotentAsync(ApiConstants.WithdrawalEndpoint, request, idempotencyKey, cancellationToken);
    }

    public Task<TransactionResponse> TransferInternallyAsync(
        InternalTransferRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return PostIdempotentAsync(ApiConstants.InternalTransferEndpoint, request, idempotencyKey, cancellationToken);
    }

    public Task<TransactionResponse> TransferInterbankAsync(
        InterbankTransferRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return PostIdempotentAsync(ApiConstants.InterbankTransferEndpoint, request, idempotencyKey, cancellationToken);
    }

    public Task<TransactionResponse> CreateNrcTransferAsync(
        NrcTransferRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return PostIdempotentAsync(ApiConstants.NrcTransferEndpoint, request, idempotencyKey, cancellationToken);
    }

    public Task<TransactionResponse> CompleteNrcPickupAsync(NrcPickupRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<NrcPickupRequest, TransactionResponse>(
            ApiConstants.NrcPickupEndpoint,
            request,
            cancellationToken);
    }

    public Task<TransactionResponse> RecordNrcPayoutAsync(long id, NrcPayoutRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<NrcPayoutRequest, TransactionResponse>(
            $"{ApiConstants.NrcTransferEndpoint}/{id}/{ApiConstants.PaidOutSegment}",
            request,
            cancellationToken);
    }

    public Task<TransactionResponse> CancelNrcTransferAsync(long id, NrcCancelRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<NrcCancelRequest, TransactionResponse>(
            $"{ApiConstants.NrcTransferEndpoint}/{id}/{ApiConstants.CancelSegment}",
            request,
            cancellationToken);
    }

    public Task<TransactionResponse> CompleteInterbankTransferAsync(
        long id,
        InterbankSettlementRequest request,
        CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<InterbankSettlementRequest, TransactionResponse>(
            $"{ApiConstants.InterbankTransferEndpoint}/{id}/{ApiConstants.CompleteSegment}",
            request,
            cancellationToken);
    }

    public Task<TransactionResponse> FailInterbankTransferAsync(
        long id,
        InterbankFailureRequest request,
        CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<InterbankFailureRequest, TransactionResponse>(
            $"{ApiConstants.InterbankTransferEndpoint}/{id}/{ApiConstants.FailSegment}",
            request,
            cancellationToken);
    }

    // Posts with the Idempotency-Key header, so a retry of the same user action returns the first result.
    private Task<TransactionResponse> PostIdempotentAsync<TRequest>(
        string endpoint,
        TRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string> { [ApiConstants.IdempotencyKeyHeader] = idempotencyKey };

        return _apiClient.PostAsync<TRequest, TransactionResponse>(endpoint, request, headers, cancellationToken);
    }
}
