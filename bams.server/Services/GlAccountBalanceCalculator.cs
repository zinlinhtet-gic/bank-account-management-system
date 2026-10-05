using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;

namespace bams.server.Services;

/// <summary>Applies debit and credit totals using a GL account's normal balance.</summary>
internal static class GlAccountBalanceCalculator
{
    public static decimal ApplyPeriodTotals(GlAccountClass accountClass, decimal openingBalance, decimal debit, decimal credit) =>
        accountClass switch
        {
            GlAccountClass.Asset or GlAccountClass.Expense => openingBalance + debit - credit,
            GlAccountClass.Liability or GlAccountClass.Equity or GlAccountClass.Income => openingBalance + credit - debit,
            _ => throw new BusinessRuleException(MessageCode.UnsupportedGlAccountClass)
        };
}
