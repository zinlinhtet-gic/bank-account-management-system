using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

public interface IEndOfDayClientService
{
    Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BusinessDateResponse>> SearchBusinessDatesAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse?> GetLatestRunAsync(DateOnly date, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> RunPreCloseAsync(DateOnly date, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> ReviewPreCloseAsync(DateOnly date, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> RequestForceCloseAsync(DateOnly date, ForceCloseRequest request, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> ApproveForceCloseAsync(long runId, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> ApproveAsync(long runId, CancellationToken cancellationToken);
    Task<EndOfDayRunResponse> CloseAsync(long runId, CancellationToken cancellationToken);
}
