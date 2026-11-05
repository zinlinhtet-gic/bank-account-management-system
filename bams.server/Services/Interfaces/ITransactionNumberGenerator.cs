namespace bams.server.Services.Interfaces;

/// <summary>Reserves globally unique, customer-readable transaction numbers.</summary>
public interface ITransactionNumberGenerator
{
    Task<string> ReserveAsync(DateOnly businessDate, bool isScheduled, CancellationToken cancellationToken);
}
