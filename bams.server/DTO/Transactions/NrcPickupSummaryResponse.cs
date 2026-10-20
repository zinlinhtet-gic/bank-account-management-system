namespace bams.server.DTO.Transactions;

/// <summary>
/// One NRC transfer waiting to be collected at one of our branches. The receiver's NRC and the pickup code are never
/// returned: the officer still reads the identity from the collector's NRC card and the server checks it.
/// </summary>
/// <param name="IsExpired">The pickup code expired; the transfer can only be reissued a code or cancelled.</param>
/// <param name="IsBlocked">Too many failed pickups; the transfer can only be reissued a code or cancelled.</param>
public sealed record NrcPickupSummaryResponse(
    long TransactionId,
    string TransactionNo,
    decimal Amount,
    string SenderName,
    string ReceiverName,
    string PickupBranchName,
    DateTime TransactionAt,
    DateTime? PickupExpiresAt,
    int FailedPickupAttempts,
    bool IsExpired,
    bool IsBlocked);
