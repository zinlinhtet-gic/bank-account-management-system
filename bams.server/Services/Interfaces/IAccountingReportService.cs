using bams.server.DTO.Accounts;

namespace bams.server.Services.Interfaces;

public interface IAccountingReportService
{
    

    /// <summary>
    /// Creates a new account from the API request contract.
    /// </summary>
    Task RecordAccountOpeningTransactionAsync(
        decimal openingBalance,
        DateTime currentDateTime,
        CancellationToken cancellationToken);

}