using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Operations;

namespace bams.desktop.ViewModels.Pages.Operations;

/// <summary>
/// Account and customer block shown at the top of every Operations detail card.
/// </summary>
public sealed class OperationAccountDisplayModel
{
    public OperationAccountDisplayModel(OperationAccountDetail account)
    {
        CustomerName = OperationDisplay.OrEmpty(account.CustomerName);
        CustomerNo = OperationDisplay.OrEmpty(account.CustomerNo);
        CustomerNrc = OperationDisplay.OrEmpty(account.CustomerNrc);
        CustomerPhone = OperationDisplay.OrEmpty(account.CustomerPhone);
        AccountNo = account.AccountNo;
        AccountType = $"{account.AccountTypeCode} · {account.AccountTypeName}";
        AccountStatus = account.AccountStatus.ToString();
        LedgerBalance = OperationDisplay.FormatMoney(account.LedgerBalance);
    }

    public string CustomerName { get; }
    public string CustomerNo { get; }
    public string CustomerNrc { get; }
    public string CustomerPhone { get; }
    public string AccountNo { get; }
    public string AccountType { get; }
    public string AccountStatus { get; }
    public string LedgerBalance { get; }
}

/// <summary>
/// Read-only detail card for one interest record (Operations › Interest), opened from the row's view button.
/// </summary>
public sealed class InterestOperationDetailsViewModel : ViewModelBase, IDialogViewModel
{
    public InterestOperationDetailsViewModel(InterestOperationDetailResponse interest)
    {
        Account = new OperationAccountDisplayModel(interest.Account);
        Title = $"Interest · {OperationDisplay.FormatMonth(interest.PeriodEnd)}";
        Status = interest.Status;
        Amount = OperationDisplay.FormatMoney(interest.Amount);
        Period = OperationDisplay.FormatPeriod(interest.PeriodStart, interest.PeriodEnd);
        Balance = OperationDisplay.FormatMoney(interest.CalculationBalance);
        Rate = OperationDisplay.FormatRate(interest.AnnualRate);
        Rule = $"Interest rate rule #{interest.InterestRateRuleId}";
        CalculatedAt = OperationDisplay.FormatOptionalDateTime(interest.CalculatedAt);
        PostedAt = OperationDisplay.FormatOptionalDateTime(interest.PostedAt);
        AccruedTransaction = OperationDisplay.FormatTransactionLink(interest.AccruedTransaction);
        PostedTransaction = OperationDisplay.FormatTransactionLink(interest.PostedTransaction);

        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
    }

    public event Action<bool>? CloseRequested;

    // Nothing to lose on a detail card, so clicking outside closes it.
    public bool CanCloseOnBackdropClick => true;

    public bool CanCancel => true;

    public OperationAccountDisplayModel Account { get; }
    public string Title { get; }
    public string Status { get; }
    public string Amount { get; }
    public string Period { get; }
    public string Balance { get; }
    public string Rate { get; }
    public string Rule { get; }
    public string CalculatedAt { get; }
    public string PostedAt { get; }

    /// <summary>The month-end accrual entry, e.g. "TXN… · 1,085.89 · 05/09/2026, 00:00".</summary>
    public string AccruedTransaction { get; }

    /// <summary>The quarterly credit that paid this accrual (it may cover several months).</summary>
    public string PostedTransaction { get; }

    public RelayCommand CloseCommand { get; }
}

/// <summary>
/// Read-only detail card for one fee record (Operations › Fees), opened from the row's view button.
/// </summary>
public sealed class FeeOperationDetailsViewModel : ViewModelBase, IDialogViewModel
{
    public FeeOperationDetailsViewModel(FeeOperationDetailResponse fee)
    {
        Account = new OperationAccountDisplayModel(fee.Account);
        FeeType = OperationDisplay.FormatFeeType(fee.FeeType);
        Title = $"{FeeType} · {OperationDisplay.FormatMonth(fee.PeriodEnd)}";
        Status = fee.Status.ToString();
        Amount = OperationDisplay.FormatMoney(fee.Amount);
        Tax = OperationDisplay.FormatMoney(fee.TaxAmount);
        Period = OperationDisplay.FormatPeriod(fee.PeriodStart, fee.PeriodEnd);
        Rule = OperationDisplay.FormatFeeRule(fee.FeeRuleId, fee.RuleAmount, fee.RulePercentage);
        CalculatedAt = OperationDisplay.FormatOptionalDateTime(fee.CalculatedAt);
        PostedAt = OperationDisplay.FormatOptionalDateTime(fee.PostedAt);
        AccruedTransaction = OperationDisplay.FormatTransactionLink(fee.AccruedTransaction);
        PostedTransaction = OperationDisplay.FormatTransactionLink(fee.PostedTransaction);

        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
    }

    public event Action<bool>? CloseRequested;

    public bool CanCloseOnBackdropClick => true;

    public bool CanCancel => true;

    public OperationAccountDisplayModel Account { get; }
    public string FeeType { get; }
    public string Title { get; }
    public string Status { get; }
    public string Amount { get; }
    public string Tax { get; }
    public string Period { get; }
    public string Rule { get; }
    public string CalculatedAt { get; }
    public string PostedAt { get; }
    public string AccruedTransaction { get; }

    /// <summary>The posting that deducted this fee (a quarterly maintenance deduction may cover several months).</summary>
    public string PostedTransaction { get; }

    public RelayCommand CloseCommand { get; }
}

/// <summary>One month of a fixed deposit's interest schedule, formatted for the detail card.</summary>
public sealed record FixedDepositInterestLineDisplayModel(string Period, string Amount, string Status, string PostedAt);

/// <summary>
/// Read-only detail card for one fixed deposit (Operations › Fixed Deposit Maturity): term, principal,
/// maturity, payout and the month-by-month interest schedule.
/// </summary>
public sealed class FixedDepositMaturityDetailsViewModel : ViewModelBase, IDialogViewModel
{
    public FixedDepositMaturityDetailsViewModel(FixedDepositMaturityDetailResponse deposit)
    {
        Account = new OperationAccountDisplayModel(deposit.Account);
        Title = $"Fixed deposit · {OperationDisplay.FormatTerm(deposit.TermDays, deposit.TermMonths)}";
        Status = deposit.Status.ToString();
        CurrentPrincipal = OperationDisplay.FormatMoney(deposit.CurrentPrincipal);
        OriginalPrincipal = OperationDisplay.FormatMoney(deposit.OriginalPrincipal);
        Rate = OperationDisplay.FormatRate(deposit.AnnualRate);
        Term = $"{OperationDisplay.FormatDate(deposit.StartDate)} → {OperationDisplay.FormatDate(deposit.MaturityDate)}";
        DaysLeft = deposit.Status == FixedDepositStatus.Active
            ? OperationDisplay.FormatDaysLeft(deposit.DaysToMaturity)
            : DisplayFormats.EmptyValue;
        InterestAccrued = OperationDisplay.FormatMoney(deposit.InterestAccrued);
        InterestPosted = OperationDisplay.FormatMoney(deposit.InterestPosted);
        MaturityInterest = OperationDisplay.FormatMoney(deposit.ExpectedMaturityInterest);
        RenewalInstruction = OperationDisplay.FormatRenewalInstruction(deposit.RenewalInstruction);
        PayoutAccountNo = OperationDisplay.OrEmpty(deposit.PayoutAccountNo);
        OpenedAt = OperationDisplay.FormatOptionalDateTime(deposit.CreatedAt);
        Schedule = deposit.InterestSchedule
            .Select(line => new FixedDepositInterestLineDisplayModel(
                OperationDisplay.FormatMonth(line.PeriodEnd),
                OperationDisplay.FormatAmount(line.Amount),
                line.Status,
                OperationDisplay.FormatOptionalDateTime(line.PostedAt)))
            .ToList();

        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
    }

    public event Action<bool>? CloseRequested;

    public bool CanCloseOnBackdropClick => true;

    public bool CanCancel => true;

    public OperationAccountDisplayModel Account { get; }
    public string Title { get; }
    public string Status { get; }
    public string CurrentPrincipal { get; }
    public string OriginalPrincipal { get; }
    public string Rate { get; }
    public string Term { get; }
    public string DaysLeft { get; }
    public string InterestAccrued { get; }
    public string InterestPosted { get; }
    public string MaturityInterest { get; }
    public string RenewalInstruction { get; }
    public string PayoutAccountNo { get; }
    public string OpenedAt { get; }

    public IReadOnlyList<FixedDepositInterestLineDisplayModel> Schedule { get; }

    public bool HasSchedule => Schedule.Count > 0;

    public RelayCommand CloseCommand { get; }
}
