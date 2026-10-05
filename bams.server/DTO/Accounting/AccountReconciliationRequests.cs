namespace bams.server.DTO.Accounting;

/// <summary>Parameters for reconciling one account or all accounts through a business-date cutoff.</summary>
public sealed record AccountReconciliationRequest(DateOnly FromDate, DateOnly ToDate, long? AccountId);

/// <summary>Changes the assignment, notes, or investigation status of a discrepancy.</summary>
public sealed record UpdateReconciliationExceptionRequest(string Status, string? Notes, long? AssignedTo, long? CorrectionTransactionId);
