using bams.desktop.Models;

namespace bams.desktop.Services;

/// <summary>
/// Defines account data operations used by account-focused ViewModels.
/// </summary>
public interface IExampleAccountService
{
    /// <summary>
    /// Retrieves example accounts for display.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the pending operation.
    /// </param>
    /// <returns>
    /// A read-only collection of account display models.
    /// </returns>
    Task<IReadOnlyList<ExampleAccountDisplayModel>> GetExampleAccountsAsync(
        CancellationToken cancellationToken);
}
