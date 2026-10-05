using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

public interface IEndOfDayClientService
{
    Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> RunPreCloseAsync(DateOnly date, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> ApproveAsync(long runId, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> CloseAsync(long runId, CancellationToken cancellationToken);
}
