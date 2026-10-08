using bams.server.DTO.Accounting;

namespace bams.server.Services.Interfaces;

public interface IEndOfDayWorkflowService
{
    Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BusinessDateResponse>> SearchBusinessDatesAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse?> GetLatestRunAsync(DateOnly date, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> RunPreCloseAsync(DateOnly date, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> RequestForceCloseAsync(DateOnly date, string reason, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> ApproveForceCloseAsync(long runId, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> ApproveAsync(long runId, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> CloseAsync(long runId, CancellationToken cancellationToken);
}
