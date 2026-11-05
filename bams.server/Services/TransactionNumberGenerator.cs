using bams.server.Constants;
using bams.server.Data;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Allocates a daily transaction sequence in a separate committed database transaction.</summary>
public sealed class TransactionNumberGenerator(IServiceScopeFactory scopeFactory) : ITransactionNumberGenerator
{
    public async Task<string> ReserveAsync(DateOnly businessDate, bool isScheduled, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // The date column is mapped as DATE; bind a DateTime because the MySQL connector does not bind DateOnly.
        var businessDateValue = businessDate.ToDateTime(TimeOnly.MinValue);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // The update holds the unique date row lock until this reservation is committed.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO `TransactionNumberGenerations` (`BusinessDate`, `LastSequenceNumber`)
            VALUES ({businessDateValue}, 1)
            ON DUPLICATE KEY UPDATE `LastSequenceNumber` = `LastSequenceNumber` + 1
            """, cancellationToken);

        var sequence = await dbContext.TransactionNumberGenerations
            .AsNoTracking()
            .Where(item => item.BusinessDate == businessDate)
            .Select(item => item.LastSequenceNumber)
            .SingleAsync(cancellationToken);

        if (sequence > TransactionConstants.TransactionNumberDailySequenceMaximum)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BusinessRuleException(MessageCode.TransactionNumberSequenceExhausted);
        }

        await transaction.CommitAsync(cancellationToken);
        var prefix = isScheduled
            ? TransactionConstants.ScheduledTransactionNumberPrefix
            : TransactionConstants.TransactionNumberPrefix;
        return $"{prefix}-{businessDate:yyyyMMdd}-{sequence.ToString(TransactionConstants.TransactionNumberSequenceFormat)}";
    }
}
