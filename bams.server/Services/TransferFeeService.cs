using bams.server.Constants;
using bams.server.Data;
using bams.server.Models.Accounts;
using bams.server.Models.Products;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>
/// Works out the fee a transfer charges to its source account from the source account type's active
/// <see cref="FeeRule"/>: <see cref="FeeType.Transfer"/> for transfers to another customer's account in this bank,
/// <see cref="FeeType.InterbankTransfer"/> for transfers to other banks. No applicable rule means no fee.
/// </summary>
public sealed class TransferFeeService
{
    private readonly ApplicationDbContext _dbContext;

    public TransferFeeService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Calculates the fee for a transfer inside this bank. Moving money between accounts that share a holder is
    /// free; a transfer to someone else's account pays the source account type's transfer fee.
    /// </summary>
    public async Task<decimal> CalculateInternalTransferFeeAsync(
        Account source,
        Account destination,
        decimal amount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (await HaveCommonHolderAsync(source.Id, destination.Id, cancellationToken))
        {
            return 0m;
        }

        return await CalculateFeeAsync(source.AccountTypeId, FeeType.Transfer, amount, now, cancellationToken);
    }

    /// <summary>
    /// Calculates the fee for a transfer to an account at another bank.
    /// </summary>
    public Task<decimal> CalculateInterbankTransferFeeAsync(
        Account source,
        decimal amount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        return CalculateFeeAsync(source.AccountTypeId, FeeType.InterbankTransfer, amount, now, cancellationToken);
    }

    // True when at least one customer holds both accounts (own-account transfer).
    private Task<bool> HaveCommonHolderAsync(
        long sourceAccountId,
        long destinationAccountId,
        CancellationToken cancellationToken)
    {
        var destinationHolderIds = _dbContext.AccountHolders
            .Where(holder => holder.AccountId == destinationAccountId)
            .Select(holder => holder.CustomerId);

        return _dbContext.AccountHolders
            .AsNoTracking()
            .AnyAsync(
                holder => holder.AccountId == sourceAccountId && destinationHolderIds.Contains(holder.CustomerId),
                cancellationToken);
    }

    // Applies the newest active rule effective on the business date: fixed amount plus percentage of the transfer
    // amount, kept within the rule's minimum and maximum fee, rounded to the stored money scale.
    private async Task<decimal> CalculateFeeAsync(
        long accountTypeId,
        FeeType feeType,
        decimal amount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var today = BusinessTime.ToBusinessDate(now);
        var rule = await _dbContext.FeeRules
            .AsNoTracking()
            .Where(item => item.AccountTypeId == accountTypeId
                && item.FeeType == feeType
                && item.Status == TransactionConstants.ActiveFeeRuleStatus
                && item.EffectiveFrom <= today
                && (item.EffectiveTo == null || item.EffectiveTo >= today))
            .OrderByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
        if (rule is null)
        {
            return 0m;
        }

        var fee = (rule.Amount ?? 0m) + amount * (rule.Percentage ?? 0m) / TransactionConstants.PercentageDivisor;
        if (rule.MinimumFee is { } minimumFee && fee < minimumFee)
        {
            fee = minimumFee;
        }

        if (rule.MaximumFee is { } maximumFee && fee > maximumFee)
        {
            fee = maximumFee;
        }

        return decimal.Round(fee, TransactionConstants.MaximumAmountDecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
