using System.Globalization;
using System.Text;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.DTO.Common;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;

namespace bams.server.Services;

public sealed class AccountService : IAccountService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAccountDocumentService _accountDocumentService;
    private readonly IAccountHolderService _accountHolderService;
    private readonly IAccountTypeService _accountTypeService;
    private readonly IFixedDepositService _fixedDepositService;
    private readonly IAuditLogService _auditLogService;
    private readonly IAccountTransactionService _accountTransactionService;
    private readonly IAccountingReportService _accountingReportService;

    public AccountService(
        ApplicationDbContext dbContext,
        IAccountDocumentService accountDocumentService,
        IAccountHolderService accountHolderService,
        IAccountTypeService accountTypeService,
        IFixedDepositService fixedDepositService,
        IAuditLogService auditLogService,
        IAccountTransactionService accountTransactionService,
        IAccountingReportService accountingReportService)
    {
        _dbContext = dbContext;
        _accountDocumentService = accountDocumentService;
        _accountHolderService = accountHolderService;
        _accountTypeService = accountTypeService;
        _fixedDepositService = fixedDepositService;
        _auditLogService = auditLogService;
        _accountTransactionService = accountTransactionService;
        _accountingReportService = accountingReportService;
    }

    /// <summary>
    /// Gets a forward-only cursor page of account summaries using a read-only database query.
    /// </summary>
    public async Task<CursorPagedResponse<AccountSummaryResponse>> GetAccountsAsync(
        GetAccountsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateGetAccountsRequest(request);
        var cursorAccountId = DecodeAccountCursor(request.Cursor);
        var search = request.Search?.Trim();
        var query = _dbContext.Accounts.AsNoTracking();

        // Keyset pagination continues strictly after the last account returned to the client.
        if (cursorAccountId.HasValue)
        {
            query = query.Where(account => account.Id > cursorAccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(account => account.AccountNo.Contains(search));
        }

        if (request.AccountTypeId.HasValue)
        {
            query = query.Where(account => account.AccountTypeId == request.AccountTypeId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(account => account.Status == request.Status.Value);
        }

        // Fetch one extra row to determine whether another page exists without a count query.
        var accounts = await query
            .OrderBy(account => account.Id)
            .Select(account => new AccountSummaryResponse(
                account.Id,
                account.AccountNo,
                account.AccountType!.Code,
                account.Status,
                account.AvailableBalance))
            .Take(request.PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = accounts.Count > request.PageSize;
        if (hasMore)
        {
            accounts.RemoveAt(accounts.Count - 1);
        }

        var nextCursor = hasMore && accounts.Count > 0
            ? EncodeAccountCursor(accounts[^1].Id)
            : null;

        return new CursorPagedResponse<AccountSummaryResponse>(
            accounts,
            hasMore,
            nextCursor);
    }

    // Rejects invalid list criteria before constructing the database query.
    private static void ValidateGetAccountsRequest(GetAccountsRequest request)
    {
        if (request.PageSize is < AccountConstants.MinimumAccountPageSize or > AccountConstants.MaximumAccountPageSize ||
            request.AccountTypeId <= 0)
        {
            throw new ValidationException(MessageCode.InvalidRequest);
        }

        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
        {
            throw new ValidationException(MessageCode.AccountStatusInvalid);
        }
    }

    // Encodes the immutable account ID as an opaque, versioned Base64URL cursor.
    private static string EncodeAccountCursor(long accountId)
    {
        var cursorValue = string.Concat(
            AccountConstants.AccountCursorVersion,
            AccountConstants.AccountCursorSeparator,
            accountId.ToString(CultureInfo.InvariantCulture));

        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(cursorValue));
    }

    // Decodes and validates an optional versioned account cursor.
    private static long? DecodeAccountCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var cursorValue = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor));
            var cursorParts = cursorValue.Split(AccountConstants.AccountCursorSeparator);

            if (cursorParts.Length != 2 ||
                cursorParts[0] != AccountConstants.AccountCursorVersion ||
                !long.TryParse(
                    cursorParts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var accountId) ||
                accountId <= 0)
            {
                throw new ValidationException(MessageCode.AccountCursorInvalid);
            }

            return accountId;
        }
        catch (FormatException)
        {
            throw new ValidationException(MessageCode.AccountCursorInvalid);
        }
    }

    /// <summary>
    /// Gets a single account by its unique identifier.
    /// </summary>
    public async Task<AccountResponse> GetAccountByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .Include(account => account.AccountType)
            .FirstOrDefaultAsync(account => account.Id == id, cancellationToken);

        if (account is null)
        {
            throw new NotFoundException(MessageCode.AccountNotFound);
        }

        return account.ToResponse();
    }

    /// <summary>
    /// Creates an active account after validating request and business rules.
    /// </summary>
    public async Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var account = await GetTrackedAccountByIdAsync(accountId, cancellationToken);
        ValidateAccountStatusReason(reason);

        var changedAt = DateTime.UtcNow;
        var oldStatus = account.Status;

        switch (newStatus)
        {
            case AccountStatus.Active:
                await ChangeToActiveStatusAsync(account, changedBy, reason, changedAt, cancellationToken);
                break;
            case AccountStatus.Closed:
                await ChangeToClosedStatusAsync(account, changedBy, reason, changedAt, cancellationToken);
                break;
            case AccountStatus.Frozen:
                await ChangeToFrozenStatusAsync(account, changedBy, reason, changedAt, cancellationToken);
                break;
            case AccountStatus.Suspended:
                await ChangeToSuspendedStatusAsync(account, changedBy, reason, changedAt, cancellationToken);
                break;
            case AccountStatus.Dormant:
                await ChangeToDormantStatusAsync(account, changedBy, reason, changedAt, cancellationToken);
                break;
            default:
                throw new ValidationException(MessageCode.AccountStatusInvalid);
        }

        await _auditLogService.RecordAccountStatusUpdateLogAsync(
            account.Id,
            oldStatus,
            account.Status,
            reason,
            changedBy,
            changedAt,
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return account.ToResponse();
    }

    // Applies the supplied adjustment to both balances and returns the updated account.
    public async Task<AccountResponse> UpdateAccountBalanceAsync(
        long accountId,
        decimal balanceAdjustment,
        long changedBy,
        CancellationToken cancellationToken)
    {
        var account = await GetTrackedAccountByIdAsync(accountId, cancellationToken);
        var oldBalance = account.AvailableBalance;
        decimal newBalance = account.AvailableBalance + balanceAdjustment;

        if (newBalance < 0)
        {
            throw new ValidationException(MessageCode.AccountBalanceCannotBeNegative);
        }

        var now = DateTime.UtcNow;
        var account = new Account
        {
            AccountNo = accountNumber,
            AccountTypeId = request.AccountTypeId,
            Status = AccountStatus.Active,
            OpenedAt = now,
            AvailableBalance = request.OpeningBalance,
            LedgerBalance = request.OpeningBalance,
            CreatedAt = currentDateTime,
            UpdatedAt = currentDateTime
        };

        await _dbContext.Accounts.AddAsync(account, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        account.AccountType = accountType;

        return account.ToResponse();
    }

    // Generates a readable account number without relying on client-provided identifiers.
    private static string GenerateAccountNumber()
    {
        return string.Concat(
            AccountConstants.AccountNumberPrefix,
            DateTime.UtcNow.ToString(AccountConstants.AccountNumberTimestampFormat));
    }
}
