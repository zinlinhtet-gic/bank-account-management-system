using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.DTO.Common;
using bams.server.Exceptions;
using bams.server.Services.Interfaces;
using bams.server.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis;
using System.Globalization;
using bams.server.DTO.Accounts;
using bams.server.Mapping;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Customers;
using bams.server.Models.Products;
using bams.server.Models.Transactions;

namespace bams.server.Services;

public sealed class AccountingReportService : IAccountingReportService
{
    private readonly ApplicationDbContext _dbContext;

    public AccountingReportService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    /// <summary>
    /// Retrieves all general-ledger accounts for reporting.
    /// </summary>
    public async Task<IReadOnlyList<GlAccountResponse>> GetGlAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await _dbContext.GlAccounts
            .AsNoTracking()
            .OrderBy(account => account.Code)
            .Select(account => new
            {
                account.Id,
                account.Code,
                account.Name,
                account.AccountClass,
                account.ParentId,
                account.Status
            })
            .ToListAsync(cancellationToken);

        return accounts
            .Select(account => new GlAccountResponse(
                account.Id,
                account.Code,
                account.Name,
                account.AccountClass,
                account.ParentId,
                NormalizeStatus(account.Status)))
            .ToList();
    }
    /// <summary>
    /// Retrieves a general-ledger account by its unique identifier.
    /// </summary>
    public async Task<GlAccountResponse> GetGlAccountByIdAsync(long glAccountId,CancellationToken cancellationToken)
    {
        var account = await _dbContext.GlAccounts
            .AsNoTracking()
            .Where(account => account.Id == glAccountId)
            .Select(account => new
            {
                account.Id,
                account.Code,
                account.Name,
                account.AccountClass,
                account.ParentId,
                account.Status
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            throw new NotFoundException(
                MessageCode.AccountNotFound);
        }

        return new GlAccountResponse(
            account.Id,
            account.Code,
            account.Name,
            account.AccountClass,
            account.ParentId,
            NormalizeStatus(account.Status));
    }

    /// <summary>
    /// Returns a GL account and every journal line for transactions that posted to it.
    /// </summary>
    public async Task<GlAccountDetailResponse> GetGlAccountDetailAsync(
        long glAccountId,
        int? requestedPage,
        int? requestedPageSize,
        CancellationToken cancellationToken)
    {
        var account = await GetGlAccountByIdAsync(glAccountId, cancellationToken);

        var (page, pageSize) = TransactionRequestValidator.ResolvePaging(requestedPage, requestedPageSize ?? 10);
        var relatedTransactionIds = _dbContext.TransactionEntries
            .AsNoTracking()
            .Where(entry => entry.GlAccountId == glAccountId)
            .Select(entry => entry.TransactionId)
            .Distinct();
        var totalCount = await relatedTransactionIds.CountAsync(cancellationToken);
        var transactionIds = await relatedTransactionIds
            .OrderByDescending(id => _dbContext.Transactions
                .Where(transaction => transaction.Id == id)
                .Select(transaction => transaction.TransactionAt)
                .FirstOrDefault())
            .ThenByDescending(id => id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var entries = await _dbContext.TransactionEntries
            .AsNoTracking()
            .Where(entry => transactionIds.Contains(entry.TransactionId))
            .OrderByDescending(entry => entry.Transaction!.TransactionAt)
            .ThenBy(entry => entry.TransactionId)
            .ThenBy(entry => entry.Id)
            .Select(entry => new AccountingEntryResponse(
                entry.Id,
                entry.TransactionId,
                entry.Transaction!.TransactionNo,
                entry.Transaction.TransactionType,
                entry.Transaction.TransactionAt,
                entry.GlAccountId,
                entry.GlAccount!.Code,
                entry.GlAccount.Name,
                entry.CustomerAccountId,
                entry.EntryType,
                entry.Amount,
                entry.PostingDate,
                entry.Description,
                entry.CreatedAt))
            .ToListAsync(cancellationToken);

        return new GlAccountDetailResponse(account, new PagedResponse<AccountingEntryResponse>(
            entries, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize)));
    }

    /// <summary>
    /// Retrieves daily accounting summaries for a specific date
    /// and optionally limits the result to one GL account.
    /// </summary>
    public async Task<IReadOnlyList<DailySummaryResponse>> GetDailySummariesAsync(DateOnly summaryDate,long? glAccountId,CancellationToken cancellationToken)
    {
        var query = _dbContext.DailySummaries
            .AsNoTracking()
            .Where(summary =>
                summary.SummaryDate == summaryDate);
        // Apply account filtering only when the caller provides an account ID.
        if (glAccountId.HasValue)
        {
            query = query.Where(summary => summary.GlAccountId == glAccountId.Value);
        }
        return await query
            .OrderBy(summary => summary.GlAccount!.Code)
            .Select(summary => new DailySummaryResponse(
                summary.Id,
                summary.SummaryDate,
                summary.GlAccountId,
                summary.GlAccount!.Code,
                summary.GlAccount.Name,
                summary.OpeningBalance,
                summary.TotalDebit,
                summary.TotalCredit,
                summary.ClosingBalance,
                summary.GeneratedAt))
            .ToListAsync(cancellationToken);
    }
    /// <summary>
    /// Retrieves monthly accounting summaries for a specific period
    /// and optionally limits the result to one GL account.
    /// </summary>
    public async Task<IReadOnlyList<MonthlySummaryResponse>> GetMonthlySummariesAsync(int year, int month, long? glAccountId, CancellationToken cancellationToken)
    {
        ValidateMonth(month);
        var query = _dbContext.MonthlySummaries
                .AsNoTracking()
                .Where(summary => summary.Year == year && summary.Month == month);
        // Apply account filtering only when the caller provides an account ID.
        if (glAccountId.HasValue)
        {
            query = query.Where(summary => summary.GlAccountId == glAccountId.Value);
        }
        return await query
            .OrderBy(summary => summary.GlAccount!.Code)
            .Select(summary => new MonthlySummaryResponse(
                summary.Id,
                summary.Year,
                summary.Month,
                summary.GlAccountId,
                summary.GlAccount!.Code,
                summary.GlAccount.Name,
                summary.OpeningBalance,
                summary.TotalDebit,
                summary.TotalCredit,
                summary.ClosingBalance,
                summary.GeneratedAt
            )).ToListAsync(cancellationToken);
    }

    public async Task<PagedResponse<AccountingEntryResponse>> GetAccountingEntriesAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        long? glAccountId,
        EntryType? entryType,
        int? requestedPage,
        int? requestedPageSize,
        CancellationToken cancellationToken
    )
    {
        var (page, pageSize) = TransactionRequestValidator.ResolvePaging(requestedPage, requestedPageSize ?? 10);
        var query = _dbContext.TransactionEntries.AsNoTracking().AsQueryable();
        if (fromDate.HasValue)
        {
            query = query.Where(entry => entry.PostingDate >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(entry => entry.PostingDate <= toDate.Value);
        }
        if (glAccountId.HasValue)
        {
            query = query.Where(entry => entry.GlAccountId == glAccountId.Value);
        }
        if (entryType.HasValue)
        {
            query = query.Where(entry => entry.EntryType == entryType.Value);
        }
        var transactionQuery = query
            .Select(entry => entry.TransactionId)
            .Distinct();
        var totalCount = await transactionQuery.CountAsync(cancellationToken);
        var transactionIds = await transactionQuery
            .OrderByDescending(id => _dbContext.Transactions
                .Where(transaction => transaction.Id == id)
                .Select(transaction => transaction.TransactionAt)
                .FirstOrDefault())
            .ThenByDescending(id => id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await query
            .Where(entry => transactionIds.Contains(entry.TransactionId))
            .OrderByDescending(entry => entry.PostingDate)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => new AccountingEntryResponse(
                entry.Id,
                entry.TransactionId,
                entry.Transaction!.TransactionNo,
                entry.Transaction.TransactionType,
                entry.Transaction.TransactionAt,
                entry.GlAccountId,
                entry.GlAccount!.Code,
                entry.GlAccount.Name,
                entry.CustomerAccountId,
                entry.EntryType,
                entry.Amount,
                entry.PostingDate,
                entry.Description,
                entry.CreatedAt
            )).ToListAsync(cancellationToken);

        return new PagedResponse<AccountingEntryResponse>(items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    // Ensures the supplied month represents a valid calendar month.
    private static void ValidateMonth(int month)
    {
        if(month is < AccountingConstants.FirstMonthOfYear or > AccountingConstants.LastMonthOfYear)
        {
            throw new ValidationException(MessageCode.InvalidDate);
        }
    }

    private static string NormalizeStatus(string status)
    {
        return string.Equals(
            status,
            AccountingConstants.ActiveStatus,
            StringComparison.OrdinalIgnoreCase)
                ? AccountingConstants.ActiveStatus
                : status;
    }
}
