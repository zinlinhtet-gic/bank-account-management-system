using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Models.Configuration;


/// An account type in the interest rate form's drop-down.
public sealed record AccountTypeOption(long Id, string Code, string Name)
{
    public static AccountTypeOption FromResponse(AccountTypeOptionResponse accountType)
    {
        return new AccountTypeOption(accountType.Id, accountType.Code, accountType.Name);
    }

    // ComboBox shows ToString() when no template is set.
    public override string ToString() => $"{Code} - {Name}";
}
