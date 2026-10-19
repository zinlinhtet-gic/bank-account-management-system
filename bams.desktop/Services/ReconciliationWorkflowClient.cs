using bams.desktop.Api;
using bams.desktop.DTOs.Accounting;
namespace bams.desktop.Services;

/// <summary>Transport boundary for reconciliation operation pages.</summary>
public sealed class ReconciliationWorkflowClient(ApiClient api)
{
    private const string Root = "/api/accounting/reconciliation/workflow";
    public Task<IReadOnlyList<DiscrepancyDefinition>> DefinitionsAsync(CancellationToken ct) => api.GetAsync<IReadOnlyList<DiscrepancyDefinition>>(Root + "/discrepancies", ct);
    public Task<AccountReconciliationRunResponse> CheckAsync(AccountReconciliationRequest input, CancellationToken ct) => api.PostAsync<AccountReconciliationRequest, AccountReconciliationRunResponse>(Root + "/checks", input, ct);
    public Task<bool> AssignAsync(long id, InvestigationRequest input, CancellationToken ct) => api.PostAsync<InvestigationRequest, bool>($"{Root}/exceptions/{id}/investigation", input, ct);
    public Task<CorrectionPreview> PreviewAsync(long id, CorrectionInputs input, CancellationToken ct) => api.PostAsync<CorrectionInputs, CorrectionPreview>($"{Root}/exceptions/{id}/preview", input, ct);
    public Task<CorrectionProposalResponse> SaveAsync(long id, long? draftId, SaveCorrectionRequest input, CancellationToken ct) =>
        api.PostAsync<SaveCorrectionRequest, CorrectionProposalResponse>($"{Root}/exceptions/{id}/proposals" + (draftId.HasValue ? $"/{draftId}" : ""), input, ct);
    public Task<IReadOnlyList<CorrectionProposalResponse>> ProposalsAsync(long? id, CancellationToken ct) => api.GetAsync<IReadOnlyList<CorrectionProposalResponse>>(Root + "/proposals" + (id.HasValue ? $"?exceptionId={id}" : ""), ct);
    public Task<CorrectionProposalResponse> ProposalAsync(long id, CancellationToken ct) => api.GetAsync<CorrectionProposalResponse>($"{Root}/proposals/{id}", ct);
    public Task<CorrectionProposalResponse> ReviewAsync(long id, ReviewCorrectionRequest input, CancellationToken ct) => api.PostAsync<ReviewCorrectionRequest, CorrectionProposalResponse>($"{Root}/proposals/{id}/review", input, ct);
    public Task<CorrectionProposalResponse> VerifyAsync(long id, CancellationToken ct) => api.PostAsync<CorrectionProposalResponse>($"{Root}/proposals/{id}/verify", ct);
    public Task<CorrectionEvidence> EvidenceAsync(long id, CancellationToken ct) => api.GetAsync<CorrectionEvidence>($"{Root}/transactions/{id}/evidence", ct);
    public Task<IReadOnlyList<CorrectionGlOption>> GlOptionsAsync(CancellationToken ct) => api.GetAsync<IReadOnlyList<CorrectionGlOption>>(Root + "/gl-options", ct);
    public Task<IReadOnlyList<SummaryRevisionResponse>> SummariesAsync(DateOnly date, CancellationToken ct) => api.GetAsync<IReadOnlyList<SummaryRevisionResponse>>($"{Root}/summaries?date={date:yyyy-MM-dd}", ct);
    public Task<bool> RefreshSummariesAsync(DateOnly date, CancellationToken ct) => api.PostAsync<bool>($"{Root}/summaries/{date:yyyy-MM-dd}/refresh", ct);
}

