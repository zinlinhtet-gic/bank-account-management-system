using System.Collections.ObjectModel;
using bams.desktop.DTOs.Accounting;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Presents officer cash still unresolved after handoff, separately from the vault's own expected cash.</summary>
public sealed class VaultUnresolvedCashViewModel : ViewModelBase
{
    private decimal _unresolvedTotal;
    private decimal _bankExpectedTotal;

    public ObservableCollection<UnresolvedCashSessionResponse> Sessions { get; } = [];
    public decimal UnresolvedTotal { get => _unresolvedTotal; private set => SetProperty(ref _unresolvedTotal, value); }
    public decimal BankExpectedTotal { get => _bankExpectedTotal; private set => SetProperty(ref _bankExpectedTotal, value); }
    public bool HasUnresolvedSessions => Sessions.Count > 0;

    /// <summary>Explains why the vault difference and the bank-wide expectation can differ.</summary>
    public string ResolutionGuidance =>
        "Vault expected cash only includes cash officers already handed over. Shortages still on an officer session are " +
        "not vault cash. Correcting an officer's count moves the handed-over difference into the vault; recording an " +
        "expense or over/short lowers only that officer's unresolved amount and leaves vault expected cash unchanged.";

    // Replaces the displayed residuals with the latest server summary.
    public void Update(VaultCashSummaryResponse? summary)
    {
        Sessions.Clear();
        if (summary is not null)
            foreach (UnresolvedCashSessionResponse session in summary.UnresolvedSessions) Sessions.Add(session);
        UnresolvedTotal = summary?.UnresolvedTotal ?? 0m;
        BankExpectedTotal = summary?.BankExpectedTotal ?? 0m;
        OnPropertyChanged(nameof(HasUnresolvedSessions));
    }
}
