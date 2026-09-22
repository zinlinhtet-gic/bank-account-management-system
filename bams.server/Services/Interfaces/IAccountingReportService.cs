using bams.server.DTO.Accounting;
namespace bams.server.Services.Interfaces;
/// <summary>
/// Provides read-only access to general-ledger accounts
/// and generated accounting summaries.
/// </summary>
public interface IAccountingReportService
{
    /// <summary>
    /// Retrieves all general-ledger accounts.
    /// </summary>
    Task<IReadOnlyList<GlAccountResponse>> GetGlAccountsAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Retrieves a general-ledger account by its identifier.
    /// </summary>
    Task<GlAccountResponse> GetGlAccountByIdAsync(long glAccountId, CancellationToken cancellationToken);
    /// <summary>
    /// Retrieves daily accounting summaries for the specified date
    /// and optional GL account.
    /// </summary>
    Task<IReadOnlyList<DailySummaryResponse>> GetDailySummariesAsync(DateOnly summaryDate, long? glAccountId, CancellationToken cancellationToken);
    /// <summary>
    /// Retrieves monthly accounting summaries for the specified period
    /// and optional GL account.
    /// </summary>
    Task<IReadOnlyList<MonthlySummaryResponse>> GetMonthlySummariesAsync(int year, int month, long? glAccountId, CancellationToken cancellationToken);
}