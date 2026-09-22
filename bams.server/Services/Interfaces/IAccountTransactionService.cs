using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;

namespace bams.server.Services.Interfaces;

public interface IAccountTransactionService
{
    

    /// <summary>
    /// Creates a new account from the API request contract.
    /// </summary>
    Task RecordAccountOpeningTransactionAsync(
        Account account,
        decimal openingBalance,
        DateTime currentDateTime,
        CancellationToken cancellationToken);

}
