using bams.server.DTO.Accounts;

namespace bams.server.Services.Interfaces;

/// <summary>Reads status history for customer accounts.</summary>
public interface IAccountStatusHistoryService
{
    /// <summary>Gets the status changes for an account, newest first.</summary>
    Task<IReadOnlyList<AccountStatusHistoryResponse>> GetAccountStatusHistoryAsync(long accountId, CancellationToken cancellationToken);
}
