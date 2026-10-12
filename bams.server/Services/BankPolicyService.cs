using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Configuration;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Products;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class BankPolicyService : IBankPolicyService
{
    private const int PageSize = 10;

    private readonly ApplicationDbContext _dbContext;

    public BankPolicyService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Gets one page of bank policies, 10 per page.
    /// </summary>
    public async Task<PagedResponse<BankPolicyResponse>> GetBankPoliciesAsync(
        int page,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);

        var query = _dbContext.AccountTypes
            .AsNoTracking()
            .OrderBy(type => type.Code);

        var totalCount = await query.CountAsync(cancellationToken);
        var TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
        var accountTypes = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<BankPolicyResponse>(
            accountTypes.Select(type => type.ToResponse()).ToList(),
            page,
            PageSize,
            totalCount,TotalPages);
    }

    /// <summary>
    /// Validates the request and creates a new bank policy.
    /// </summary>
    public async Task<BankPolicyResponse> CreateBankPolicyAsync(
        CreateBankPolicyRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(request.Code, request.MinimumOpeningBalance, request.MinimumMaintainedBalance,
            request.DailyTransactionLimit, request.MonthlyTransactionLimit, request.CitizenRequiredRefer,
            request.ForeignRequiredRefer, request.Status, excludedId: null, cancellationToken);
        ValidateTransactionPolicyAmounts(request.WeeklyTransactionLimit, request.DailyWithdrawalLimit,
            request.MinimumDepositAmount, request.MinimumWithdrawalAmount);

        var accountType = new AccountType
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Category = request.Category,
            MinimumOpeningBalance = request.MinimumOpeningBalance,
            MinimumMaintainedBalance = request.MinimumMaintainedBalance,
            DailyTransactionLimit = request.DailyTransactionLimit,
            MonthlyTransactionLimit = request.MonthlyTransactionLimit,
            WeeklyTransactionLimit = request.WeeklyTransactionLimit,
            DailyWithdrawalLimit = request.DailyWithdrawalLimit,
            MinimumDepositAmount = request.MinimumDepositAmount,
            MinimumWithdrawalAmount = request.MinimumWithdrawalAmount,
            AllowWithdrawal = request.AllowWithdrawal,
            AllowTransfer = request.AllowTransfer,
            AllowPartialWithdrawal = request.AllowPartialWithdrawal,
            AllowCitizen = request.AllowCitizen,
            AllowForeigner = request.AllowForeigner,
            CitizenRequiredRefer = request.CitizenRequiredRefer,
            ForeignRequiredRefer = request.ForeignRequiredRefer,
            Status = request.Status.Trim()
        };

        await _dbContext.AccountTypes.AddAsync(accountType, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return accountType.ToResponse();
    }

    /// <summary>
    /// Validates the request and updates an existing bank policy.
    /// </summary>
    public async Task<BankPolicyResponse> UpdateBankPolicyAsync(
        long id,
        UpdateBankPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var accountType = await _dbContext.AccountTypes
            .FirstOrDefaultAsync(type => type.Id == id, cancellationToken);

        if (accountType is null)
        {
            throw new NotFoundException(MessageCode.AccountTypeNotFound);
        }

        await ValidateAsync(request.Code, request.MinimumOpeningBalance, request.MinimumMaintainedBalance,
            request.DailyTransactionLimit, request.MonthlyTransactionLimit, request.CitizenRequiredRefer,
            request.ForeignRequiredRefer, request.Status, excludedId: id, cancellationToken);
        ValidateTransactionPolicyAmounts(request.WeeklyTransactionLimit, request.DailyWithdrawalLimit,
            request.MinimumDepositAmount, request.MinimumWithdrawalAmount);

        accountType.Code = request.Code.Trim();
        accountType.Name = request.Name.Trim();
        accountType.Category = request.Category;
        accountType.MinimumOpeningBalance = request.MinimumOpeningBalance;
        accountType.MinimumMaintainedBalance = request.MinimumMaintainedBalance;
        accountType.DailyTransactionLimit = request.DailyTransactionLimit;
        accountType.MonthlyTransactionLimit = request.MonthlyTransactionLimit;
        accountType.WeeklyTransactionLimit = request.WeeklyTransactionLimit;
        accountType.DailyWithdrawalLimit = request.DailyWithdrawalLimit;
        accountType.MinimumDepositAmount = request.MinimumDepositAmount;
        accountType.MinimumWithdrawalAmount = request.MinimumWithdrawalAmount;
        accountType.AllowWithdrawal = request.AllowWithdrawal;
        accountType.AllowTransfer = request.AllowTransfer;
        accountType.AllowPartialWithdrawal = request.AllowPartialWithdrawal;
        accountType.AllowCitizen = request.AllowCitizen;
        accountType.AllowForeigner = request.AllowForeigner;
        accountType.CitizenRequiredRefer = request.CitizenRequiredRefer;
        accountType.ForeignRequiredRefer = request.ForeignRequiredRefer;
        accountType.Status = request.Status.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return accountType.ToResponse();
    }

    // The weekly and daily-withdrawal limits and the minimum deposit and withdrawal amounts are optional (null means
    // no limit), but a given value may not be negative.
    private static void ValidateTransactionPolicyAmounts(params decimal?[] amounts)
    {
        if (amounts.Any(amount => amount < 0))
        {
            throw new ValidationException(MessageCode.InvalidAmount);
        }
    }

    // Shared validation for create and update. excludedId lets update keep its own code.
    private async Task ValidateAsync(
        string code,
        decimal minimumOpeningBalance,
        decimal minimumMaintainedBalance,
        decimal? dailyTransactionLimit,
        decimal? monthlyTransactionLimit,
        int citizenRequiredRefer,
        int foreignRequiredRefer,
        string status,
        long? excludedId,
        CancellationToken cancellationToken)
    {
        var trimmedCode = code.Trim();

        if (trimmedCode.Length == 0 || status.Trim().Length == 0)
        {
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        }

        if (trimmedCode.Length > 40)
        {
            throw new ValidationException(MessageCode.FieldTooLong);
        }

        if (minimumOpeningBalance < 0 || minimumMaintainedBalance < 0
            || dailyTransactionLimit < 0 || monthlyTransactionLimit < 0
            || citizenRequiredRefer < 0 || foreignRequiredRefer < 0)
        {
            throw new ValidationException(MessageCode.InvalidAmount);
        }

        if (minimumMaintainedBalance > minimumOpeningBalance)
        {
            throw new ValidationException(MessageCode.BankPolicyBalanceRangeInvalid);
        }

        var codeTaken = await _dbContext.AccountTypes
            .AsNoTracking()
            .AnyAsync(type => type.Code == trimmedCode && type.Id != (excludedId ?? 0), cancellationToken);

        if (codeTaken)
        {
            throw new ConflictException(MessageCode.AccountTypeCodeAlreadyExists);
        }
    }
}
