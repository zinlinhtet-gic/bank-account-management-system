namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// One tab of the Transactions page (Deposit, Withdraw, ...). The view binds a tab RadioButton's IsChecked to
/// <see cref="IsSelected"/>; selecting it raises <see cref="Selected"/> so the page shows that form.
/// </summary>
public sealed class TransactionTabViewModel : ViewModelBase
{
    private bool _isSelected;

    public TransactionTabViewModel(TransactionFormKind kind, string label, string iconKey, string toolTip)
    {
        Kind = kind;
        Label = label;
        IconKey = iconKey;
        ToolTip = toolTip;
    }

    /// <summary>Raised when the user selects this tab.</summary>
    public event Action<TransactionTabViewModel>? Selected;

    public TransactionFormKind Kind { get; }

    public string Label { get; }

    /// <summary>Icon resource key, e.g. "Icon.Download".</summary>
    public string IconKey { get; }

    public string ToolTip { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value)
            {
                Selected?.Invoke(this);
            }
        }
    }

    /// <summary>Marks the tab selected without raising <see cref="Selected"/>, e.g. for the initial tab.</summary>
    public void SelectSilently()
    {
        _isSelected = true;
        OnPropertyChanged(nameof(IsSelected));
    }
}
