using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;

namespace bams.desktop.ViewModels.Pages.Accounting;

public sealed record ReviewJournalRow(string Account, string Direction, decimal Amount, string Context);
public sealed record ReviewBalanceRow(string Account, decimal Before, decimal Change, decimal After);

/// <summary>Read-only evidence and server-calculated effects are reviewed before a manager submits a decision.</summary>
public sealed class ReconciliationReviewDialog : ViewModelBase, IDialogViewModel
{
    public event Action<bool>? CloseRequested;
    public bool CanCloseOnBackdropClick => false;
    public bool CanCancel => true;
    public string Title { get; }
    public string Findings { get; }
    public string Evidence { get; }
    public string Explanation { get; }
    public string? Warning { get; }
    public IReadOnlyList<ReviewJournalRow> OriginalJournal { get; }
    public IReadOnlyList<ReviewJournalRow> ProposedJournal { get; }
    public IReadOnlyList<ReviewBalanceRow> AccountBalances { get; }
    public IReadOnlyList<CashPositionCorrectionEffect> CashPositions { get; }
    private string _note = "";
    public string Note { get => _note; set { SetProperty(ref _note, value); AcceptCommand.RaiseCanExecuteChanged(); RejectCommand.RaiseCanExecuteChanged(); } }
    public bool Approved { get; private set; }
    public RelayCommand AcceptCommand { get; }
    public RelayCommand RejectCommand { get; }
    public RelayCommand CancelCommand { get; }

    public ReconciliationReviewDialog(string title, string findings, string evidence, string explanation, string? warning,
        IReadOnlyList<ReviewJournalRow> original, IReadOnlyList<ReviewJournalRow> proposed,
        IReadOnlyList<ReviewBalanceRow> balances, IReadOnlyList<CashPositionCorrectionEffect> positions, bool canAccept)
    {
        Title = title; Findings = findings; Evidence = evidence; Explanation = explanation; Warning = warning;
        OriginalJournal = original; ProposedJournal = proposed; AccountBalances = balances; CashPositions = positions;
        AcceptCommand = new(_ => { Approved = true; CloseRequested?.Invoke(true); }, _ => canAccept && Note.Trim().Length >= 5);
        RejectCommand = new(_ => { Approved = false; CloseRequested?.Invoke(true); }, _ => Note.Trim().Length >= 5);
        CancelCommand = new(_ => CloseRequested?.Invoke(false));
    }
}
