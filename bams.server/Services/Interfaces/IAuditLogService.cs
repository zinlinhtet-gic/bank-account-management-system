using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;

namespace bams.server.Services.Interfaces;

public interface IAuditLogService
{
    /// <summary>
    /// Creates a new account from the API request contract.
    /// </summary>
    Task RecordAccountOpeningLogAsync(
        Account account,
        DateTime performedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records an audit entry for an account balance update.
    /// </summary>
    Task RecordAccountBalanceUpdateLogAsync(
        long accountId,
        decimal oldBalance,
        decimal newBalance,
        DateTime performedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records an audit entry for an account status update.
    /// </summary>
    Task RecordAccountStatusUpdateLogAsync(
        long accountId,
        AccountStatus oldStatus,
        AccountStatus newStatus,
        string? reason,
        DateTime performedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the before-and-after state of an account holder update.
    /// </summary>
    Task RecordAccountHolderUpdateLogAsync(
        long accountId,
        IReadOnlyList<AccountHolderResponse> oldHolders,
        IReadOnlyList<AccountHolderResponse> newHolders,
        DateTime performedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records creation of a fixed-deposit row.
    /// </summary>
    Task RecordFixedDepositCreationLogAsync(
        FixedDepositResponse fixedDeposit,
        DateTime performedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the before-and-after state of a fixed-deposit update.
    /// </summary>
    Task RecordFixedDepositUpdateLogAsync(
        FixedDepositResponse oldFixedDeposit,
        FixedDepositResponse newFixedDeposit,
        DateTime performedAt,
        CancellationToken cancellationToken);

}
