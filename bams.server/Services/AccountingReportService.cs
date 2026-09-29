using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Services.Interfaces;
using bams.server.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis;
using System.Globalization;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Customers;
using bams.server.Models.Products;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
        // Reporting queries are read-only, so entity tracking is unnecessary
        return await _dbContext.GlAccounts
                    .AsNoTracking()
                    .OrderBy(account => account.Code)
                    .Select(account => new GlAccountResponse(
                        account.Id,
                        account.Code,
                        account.Name,
                        account.AccountClass,
                        account.ParentId,
                        account.Status
                    )).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a general-ledger account by its unique identifier.
    /// </summary>
    public async Task<GlAccountResponse> GetGlAccountByIdAsync(long glAccountId,CancellationToken cancellationToken)
    {
        var account = await _dbContext.GlAccounts
                            .AsNoTracking()
                            .Where(account => account.Id == glAccountId)
                            .Select(account => new GlAccountResponse(
                                account.Id,
                                account.Code,
                                account.Name,
                                account.AccountClass,
                                account.ParentId,
                                account.Status
                            )).SingleOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            throw new NotFoundException(MessageCode.AccountNotFound);
        }
        return account;
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
    // Ensures the supplied month represents a valid calendar month.
    private static void ValidateMonth(int month)
    {
        if(month is < AccountingConstants.FirstMonthOfYear or > AccountingConstants.LastMonthOfYear)
        {
            throw new ValidationException(MessageCode.InvalidDate);
        }
    }

    /// <inheritdoc />
    public async Task RecordAccountOpeningTransactionAsync(
        decimal openingBalance,
        DateTime currentDateTime,
        CancellationToken cancellationToken)
    {
        // Create a new audit log entry for the account opening
    }
}
