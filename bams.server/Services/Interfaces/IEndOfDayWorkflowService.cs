using bams.server.DTO.Accounting;

namespace bams.server.Services.Interfaces;

public interface IEndOfDayWorkflowService
{
    Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> RunPreCloseAsync(DateOnly date, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> ApproveAsync(long runId, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> CloseAsync(long runId, CancellationToken cancellationToken);
}
