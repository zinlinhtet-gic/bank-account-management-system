using System.Text.Json;
using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.External;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed partial class UnifiedReconciliationService
{
    // Expose stored findings without rerunning checks or changing the finalized audit.
    public async Task<EndOfDayAuditReport?> AuditAsync(DateOnly date, CancellationToken ct)
    {
        ReconciliationBatch? batch = await db.ReconciliationBatches.AsNoTracking().Where(x => x.ReconciliationDate == date &&
            x.ReconciliationType == AuditConstants.EndOfDayReconciliationType).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (batch is null) return null;
        List<EndOfDayAuditFinding> findings = [];
        decimal? effectiveDebit = null;
        decimal? effectiveCredit = null;
        bool? reconciled = null;
        if (!string.IsNullOrWhiteSpace(batch.FindingsJson))
        {
            using JsonDocument document = JsonDocument.Parse(batch.FindingsJson);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("EffectiveDebit", out JsonElement debit)) effectiveDebit = debit.GetDecimal();
            if (root.TryGetProperty("EffectiveCredit", out JsonElement credit)) effectiveCredit = credit.GetDecimal();
            if (root.TryGetProperty("IsReconciled", out JsonElement matched)) reconciled = matched.GetBoolean();
            AddIds(root, "MissingJournalTransactionIds", "Missing journal", "Posted transaction has no accounting journal.", findings);
            AddIds(root, "WaivedTransactionIds", "Waived transaction", "Excluded from effective validation by an explicit Force Pass.", findings);
            AddIds(root, "WaivedEntryIds", "Waived GL entry", "Excluded from effective validation; raw posting is retained.", findings);
            foreach (string property in new[] { "RawUnbalancedJournals", "UnbalancedJournals" })
            {
                if (!root.TryGetProperty(property, out JsonElement journals)) continue;
                foreach (JsonElement journal in journals.EnumerateArray())
                    findings.Add(new(property == "RawUnbalancedJournals" ? "Raw journal inequality" : "Combined journal inequality",
                        journal.GetProperty("TransactionId").GetInt64(), journal.GetProperty("Debit").GetDecimal(),
                        journal.GetProperty("Credit").GetDecimal(), "Debit and credit differ; raw and linked-repair views are shown separately."));
            }
        }
        else findings.Add(new("Legacy audit", null, null, null, "This audit predates persisted detailed findings."));
        return new(batch.Id, date, batch.Status, batch.TotalDebit, batch.TotalCredit, effectiveDebit, effectiveCredit, reconciled, findings);
    }

    private static void AddIds(JsonElement root, string property, string kind, string detail, List<EndOfDayAuditFinding> findings)
    {
        if (!root.TryGetProperty(property, out JsonElement ids)) return;
        foreach (JsonElement id in ids.EnumerateArray()) findings.Add(new(kind, id.GetInt64(), null, null, detail));
    }
}
