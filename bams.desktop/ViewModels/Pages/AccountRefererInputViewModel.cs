using bams.desktop.ViewModels;

namespace bams.desktop.ViewModels.Pages;

/// <summary>Stores one editable account referrer NRC row.</summary>
public sealed class AccountRefererInputViewModel : ViewModelBase
{
    private string _nrc = string.Empty;

    public string Nrc
    {
        get => _nrc;
        set => SetProperty(ref _nrc, value);
    }
}
