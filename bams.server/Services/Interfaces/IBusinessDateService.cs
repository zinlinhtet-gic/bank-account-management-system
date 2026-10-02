using bams.server.DTO.Accounting;

namespace bams.server.Services.Interfaces;

public interface IBusinessDateService
{
    Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken);
    Task<DateOnly> GetOpenBusinessDateValueAsync(CancellationToken cancellationToken);
}
