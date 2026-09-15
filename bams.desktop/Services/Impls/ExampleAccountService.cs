using bams.desktop.Models;

namespace bams.desktop.Services.Impls;

/// <summary>
/// Provides temporary sample account data until the real API service is wired in.
/// </summary>
public sealed class ExampleAccountService : IExampleAccountService
{
    // Returns stable sample data without calling HTTP directly from a ViewModel.
    public Task<IReadOnlyList<ExampleAccountDisplayModel>> GetExampleAccountsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ExampleAccountDisplayModel> accounts =
        [
            new ExampleAccountDisplayModel
            {
                Id = 1,
                AccountNumber = "EX-0001",
                CustomerName = "Sample Customer",
                Balance = 1000m
            }
        ];

        return Task.FromResult(accounts);
    }
}
