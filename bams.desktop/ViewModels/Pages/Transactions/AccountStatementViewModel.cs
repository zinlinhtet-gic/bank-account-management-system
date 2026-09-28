using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// One statement line, e.g. "25 Sep 2026, 09:47 · Cash deposit · Credit +10,000.00 · balance 60,000.00".
/// </summary>
public sealed record StatementLineDisplayModel(
    string DateText,
    string TransactionNo,
    string TypeText,
    bool IsCredit,
    string AmountText,
    string BalanceAfterText,
    string DescriptionText)
{
    public string EntryTypeText => IsCredit ? "Credit" : "Debit";

    /// <summary>Builds a line from the server statement line.</summary>
    public static StatementLineDisplayModel FromResponse(AccountStatementLineResponse line)
    {
        var isCredit = line.EntryType == EntryType.Credit;

        return new StatementLineDisplayModel(
            TransactionDisplay.FormatTimestamp(line.CreatedAt),
            line.TransactionNo,
            TransactionDisplay.ToDisplayName(line.TransactionType),
            isCredit,
            (isCredit ? "+" : "−") + TransactionDisplay.FormatAmount(line.Amount),
            TransactionDisplay.FormatAmount(line.LedgerBalanceAfter),
            TransactionDisplay.OrDash(line.Description ?? line.ReferenceNo));
    }
}

/// <summary>
/// Account statement dialog: one account's entries, newest first, with the balance after each entry, a date range
/// and a pager. Load the first page with <see cref="LoadAsync"/> before showing it. Read-only; closes with <c>false</c>.
/// </summary>
public sealed class AccountStatementViewModel : ViewModelBase, IDialogViewModel
{
    private const int FirstPage = 1;

    private readonly ITransactionService _transactionService;
    private readonly long _accountId;

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private int _page = FirstPage;
    private int _totalCount;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public AccountStatementViewModel(ITransactionService transactionService, long accountId, string accountNo)
    {
        _transactionService = transactionService;
        _accountId = accountId;
        AccountNo = accountNo;

        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        PreviousPageCommand = new RelayCommand(_ => _ = ReloadAsync(Page - 1), _ => !IsLoading && Page > FirstPage);
        NextPageCommand = new RelayCommand(_ => _ = ReloadAsync(Page + 1), _ => !IsLoading && Page < TotalPages);
    }

    public event Action<bool>? CloseRequested;

    // Read-only, so clicking outside may close it.
    public bool CanCloseOnBackdropClick => true;

    public bool CanCancel => true;

    public string AccountNo { get; }

    public ObservableCollection<StatementLineDisplayModel> Lines { get; } = [];

    public RelayCommand CloseCommand { get; }

    // Same names as TransactionListViewModel so the shared TransactionPager view binds to this too.
    public RelayCommand PreviousPageCommand { get; }

    public RelayCommand NextPageCommand { get; }

    public int Page
    {
        get => _page;
        private set => SetProperty(ref _page, value);
    }

    public string PageText => $"Page {Page} of {TotalPages}";

    public string CountText => _totalCount == 1 ? "1 entry" : $"{_totalCount} entries";

    public bool IsEmpty => !IsLoading && Lines.Count == 0 && !HasError;

    /// <summary>First day to include (local date); changing it reloads from page 1.</summary>
    public DateTime? DateFrom
    {
        get => _dateFrom;
        set
        {
            if (SetProperty(ref _dateFrom, value))
            {
                _ = ReloadAsync(FirstPage);
            }
        }
    }

    /// <summary>Last day to include (local date, inclusive); changing it reloads from page 1.</summary>
    public DateTime? DateTo
    {
        get => _dateTo;
        set
        {
            if (SetProperty(ref _dateTo, value))
            {
                _ = ReloadAsync(FirstPage);
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RaisePagingChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private int TotalPages => Math.Max(FirstPage, (int)Math.Ceiling(_totalCount / (double)TransactionFieldRules.PageSize));

    /// <summary>
    /// Loads the first page. Returns false with <see cref="ErrorMessage"/> set when the server call failed.
    /// </summary>
    public async Task<bool> LoadAsync()
    {
        await ReloadAsync(FirstPage);
        return !HasError;
    }

    // Loads one page with the current date range; failures go to the dialog's banner.
    private async Task ReloadAsync(int page)
    {
        if (DateFrom is not null && DateTo is not null && DateFrom.Value.Date > DateTo.Value.Date)
        {
            ErrorMessage = MessageCatalog.GetMessage(MessageCode.InvalidDateRange);
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            // "To" is inclusive on screen, so send the start of the following day as the exclusive bound.
            var result = await _transactionService.GetAccountStatementAsync(
                _accountId,
                DateFrom is null ? null : DateTimeDisplay.StartOfLocalDay(DateFrom.Value),
                DateTo is null ? null : DateTimeDisplay.StartOfLocalDay(DateTo.Value.AddDays(1)),
                Math.Max(FirstPage, page),
                TransactionFieldRules.PageSize,
                CancellationToken.None);

            Lines.Clear();
            foreach (var line in result.Items)
            {
                Lines.Add(StatementLineDisplayModel.FromResponse(line));
            }

            Page = result.Page;
            _totalCount = result.TotalCount;
            OnPropertyChanged(nameof(CountText));
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // The pager text, buttons and empty state depend on the page, total and loading state.
    private void RaisePagingChanged()
    {
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(IsEmpty));
        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }
}
