using System.Globalization;
using System.Text.Json;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Audit;
using bams.server.Services.Interfaces;

namespace bams.server.Services;

public sealed class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _dbContext;

    public AuditLogService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task RecordAccountOpeningLogAsync(Account account, long performedBy, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(performedBy, AuditConstants.AccountOpenedAction, nameof(Account), account.Id, null,
            new { account.AccountNo, account.AccountTypeId, account.Status, account.AvailableBalance }, performedAt, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordAccountBalanceUpdateLogAsync(long accountId, decimal oldBalance, decimal newBalance, long performedBy, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(performedBy, AuditConstants.AccountBalanceUpdatedAction, nameof(Account), accountId,
            new { Balance = oldBalance }, new { Balance = newBalance }, performedAt, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordAccountStatusUpdateLogAsync(long accountId, AccountStatus oldStatus, AccountStatus newStatus, string? reason, long performedBy, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(performedBy, AuditConstants.AccountStatusUpdatedAction, nameof(Account), accountId,
            new { Status = oldStatus }, new { Status = newStatus, Reason = reason }, performedAt, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordAccountHolderUpdateLogAsync(long accountId, IReadOnlyList<AccountHolderResponse> oldHolders, IReadOnlyList<AccountHolderResponse> newHolders, long performedBy, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(performedBy, AuditConstants.AccountHoldersUpdatedAction, nameof(Account), accountId,
            oldHolders, newHolders, performedAt, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordFixedDepositCreationLogAsync(FixedDepositResponse fixedDeposit, long performedBy, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(performedBy, AuditConstants.FixedDepositCreatedAction, nameof(FixedDeposit), fixedDeposit.Id,
            null, fixedDeposit, performedAt, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordFixedDepositUpdateLogAsync(FixedDepositResponse oldFixedDeposit, FixedDepositResponse newFixedDeposit, long performedBy, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(performedBy, AuditConstants.FixedDepositUpdatedAction, nameof(FixedDeposit), newFixedDeposit.Id,
            oldFixedDeposit, newFixedDeposit, performedAt, cancellationToken);
    }

    // Tracks an audit entry so the owning operation can persist it atomically with its write.
    private async Task AddAuditLogAsync(long userId, string action, string entityType, long entityId, object? oldValues, object? newValues, DateTime createdAt, CancellationToken cancellationToken)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(CultureInfo.InvariantCulture),
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            CreatedAt = createdAt
        };

        await _dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
    }
}
