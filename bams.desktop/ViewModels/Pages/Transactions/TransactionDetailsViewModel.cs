using bams.desktop.Commands;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// One account entry of the detail card, e.g. "ACC… · Debit · 1,000.00 · balance 49,000.00".
/// </summary>
public sealed record AccountEntryDisplayModel(
    long AccountId,
    string AccountNo,
    bool IsCredit,
    string AmountText,
    string BalanceBeforeText,
    string BalanceAfterText,
    string AvailableBalanceBeforeText,
    string AvailableBalanceAfterText,
    string ValueDateText,
    string PostingDateText,
    string DescriptionText,
    string ReferenceText)
{
    public string EntryTypeText => IsCredit ? "Credit" : "Debit";

    public string BalanceSummaryText => $"Ledger {BalanceBeforeText} → {BalanceAfterText} · available {AvailableBalanceBeforeText} → {AvailableBalanceAfterText}";

    public string EntryDatesText => $"Value {ValueDateText} · posted {PostingDateText}";

    public string DescriptionSummaryText => $"{DescriptionText} · {ReferenceText}";

    /// <summary>Builds a line from the server entry.</summary>
    public static AccountEntryDisplayModel FromResponse(AccountEntryResponse entry)
    {
        return new AccountEntryDisplayModel(
            entry.AccountId,
            entry.AccountNo,
            entry.EntryType == EntryType.Credit,
            TransactionDisplay.FormatAmount(entry.Amount),
            TransactionDisplay.FormatAmount(entry.LedgerBalanceBefore),
            TransactionDisplay.FormatAmount(entry.LedgerBalanceAfter),
            TransactionDisplay.FormatAmount(entry.AvailableBalanceBefore),
            TransactionDisplay.FormatAmount(entry.AvailableBalanceAfter),
            entry.ValueDate.ToString(Constants.DisplayFormats.Date),
            entry.PostingDate.ToString(Constants.DisplayFormats.Date),
            TransactionDisplay.OrDash(entry.Description),
            TransactionDisplay.OrDash(entry.ReferenceNo));
    }
}

public sealed record AccountingEntryDisplayModel(
    string AccountCode,
    string AccountName,
    string? CustomerAccountNo,
    string EntryTypeText,
    string AmountText,
    string PostingDateText,
    string CreatedAtText,
    string DescriptionText)
{
    public string EntrySummaryText => $"{EntryTypeText} · {PostingDateText} · {CreatedAtText} · {TransactionDisplay.OrDash(CustomerAccountNo)} · {DescriptionText}";

    public static AccountingEntryDisplayModel FromResponse(TransactionAccountingEntryResponse entry) => new(
        entry.GlAccountCode,
        entry.GlAccountName,
        entry.CustomerAccountNo,
        entry.EntryType.ToString(),
        TransactionDisplay.FormatAmount(entry.Amount),
        entry.PostingDate.ToString(Constants.DisplayFormats.Date),
        TransactionDisplay.FormatTimestamp(entry.CreatedAt),
        TransactionDisplay.OrDash(entry.Description));
}

public sealed record AuditLogDisplayModel(
    string Action,
    string UserText,
    string DetailsText,
    string TimestampText,
    string SourceText)
{
    public static AuditLogDisplayModel FromResponse(TransactionAuditLogResponse log) => new(
        log.Action,
        FormatActor(log.FullName, log.Username),
        TransactionDisplay.OrDash(log.Details),
        TransactionDisplay.FormatTimestamp(log.CreatedAt),
        TransactionDisplay.OrDash(string.Join(" · ", new[] { log.IpAddress, log.DeviceInfo }
            .OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value)))));

    private static string FormatActor(string fullName, string? username) =>
        string.IsNullOrWhiteSpace(username) ? fullName : $"{fullName} ({username})";
}

/// <summary>
/// Read-only detail card for one transaction, opened by clicking a table row: header, account entries and the
/// NRC or interbank detail. The pickup code is never shown here. Closes with <c>true</c> when the user asks for an
/// entry's account statement (<see cref="RequestedStatement"/>); the caller then opens the statement.
/// </summary>
public sealed class TransactionDetailsViewModel : ViewModelBase, IDialogViewModel
{
    public TransactionDetailsViewModel(TransactionDetailResponse transaction)
    {
        Transaction = transaction;
        Entries = transaction.AccountEntries.Select(AccountEntryDisplayModel.FromResponse).ToList();
        AccountingEntries = transaction.AccountingEntries.Select(AccountingEntryDisplayModel.FromResponse).ToList();
        AuditLogs = transaction.AuditLogs.Select(AuditLogDisplayModel.FromResponse).ToList();

        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        ShowStatementCommand = new RelayCommand(RequestStatement);
    }

    public event Action<bool>? CloseRequested;

    // Nothing to lose on a detail card, so clicking outside closes it.
    public bool CanCloseOnBackdropClick => true;

    public bool CanCancel => true;

    public TransactionDetailResponse Transaction { get; }

    public IReadOnlyList<AccountEntryDisplayModel> Entries { get; }

    public IReadOnlyList<AccountingEntryDisplayModel> AccountingEntries { get; }

    public IReadOnlyList<AuditLogDisplayModel> AuditLogs { get; }

    public RelayCommand CloseCommand { get; }

    /// <summary>Entry button: parameter is the <see cref="AccountEntryDisplayModel"/> whose statement to open.</summary>
    public RelayCommand ShowStatementCommand { get; }

    /// <summary>The entry whose account statement was asked for, set just before the card closes with <c>true</c>.</summary>
    public AccountEntryDisplayModel? RequestedStatement { get; private set; }

    public string TypeText => TransactionDisplay.ToDisplayName(Transaction.TransactionType);

    public TransactionStatus Status => Transaction.TransactionStatus;

    public string StatusText => TransactionDisplay.ToDisplayName(Status);

    public string AmountText => TransactionDisplay.FormatMoney(Transaction.Amount);

    public string FeeAmountText => TransactionDisplay.FormatMoney(Transaction.FeeAmount);

    public bool HasFee => Transaction.FeeAmount != 0m;

    public string TransactionAtText => TransactionDisplay.FormatTimestamp(Transaction.TransactionAt);

    public string PostedAtText => TransactionDisplay.FormatTimestamp(Transaction.PostedAt);

    public string CreatedAtText => TransactionDisplay.FormatTimestamp(Transaction.CreatedAt);

    public string UpdatedAtText => TransactionDisplay.FormatTimestamp(Transaction.UpdatedAt);

    public string BusinessDateText => Transaction.BusinessDate?.ToString(Constants.DisplayFormats.Date)
        ?? Constants.DisplayFormats.EmptyValue;

    public string InitiatedByText => FormatActor(Transaction.InitiatedByFullName, Transaction.InitiatedByUsername);

    public string AuthorizedByText => FormatActor(Transaction.AuthorizedByFullName, Transaction.AuthorizedByUsername);

    public string AuthorizedByDisplayText => Transaction.AuthorizedAt is { } authorizedAt
        ? $"{AuthorizedByText} · {TransactionDisplay.FormatTimestamp(authorizedAt)}"
        : AuthorizedByText;

    public string PostedByText => FormatActor(Transaction.PostedByFullName, Transaction.PostedByUsername);

    public string PostedByDisplayText => Transaction.PostedAt is { } postedAt
        ? $"{PostedByText} · {TransactionDisplay.FormatTimestamp(postedAt)}"
        : PostedByText;

    public string ReferenceText => TransactionDisplay.OrDash(Transaction.ReferenceNo);

    public string DescriptionText => TransactionDisplay.OrDash(Transaction.Description);

    public bool HasEntries => Entries.Count > 0;

    public bool HasAccountingEntries => AccountingEntries.Count > 0;

    public bool HasAuditLogs => AuditLogs.Count > 0;

    public bool HasAuthorizedBy => Transaction.AuthorizedBy is not null;

    public bool HasPostedBy => Transaction.PostedBy is not null;

    /// <summary>Refunds point back to the transfer they reversed.</summary>
    public bool IsRefund => Transaction.ReversalOfTransactionId is not null;

    public string RefundOfText => $"Refund of transaction #{Transaction.ReversalOfTransactionId}";

    // ===== NRC transfer =====

    public bool HasNrcTransfer => Transaction.NrcTransfer is not null;

    public string SenderText => Transaction.NrcTransfer is { } nrc
        ? $"{nrc.SenderName} · {nrc.SenderNrc}{PhoneSuffix(nrc.SenderPhone)}"
        : string.Empty;

    public string ReceiverText => Transaction.NrcTransfer is { } nrc
        ? $"{nrc.ReceiverName} · {nrc.ReceiverNrc}{PhoneSuffix(nrc.ReceiverPhone)}"
        : string.Empty;

    /// <summary>e.g. "Mandalay Branch" or "KBZ Bank (another bank)".</summary>
    public string PickupLocationText => Transaction.NrcTransfer is { } nrc
        ? nrc.DeliveryType == Constants.TransactionFieldRules.NrcDeliveryAtOtherBank
            ? $"{nrc.PickupLocation} (another bank)"
            : nrc.PickupLocation
        : string.Empty;

    /// <summary>How the sender paid, and how the receiver was paid once collected.</summary>
    public string PaymentText => Transaction.NrcTransfer is { } nrc
        ? (nrc.IsPaidInCash ? "Sender paid in cash" : "Sender paid from their account")
          + (nrc.PickedUpAt is null ? string.Empty : nrc.DestinationAccountId is null ? " · paid out in cash" : " · paid into the receiver's account")
        : string.Empty;

    /// <summary>e.g. "Waiting for pickup until 26 Sep 2026, 09:12 · 1 wrong code".</summary>
    public string PickupText
    {
        get
        {
            if (Transaction.NrcTransfer is not { } nrc)
            {
                return string.Empty;
            }

            var state = nrc.PickedUpAt is not null
                ? $"Paid out {TransactionDisplay.FormatTimestamp(nrc.PickedUpAt)}"
                : Status == TransactionStatus.Pending
                    ? $"Waiting for pickup until {TransactionDisplay.FormatTimestamp(nrc.PickupExpiresAt)}"
                    : "Cancelled, sender refunded";

            return nrc.FailedPickupAttempts == 0
                ? state
                : $"{state} · {nrc.FailedPickupAttempts} wrong code{(nrc.FailedPickupAttempts == 1 ? string.Empty : "s")}";
        }
    }

    // ===== Interbank transfer =====

    public bool HasInterbankTransfer => Transaction.InterbankTransfer is not null;

    public string BeneficiaryText => Transaction.InterbankTransfer is { } bank
        ? $"{bank.BeneficiaryName} · {bank.DestinationAccountNo} at {bank.OtherBankName}"
        : string.Empty;

    public string GatewayText => Transaction.InterbankTransfer is { } bank
        ? $"{bank.GatewayStatus}{ReferenceSuffix(bank.GatewayReference)}"
        : string.Empty;

    public string SettlementText => TransactionDisplay.OrDash(Transaction.InterbankTransfer?.SettlementReference);

    // Closes the card so the caller can open the chosen account's statement in its place.
    private void RequestStatement(object? parameter)
    {
        if (parameter is AccountEntryDisplayModel entry)
        {
            RequestedStatement = entry;
            CloseRequested?.Invoke(true);
        }
    }

    // " · 09 123 456" when a phone number was given.
    private static string PhoneSuffix(string? phone)
    {
        return string.IsNullOrWhiteSpace(phone) ? string.Empty : $" · {phone}";
    }

    // " · ref GW-001" when the gateway returned a reference.
    private static string ReferenceSuffix(string? reference)
    {
        return string.IsNullOrWhiteSpace(reference) ? string.Empty : $" · ref {reference}";
    }

    private static string FormatActor(string? fullName, string? username)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return TransactionDisplay.OrDash(username);
        }

        return string.IsNullOrWhiteSpace(username) ? fullName : $"{fullName} ({username})";
    }
}
