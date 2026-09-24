namespace bams.desktop.Constants;

/// <summary>
/// The bank's name as shown in the app. Change it here only; XAML uses
/// <c>{x:Static constants:BrandConstants.BankShortName}</c>.
/// </summary>
public static class BrandConstants
{
    /// <summary>Everyday name: window title, navigation bar, dialogs.</summary>
    public const string BankShortName = "GIC Bank";

    /// <summary>Full legal name: sign-in screen and copyright line.</summary>
    public const string BankFullName = "Global Innovation Consulting Bank";

    /// <summary>Sign-in screen footer.</summary>
    public const string CopyrightNotice = "© 2026 " + BankFullName + " · For internal use only";

    /// <summary>Sign-in screen tagline.</summary>
    public const string SignInTagline = "Customers, accounts and transactions in one place, for authorised " + BankShortName + " staff.";
}
