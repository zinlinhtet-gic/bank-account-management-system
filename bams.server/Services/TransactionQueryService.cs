using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Transactions;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>
/// Read-only transaction queries: list, detail and account statement. Nothing here changes data.
/// </summary>
public sealed class TransactionQueryService : ITransactionQueryService
{
    private readonly ApplicationDbContext _dbContext;

    public TransactionQueryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lists transactions matching the filters, newest first, one page at a time.
    /// </summary>
    public async Task<PagedResponse<TransactionSummaryResponse>> GetTransactionsAsync(
        TransactionListQuery query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = TransactionRequestValidator.ResolvePaging(query.Page, query.PageSize);
        TransactionRequestValidator.ValidateDateRange(query.From, query.Before);

        var transactions = _dbContext.Transactions.AsNoTracking();

        // Each filter is optional; only the ones provided narrow the list.
        if (query.AccountId is { } accountId)
        {
            transactions = transactions.Where(transaction => _dbContext.AccountTransactions
                .Any(entry => entry.TransactionId == transaction.Id && entry.AccountId == accountId));
        }

        // Staff know accounts by number; auditors cannot list accounts to look up the id.
        if (TransactionRequestValidator.TrimToNull(query.AccountNo) is { } accountNo)
        {
            transactions = transactions.Where(transaction => _dbContext.AccountTransactions
                .Any(entry => entry.TransactionId == transaction.Id && entry.Account!.AccountNo == accountNo));
        }

        // Allows auditors to locate one transaction directly by its stable transaction number.
        if (TransactionRequestValidator.TrimToNull(query.TransactionNo)
            is { } transactionNo)
        {
            transactions = transactions.Where(
                transaction =>
                    transaction.TransactionNo == transactionNo);
        }

        // Allows auditors to locate transactions using an external or operator-entered reference.
        if (TransactionRequestValidator.TrimToNull(query.ReferenceNo)
            is { } referenceNo)
        {
            transactions = transactions.Where(
                transaction =>
                    transaction.ReferenceNo == referenceNo);
        }

        if (query.Type is { } type)
        {
            transactions = transactions.Where(transaction => transaction.TransactionType == type);
        }

        if (query.Status is { } status)
        {
            transactions = transactions.Where(transaction => transaction.TransactionStatus == status);
        }

        if (query.From is { } from)
        {
            var fromUtc = from.UtcDateTime;
            transactions = transactions.Where(transaction => transaction.TransactionAt >= fromUtc);
        }

        if (query.Before is { } before)
        {
            var beforeUtc = before.UtcDateTime;
            transactions = transactions.Where(transaction => transaction.TransactionAt < beforeUtc);
        }

        var totalCount = await transactions.CountAsync(cancellationToken);
        var TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var items = await transactions
            .OrderByDescending(transaction => transaction.TransactionAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(transaction => new TransactionSummaryResponse(
                transaction.Id,
                transaction.TransactionNo,
                transaction.TransactionType,
                transaction.TransactionStatus,
                transaction.Amount,
                transaction.TransactionAt,
                transaction.Description,
                transaction.ReferenceNo,
                transaction.InitiatedBy,
                transaction.InitiatedByUser != null
                    ? transaction.InitiatedByUser.Username
                    : string.Empty,
                transaction.InitiatedByUser != null
                    ? transaction.InitiatedByUser.FullName
                    : string.Empty)
            {
                BusinessDate = transaction.BusinessDate
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<TransactionSummaryResponse>(items, page, pageSize, totalCount, TotalPages);
    }

    /// <summary>
    /// Lists the banks an interbank transfer can be sent to, sorted by name.
    /// </summary>
    public async Task<IReadOnlyList<OtherBankResponse>> GetOtherBanksAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.OtherBanks
            .AsNoTracking()
            .OrderBy(bank => bank.BankName)
            .Select(bank => new OtherBankResponse(bank.Id, bank.BankCode, bank.BankName))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the active branches where NRC transfers can be collected, sorted by name.
    /// </summary>
    public async Task<IReadOnlyList<BranchResponse>> GetBranchesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.Status == BranchConstants.ActiveStatus)
            .OrderBy(branch => branch.Name)
            .Select(branch => new BranchResponse(branch.Id, branch.Code, branch.Name, branch.City))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets one transaction with its account entries and its NRC or interbank detail.
    /// </summary>
    public async Task<TransactionDetailResponse> GetTransactionByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.Transactions
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.TransactionNo,
                item.TransactionType,
                item.TransactionStatus,
                item.Amount,
                item.FeeAmount,
                item.TransactionAt,
                item.BusinessDate,
                item.Description,
                item.ReferenceNo,

                item.InitiatedBy,
                InitiatedByUsername = item.InitiatedByUser != null
                    ? item.InitiatedByUser.Username
                    : string.Empty,
                InitiatedByFullName = item.InitiatedByUser != null
                    ? item.InitiatedByUser.FullName
                    : string.Empty,

                item.AuthorizedBy,
                AuthorizedByUsername = item.AuthorizedByUser != null
                    ? item.AuthorizedByUser.Username
                    : null,
                AuthorizedByFullName = item.AuthorizedByUser != null
                    ? item.AuthorizedByUser.FullName
                    : null,
                item.AuthorizedAt,

                item.PostedBy,
                PostedByUsername = item.PostedByUser != null
                    ? item.PostedByUser.Username
                    : null,
                PostedByFullName = item.PostedByUser != null
                    ? item.PostedByUser.FullName
                    : null,
                item.PostedAt,

                item.ReversalOfTransactionId,
                item.CreatedAt,
                item.UpdatedAt
            }).FirstOrDefaultAsync(cancellationToken);
        if (transaction is null)
        {
            throw new NotFoundException(MessageCode.TransactionNotFound);
        }

        var accountEntries = await _dbContext.AccountTransactions
            .AsNoTracking()
            .Where(entry => entry.TransactionId == id)
            .OrderBy(entry => entry.Id)
            .Select(entry => new AccountEntryResponse(
            entry.AccountId,
            entry.Account!.AccountNo,
            entry.EntryType,
            entry.Amount,
            entry.LedgerBalanceBefore,
            entry.LedgerBalanceAfter,
            entry.AvailableBalanceBefore,
            entry.AvailableBalanceAfter,
            entry.ValueDate,
            entry.PostingDate,
            entry.Description,
            entry.ReferenceNo))
            .ToListAsync(cancellationToken);
        
        // Loads the complete accounting journal generated by this transaction.
        var accountingEntries = await _dbContext.TransactionEntries
            .AsNoTracking()
            .Where(entry => entry.TransactionId == id)
            .OrderBy(entry => entry.Id)
            .Select(entry => new TransactionAccountingEntryResponse(
                entry.Id,
                entry.GlAccountId,
                entry.GlAccount!.Code,
                entry.GlAccount.Name,
                entry.CustomerAccountId,
                entry.CustomerAccount != null
                    ? entry.CustomerAccount.AccountNo
                    : null,
                entry.EntryType,
                entry.Amount,
                entry.PostingDate,
                entry.Description,
                entry.CreatedAt))
            .ToListAsync(cancellationToken);

        // Loads only audit events belonging to this transaction.
        // Transaction audit records use TransactionNo as EntityId.
        var auditLogs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.EntityType == AuditConstants.TransactionEntityType &&
                log.EntityId == transaction.TransactionNo)
            .OrderBy(log => log.CreatedAt)
            .ThenBy(log => log.Id)
            .Select(log => new TransactionAuditLogResponse(
                log.Id,
                log.UserId,
                log.User != null
                    ? log.User.Username
                    : string.Empty,
                log.User != null
                    ? log.User.FullName
                    : string.Empty,
                log.Action,
                log.NewValues,
                log.IpAddress,
                log.DeviceInfo,
                log.CreatedAt))
            .ToListAsync(cancellationToken);

        // The pickup code hash is never selected. A sender who paid from an account has a debit entry; cash has none.
        var isPaidInCash = accountEntries.All(entry => entry.EntryType != Models.Transactions.EntryType.Debit);
        var nrcTransfer = await _dbContext.NrcCashTransferDetails
            .AsNoTracking()
            .Where(detail => detail.TransactionId == id)
            .Select(detail => new NrcTransferDetailResponse(
                detail.SenderName,
                detail.SenderNrc,
                detail.SenderPhone,
                detail.ReceiverName,
                detail.ReceiverNrc,
                detail.ReceiverPhone,
                detail.DeliveryType,
                detail.PickupBranch != null ? detail.PickupBranch.Name : detail.PickupOtherBank != null ? detail.PickupOtherBank.BankName : string.Empty,
                isPaidInCash,
                detail.DestinationAccountId,
                detail.Status,
                detail.PickupExpiresAt,
                detail.PickedUpAt,
                detail.FailedPickupAttempts))
            .FirstOrDefaultAsync(cancellationToken);

        var interbankTransfer = await _dbContext.InterbankTransferDetails
            .AsNoTracking()
            .Where(detail => detail.TransactionId == id)
            .Select(detail => new InterbankTransferDetailResponse(
                detail.OtherBankId,
                detail.OtherBank!.BankName,
                detail.DestinationAccountNo,
                detail.BeneficiaryName,
                detail.GatewayStatus,
                detail.GatewayReference,
                detail.SettlementReference,
                detail.RequestAt,
                detail.ResponseAt))
            .FirstOrDefaultAsync(cancellationToken);

        return new TransactionDetailResponse(
            transaction.Id,
            transaction.TransactionNo,
            transaction.TransactionType,
            transaction.TransactionStatus,
            transaction.Amount,
            transaction.FeeAmount,
            transaction.TransactionAt,
            transaction.Description,
            transaction.ReferenceNo,

            transaction.InitiatedBy,
            transaction.InitiatedByUsername,
            transaction.InitiatedByFullName,

            transaction.AuthorizedBy,
            transaction.AuthorizedByUsername,
            transaction.AuthorizedByFullName,
            transaction.AuthorizedAt,

            transaction.PostedBy,
            transaction.PostedByUsername,
            transaction.PostedByFullName,
            transaction.PostedAt,

            transaction.ReversalOfTransactionId,

            transaction.CreatedAt,
            transaction.UpdatedAt,

            accountEntries,
            accountingEntries,
            auditLogs,

            nrcTransfer,
            interbankTransfer)
        {
            BusinessDate = transaction.BusinessDate
        };
    }

    /// <summary>
    /// Lists the entries posted to one account, newest first, with the balance after each entry.
    /// </summary>
    public async Task<PagedResponse<AccountStatementLineResponse>> GetAccountStatementAsync(
        long accountId,
        AccountStatementQuery query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = TransactionRequestValidator.ResolvePaging(query.Page, query.PageSize);
        TransactionRequestValidator.ValidateDateRange(query.From, query.Before);

        var accountExists = await _dbContext.Accounts
            .AsNoTracking()
            .AnyAsync(account => account.Id == accountId, cancellationToken);
        if (!accountExists)
        {
            throw new NotFoundException(MessageCode.AccountNotFound);
        }

        var entries = _dbContext.AccountTransactions
            .AsNoTracking()
            .Where(entry => entry.AccountId == accountId);

        if (query.From is { } from)
        {
            var fromUtc = from.UtcDateTime;
            entries = entries.Where(entry => entry.CreatedAt >= fromUtc);
        }

        if (query.Before is { } before)
        {
            var beforeUtc = before.UtcDateTime;
            entries = entries.Where(entry => entry.CreatedAt < beforeUtc);
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var items = await entries
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entry => new AccountStatementLineResponse(
                entry.TransactionId,
                entry.Transaction!.TransactionNo,
                entry.Transaction!.TransactionType,
                entry.EntryType,
                entry.Amount,
                entry.LedgerBalanceAfter,
                entry.AvailableBalanceAfter,
                entry.PostingDate,
                entry.CreatedAt,
                entry.Description,
                entry.ReferenceNo))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AccountStatementLineResponse>(items, page, pageSize, totalCount, TotalPages);
    }
}
