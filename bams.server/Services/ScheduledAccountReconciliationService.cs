using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using bams.server.Services.Jobs;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Runs account reconciliation for the open business date under the non-login scheduler actor.</summary>
public sealed class ScheduledAccountReconciliationService(
    ApplicationDbContext db,
    IBusinessDateService businessDates,
    IAccountReconciliationService reconciliation) 
{
    /// <summary>Runs the standard reconciliation service for the currently open bank-wide business date.</summary>
    public async Task ExecuteAsync(ScheduledJobExecutionContext execution, CancellationToken cancellationToken)
    {
        var actorId = await db.Users.AsNoTracking()
            .Where(user => user.Username == ScheduledJobConstants.SystemActorUsername && user.Status == UserStatus.Disabled)
            .Select(user => (long?)user.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException(MessageCode.AuditUserUnavailable);
        var date = await businessDates.GetOpenBusinessDateValueAsync(cancellationToken);
        await reconciliation.ReconcileAccountsAsAsync(new AccountReconciliationRequest(date, date, null), actorId,
            execution.ExecutionId, cancellationToken);
    }
}
