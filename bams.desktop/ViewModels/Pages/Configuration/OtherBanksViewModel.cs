using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Exceptions;
using bams.desktop.Models.Configuration;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Configuration;

/// ViewModel for the Other Banks list page.
public sealed class OtherBanksViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IOtherBankService _otherBankService;
    private string _errorMessage = string.Empty;
    private bool _isLoading;

    public OtherBanksViewModel(IOtherBankService otherBankService)
    {
        // Constructors only store dependencies. No server calls here.
        _otherBankService = otherBankService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
    }

    public string PageTitle => "Other Banks";
    public string PageDescription => "View only. Correspondent bank records are managed by back-office operations.";

    public ObservableCollection<OtherBankRowModel> Rows { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    /// <summary>True when a finished load returned no rows (shows the "no banks" message).</summary>
    public bool IsEmpty => !IsLoading && Rows.Count == 0;

    /// <summary>Counter shown next to the table title, e.g. "3 banks".</summary>
    public string CountText => Rows.Count == 1 ? "1 bank" : $"{Rows.Count} banks";

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // Called by MainViewModel every time the user opens this page.
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(cancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        try
        {
            IsLoading = true;

            var otherBanks = await _otherBankService.GetOtherBanksAsync(cancellationToken);

            Rows.Clear();
            foreach (var bank in otherBanks)
            {
                Rows.Add(new OtherBankRowModel
                {
                    BankCode = bank.BankCode,
                    BankName = bank.BankName,
                    SwiftCode = bank.SwiftCode ?? string.Empty,
                    Status = bank.Status
                });
            }

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
}
