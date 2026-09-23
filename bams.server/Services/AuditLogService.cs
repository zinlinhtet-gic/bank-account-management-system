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
    private readonly ICurrentUserService _currentUserService;

    public AuditLogService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    /// <inheritdoc />
    public Task RecordAccountOpeningLogAsync(Account account, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(AuditConstants.AccountOpenedAction, nameof(Account), account.Id, null,
            new { account.AccountNo, account.AccountTypeId, account.Status, account.AvailableBalance }, performedAt, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordAccountBalanceUpdateLogAsync(long accountId, decimal oldBalance, decimal newBalance, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(AuditConstants.AccountBalanceUpdatedAction, nameof(Account), accountId,
            new { Balance = oldBalance }, new { Balance = newBalance }, performedAt, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordAccountStatusUpdateLogAsync(long accountId, AccountStatus oldStatus, AccountStatus newStatus, string? reason, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(AuditConstants.AccountStatusUpdatedAction, nameof(Account), accountId,
            new { Status = oldStatus }, new { Status = newStatus, Reason = reason }, performedAt, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordAccountHolderUpdateLogAsync(long accountId, IReadOnlyList<AccountHolderResponse> oldHolders, IReadOnlyList<AccountHolderResponse> newHolders, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(AuditConstants.AccountHoldersUpdatedAction, nameof(Account), accountId,
            oldHolders, newHolders, performedAt, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordFixedDepositCreationLogAsync(FixedDepositResponse fixedDeposit, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(AuditConstants.FixedDepositCreatedAction, nameof(FixedDeposit), fixedDeposit.Id,
            null, fixedDeposit, performedAt, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordFixedDepositUpdateLogAsync(FixedDepositResponse oldFixedDeposit, FixedDepositResponse newFixedDeposit, DateTime performedAt, CancellationToken cancellationToken)
    {
        return AddAuditLogAsync(AuditConstants.FixedDepositUpdatedAction, nameof(FixedDeposit), newFixedDeposit.Id,
            oldFixedDeposit, newFixedDeposit, performedAt, cancellationToken);
    }

    // Tracks an audit entry so the owning operation can persist it atomically with its write.
    private async Task AddAuditLogAsync(string action, string entityType, long entityId, object? oldValues, object? newValues, DateTime createdAt, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetCurrentUserId();
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
