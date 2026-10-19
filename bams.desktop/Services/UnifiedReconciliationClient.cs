using bams.desktop.Api;
using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

/// <summary>Shared transport for account/cash checks, investigator tasks, cash proposals and expenses.</summary>
public sealed class UnifiedReconciliationClient(ApiClient api)
{
    private const string Root = "/api/reconciliation";
    public Task<CashSourceCatalog> SourcesAsync(long id, string? search, CancellationToken ct = default) => api.GetAsync<CashSourceCatalog>($"{Root}/cash/exceptions/{id}/sources?search={Uri.EscapeDataString(search ?? "")}", ct);
    public Task<CashProposalEvidence> CashEvidenceAsync(long id, CancellationToken ct = default) => api.GetAsync<CashProposalEvidence>($"{Root}/cash/proposals/{id}/evidence", ct);
    public Task<EndOfDayAuditReport?> AuditAsync(DateOnly date, CancellationToken ct = default) => api.GetOptionalAsync<EndOfDayAuditReport>($"{Root}/audit/{date:yyyy-MM-dd}", ct);
    public Task DownloadExpenseReceiptAsync(long id,string path) => api.DownloadFileAsync($"/api/cash-operations/expenses/{id}/receipt",path,default);
    public Task<UnifiedReconciliationResponse> CheckAsync(DateOnly from, DateOnly to, CancellationToken ct = default) => api.PostAsync<UnifiedReconciliationRequest, UnifiedReconciliationResponse>(Root + "/checks", new(from,to),ct);
    public Task<UnifiedReconciliationResponse> ReadAsync(DateOnly from, DateOnly to, CancellationToken ct = default) => api.GetAsync<UnifiedReconciliationResponse>($"{Root}/exceptions?fromDate={from:yyyy-MM-dd}&toDate={to:yyyy-MM-dd}",ct);
    public Task<IReadOnlyList<UnifiedExceptionRow>> TasksAsync(CancellationToken ct = default) => api.GetAsync<IReadOnlyList<UnifiedExceptionRow>>(Root + "/tasks/mine",ct);
    public Task<bool> AssignAsync(long id, long investigator, string note) => api.PostAsync<AssignReconciliationRequest,bool>($"{Root}/exceptions/{id}/assign",new(investigator,note),default);
    public Task<bool> ForcePassAsync(long id,string reason) => api.PostAsync<ForcePassRequest,bool>($"{Root}/exceptions/{id}/force-pass",new(reason),default);
    public Task<IReadOnlyList<CashDiscrepancyDefinition>> DefinitionsAsync(CancellationToken ct = default) => api.GetAsync<IReadOnlyList<CashDiscrepancyDefinition>>(Root + "/cash/discrepancies",ct);
    public Task<CashCorrectionPreview> PreviewAsync(long id,CashCorrectionInputs input) => api.PostAsync<CashCorrectionInputs,CashCorrectionPreview>($"{Root}/cash/exceptions/{id}/preview",input,default);
    public Task<CashCorrectionProposalResponse> SubmitAsync(long id,CashCorrectionInputs input,string previewHash) => api.PostAsync<SubmitCashCorrectionRequest,CashCorrectionProposalResponse>($"{Root}/cash/exceptions/{id}/proposals",new(input,previewHash),default);
    public Task<IReadOnlyList<CashCorrectionProposalResponse>> PendingAsync(CancellationToken ct = default) => api.GetAsync<IReadOnlyList<CashCorrectionProposalResponse>>(Root + "/cash/proposals/pending",ct);
    public Task<IReadOnlyList<ReconciliationVerificationTask>> VerificationTasksAsync(CancellationToken ct = default) => api.GetAsync<IReadOnlyList<ReconciliationVerificationTask>>(Root + "/verification",ct);
    public Task<CashCorrectionProposalResponse> VerifyCashAsync(long id,CancellationToken ct = default) => api.PostAsync<CashCorrectionProposalResponse>($"{Root}/cash/proposals/{id}/verify",ct);
    public Task<CashCorrectionProposalResponse> ReviewAsync(long id,bool approve,string note) => api.PostAsync<ReviewCorrectionRequest,CashCorrectionProposalResponse>($"{Root}/cash/proposals/{id}/review",new(approve,note),default);
    public Task<bool> FinalCountAsync(DateOnly date,decimal amount,string note) => api.PostAsync<FinalVaultCountRequest,bool>(Root + "/vault/final-count",new(date,amount,note),default);
    public Task<IReadOnlyList<BankExpenseResponse>> ExpensesAsync(CancellationToken ct = default) => api.GetAsync<IReadOnlyList<BankExpenseResponse>>("/api/cash-operations/expenses",ct);
    public Task<BankExpenseResponse> SubmitExpenseAsync(SubmitBankExpenseRequest input) => api.PostAsync<SubmitBankExpenseRequest,BankExpenseResponse>("/api/cash-operations/expenses",input,default);
    public Task<BankExpenseResponse> ReviewExpenseAsync(long id,bool approve,string note) => api.PostAsync<ReviewBankExpenseRequest,BankExpenseResponse>($"/api/cash-operations/expenses/{id}/review",new(approve,note),default);
}

