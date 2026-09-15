using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels;

/// <summary>
/// Demonstrates a thin ViewModel that loads account data through a service.
/// </summary>
public sealed class ExampleAccountListViewModel : ViewModelBase
{
    private readonly IExampleAccountService _accountService;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public ExampleAccountListViewModel(
        IExampleAccountService accountService)
    {
        _accountService = accountService;
        LoadAccountsCommand = new RelayCommand(
            async _ => await LoadAccountsAsync(CancellationToken.None),
            _ => !IsBusy);
    }

    public ObservableCollection<ExampleAccountDisplayModel> Accounts { get; } = [];

    public RelayCommand LoadAccountsCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                LoadAccountsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // Loads account data through the service layer and updates bindable UI state.
    private async Task LoadAccountsAsync(
        CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;

            var accounts = await _accountService.GetExampleAccountsAsync(
                cancellationToken);

            Accounts.Clear();

            foreach (var account in accounts)
            {
                Accounts.Add(account);
            }

            StatusMessage = MessageCatalog.GetMessage(MessageCode.Success);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
