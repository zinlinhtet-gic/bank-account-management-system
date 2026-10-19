using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed partial class CashOperationsService
{
    /// <summary>Serializes manager batches on today's vault and replays matching submissions.</summary>
    public async Task<IReadOnlyList<CashPositionSessionResponse>> CreateSchedulesAsync(CreateSessionSchedulesRequest request, CancellationToken cancellationToken)
    {
        if (request.ExpectedDate.HasValue && request.ExpectedDate != BusinessTime.Today) throw new ConflictException(MessageCode.BusinessDateTransitionConflict);
        var actorId = currentUser.GetCurrentUserId();
        await EnsureVaultPermissionAsync(actorId, cancellationToken);
        var key = NormalizeCashIdempotencyKey(request.IdempotencyKey);
        if (key.Length > TransactionConstants.IdempotencyKeyMaximumLength - 12 || request.Sessions is null || request.Sessions.Count == 0)
            throw new ValidationException(MessageCode.InvalidRequest);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var date = await businessDates.GetPostingBusinessDateValueAsync(DateTime.UtcNow, cancellationToken);
        await LockCashDateAsync(date, cancellationToken);
        var vault = (await db.CashPositionSessions.FromSql($"SELECT * FROM CashPositionSessions WHERE BusinessDate = {date} AND PositionType = {OperationsConstants.CashPositionVault} AND Status = {OperationsConstants.CashSessionOpen} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault() ?? throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        var previous = await db.CashPositionSessions.Where(item => item.OpenedBy == actorId && item.OpenIdempotencyKey != null && item.OpenIdempotencyKey.StartsWith(key + ":"))
            .OrderBy(item => item.Id).ToListAsync(cancellationToken);
        if (previous.Count > 0)
        {
            if (previous.Count != request.Sessions.Count) throw new ConflictException(MessageCode.IdempotencyKeyReused);
            for (int index = 0; index < previous.Count; index++)
            {
                var row = request.Sessions[index];
                if (!await MatchesOpenRequestAsync(previous[index], new OpenCashSessionRequest(OperationsConstants.CashPositionTeller,
                    row.StartingCash, row.OfficerId, row.StartAtMyanmar, row.EndAtMyanmar), cancellationToken))
                    throw new ConflictException(MessageCode.IdempotencyKeyReused);
            }
            return previous.Select(ToResponse).ToList();
        }
        var created = await scheduling.CreateAsync(request.Sessions, vault, actorId, key, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created.Select(ToResponse).ToList();
    }

    /// <summary>Older-date cash may go only to manager or auditor custody; officers need a same-date active session.</summary>
    private async Task ValidateHandoffDestinationAsync(CashPositionSession source, long recipientId, CancellationToken cancellationToken)
    {
        if (await HasVaultPermissionAsync(recipientId, cancellationToken) || await HasAuditPermissionAsync(recipientId, cancellationToken)) return;
        if (source.BusinessDate != BusinessTime.Today || !await db.CashPositionSessions.AnyAsync(item => item.TellerId == recipientId &&
                item.BusinessDate == source.BusinessDate && item.PositionType == OperationsConstants.CashPositionTeller &&
                item.Status == OperationsConstants.CashSessionOpen, cancellationToken))
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
    }
}
