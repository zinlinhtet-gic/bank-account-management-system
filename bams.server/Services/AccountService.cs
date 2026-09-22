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

    public AccountService(
        ApplicationDbContext dbContext,
        IAccountDocumentService accountDocumentService)
    {
        _dbContext = dbContext;
        _accountDocumentService = accountDocumentService;
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
        var accountType = await _accountTypeService.GetAccountTypeByIdAsync(
            request.AccountTypeId,
            cancellationToken);
        var isFixedDeposit = _accountTypeService.IsFixedDeposit(accountType);
        ValidateFixedDepositFields(request, isFixedDeposit);
        var customers = await _accountHolderService.ResolveAndValidateHoldersAsync(request, cancellationToken);
        await _accountHolderService.ValidateRequiredProductsAsync(accountType, customers, cancellationToken);
        _accountTypeService.ValidateOpeningBalance(request.OpeningBalance, accountType);
        var ownershipPercentages = _accountHolderService.ValidateOwnershipPercentages(request);
        var documents = request.Documents ?? [];
        await _accountDocumentService.ValidateRequiredDocumentsAsync(
            accountType.Id,
            documents,
            cancellationToken);
        var now = DateTime.UtcNow;
        IReadOnlyList<string> storedFileReferences = [];
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var accountNumber = await GenerateAccountNumberAsync(
                request.AccountTypeId,
                now,
                cancellationToken);
            var account = await InsertAccountAsync(
                request,
                accountNumber,
                now,
                cancellationToken);
            await _accountHolderService.CreateAccountHoldersAsync(
                account,
                customers,
                ownershipPercentages,
                request,
                now,
                cancellationToken);

            // Persist the account first so its database identifier can organize private files.
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (isFixedDeposit)
            {
                await _fixedDepositService.CreateFixedDepositAsync(
                    account,
                    accountType,
                    customers[0],
                    request,
                    now,
                    cancellationToken);
            }
            storedFileReferences = await _accountDocumentService.StoreAccountDocumentsAsync(
                account,
                documents,
                now,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            account.AccountType = accountType;
            return account.ToResponse();
        }
        catch
        {
            // File storage cannot participate in the database transaction, so compensate on failure.
            _accountDocumentService.DeleteStoredFiles(storedFileReferences);
            throw;
        }
    }

    // Gets an account type by identifier and rejects unknown account types.
    private async Task<AccountType> GetAccountTypeByIdAsync(
        long accountTypeId,
        CancellationToken cancellationToken)
    {
        var accountType = await _dbContext.AccountTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(type => type.Id == accountTypeId, cancellationToken);

        if (accountType is null)
        {
            throw new NotFoundException(MessageCode.AccountTypeNotFound);
        }

        return account.ToResponse();
    }

    // Enforces the configured minimum opening balance for the account type.
    private void ValidateOpeningBalance(
        decimal openingBalance,
        AccountType accountType)
    {
        if (openingBalance < accountType.MinimumOpeningBalance)
        {
            throw new ValidationException(MessageCode.AccountBalanceCannotBeNegative);
        }
    }

    // Normalizes individual ownership and validates joint ownership percentages.
    private IReadOnlyList<decimal> ValidateOwnershipPercentages(
        CreateAccountRequest request)
    {
        if (!request.IsSharedAccount)
        {
            return [AccountConstants.FullOwnershipPercentage];
        }

        if (!request.OwnershipPercentage1.HasValue || !request.OwnershipPercentage2.HasValue)
        {
            throw new NotFoundException(MessageCode.AccountNotFound);
        }

        return account;
    }

    // Rejects a status-change reason that cannot fit in the history record.
    private static void ValidateAccountStatusReason(string? reason)
    {
        if (reason?.Length > AccountConstants.AccountStatusReasonMaximumLength)
        {
            throw new ValidationException(MessageCode.InvalidRequest);
        }
    }

    // Reactivates an account only from a reversible restricted status.
    private async Task ChangeToActiveStatusAsync(
        Account account,
        long changedBy,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        if (account.Status is not (AccountStatus.Dormant or AccountStatus.Suspended or AccountStatus.Frozen))
        {
            throw new BusinessRuleException(MessageCode.AccountStatusTransitionNotAllowed);
        }

        account.ActiveAt = changedAt;
        await ApplyAccountStatusChangeAsync(account, AccountStatus.Active, changedBy, reason, changedAt, cancellationToken);
    }

    // Marks an active account as dormant.
    private async Task ChangeToDormantStatusAsync(
        Account account,
        long changedBy,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        if (account.Status != AccountStatus.Active)
        {
            throw new BusinessRuleException(MessageCode.AccountStatusTransitionNotAllowed);
        }

        account.DormantAt = changedAt;
        await ApplyAccountStatusChangeAsync(account, AccountStatus.Dormant, changedBy, reason, changedAt, cancellationToken);
    }

    // Suspends an account that is active or dormant.
    private async Task ChangeToSuspendedStatusAsync(
        Account account,
        long changedBy,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        if (account.Status is not (AccountStatus.Active or AccountStatus.Dormant))
        {
            throw new BusinessRuleException(MessageCode.AccountStatusTransitionNotAllowed);
        }

        account.SuspendedAt = changedAt;
        await ApplyAccountStatusChangeAsync(account, AccountStatus.Suspended, changedBy, reason, changedAt, cancellationToken);
    }

    // Freezes an account that is active or dormant.
    private async Task ChangeToFrozenStatusAsync(
        Account account,
        long changedBy,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        if (account.Status is not (AccountStatus.Active or AccountStatus.Dormant))
        {
            throw new BusinessRuleException(MessageCode.AccountStatusTransitionNotAllowed);
        }

        account.FrozenAt = changedAt;
        await ApplyAccountStatusChangeAsync(account, AccountStatus.Frozen, changedBy, reason, changedAt, cancellationToken);
    }

    // Permanently closes an active account.
    private async Task ChangeToClosedStatusAsync(
        Account account,
        long changedBy,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        if (account.Status != AccountStatus.Active)
        {
            throw new BusinessRuleException(MessageCode.AccountStatusTransitionNotAllowed);
        }

        account.ClosedAt = changedAt;
        await ApplyAccountStatusChangeAsync(account, AccountStatus.Closed, changedBy, reason, changedAt, cancellationToken);
    }

    // Applies a validated status change and tracks its immutable history entry.
    private async Task ApplyAccountStatusChangeAsync(
        Account account,
        AccountStatus newStatus,
        long changedBy,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        var oldStatus = account.Status;
        account.Status = newStatus;
        account.UpdatedAt = changedAt;

        var history = new AccountStatusHistory
        {
            Account = account,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Reason = reason,
            ChangedBy = changedBy,
            ChangedAt = changedAt
        };

        await _dbContext.AccountStatusHistories.AddAsync(history, cancellationToken);
    }

    // Allocates the next sequence for the account type and UTC hour, then builds a 16-digit number.
    private async Task<string> GenerateAccountNumberAsync(
        long accountTypeId,
        DateTime currentDateTime,
        CancellationToken cancellationToken)
    {
        if (accountTypeId is < 1 or > AccountConstants.MaximumAccountTypeIdentifier)
        {
            throw new BusinessRuleException(MessageCode.AccountTypeIdentifierOutOfRange);
        }

        var generationPeriod = currentDateTime.ToString(
            AccountConstants.AccountNumberTimestampFormat,
            CultureInfo.InvariantCulture);

        var sequenceNumber = await GetSequenceNumberAsync(
            accountTypeId,
            generationPeriod,
            cancellationToken);

        if (sequenceNumber > AccountConstants.MaximumHourlyAccountNumberSequence)
        {
            throw new BusinessRuleException(MessageCode.AccountNumberSequenceExhausted);
        }

        var accountNumber = string.Concat(
            accountTypeId.ToString(
                $"D{AccountConstants.AccountTypeIdentifierWidth}",
                CultureInfo.InvariantCulture),
            generationPeriod,
            sequenceNumber.ToString(
                $"D{AccountConstants.AccountNumberSequenceWidth}",
                CultureInfo.InvariantCulture));

        if (accountNumber.Length != AccountConstants.AccountNumberRequiredLength)
        {
            throw new InvalidOperationException(
                "Generated account number does not have the required length.");
        }

        return accountNumber;
    }

    // Creates and tracks a new active account for the validated request.
    private async Task<Account> InsertAccountAsync(
        CreateAccountRequest request,
        string accountNumber,
        DateTime currentDateTime,
        CancellationToken cancellationToken)
    {
        var account = new Account
        {
            AccountNo = accountNumber,
            AccountTypeId = request.AccountTypeId,
            Status = AccountStatus.Active,
            OpenedAt = currentDateTime,
            ActiveAt = currentDateTime,
            AvailableBalance = request.OpeningBalance,
            LedgerBalance = request.OpeningBalance,
            CreatedAt = currentDateTime,
            UpdatedAt = currentDateTime
        };

        await _dbContext.Accounts.AddAsync(account, cancellationToken);
        return account;
    }

    // Atomically creates or increments the sequence for an account type and UTC hour.
    private async Task<int> GetSequenceNumberAsync(
        long accountTypeId,
        string generationPeriod,
        CancellationToken cancellationToken)
    {
        // The unique period key makes this MySQL insert-or-increment atomic for concurrent requests.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO `AccountNumberGenerations`
                (`AccountTypeId`, `GenerationPeriod`, `LastSequenceNumber`)
            VALUES
                ({accountTypeId}, {generationPeriod}, 1)
            ON DUPLICATE KEY UPDATE
                `LastSequenceNumber` = `LastSequenceNumber` + 1
            """,
            cancellationToken);

        // The transaction retains the row lock until the generated account is persisted.
        var sequenceNumber = await _dbContext.AccountNumberGenerations
            .AsNoTracking()
            .Where(generation =>
                generation.AccountTypeId == accountTypeId &&
                generation.GenerationPeriod == generationPeriod)
            .Select(generation => generation.LastSequenceNumber)
            .SingleAsync(cancellationToken);

        return sequenceNumber;
    }

}
