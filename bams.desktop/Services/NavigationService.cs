using bams.desktop.Constants;
using bams.desktop.ViewModels;
using bams.desktop.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace bams.desktop.Services;

/// <summary>
/// Implementation of the navigation service that maps page labels to ViewModels.
/// Uses dependency injection to create ViewModels as needed.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the ViewModel for a given navigation item label.
    /// </summary>
    public object? GetPageViewModel(string pageLabel)
    {
        return pageLabel switch
        {
            PageNames.UserManagement => _serviceProvider.GetService<UserManagementViewModel>(),
            PageNames.CustomerManagement => _serviceProvider.GetService<CustomerManagementViewModel>(),
            PageNames.CustomerKyc => _serviceProvider.GetService<CustomerKYCViewModel>(),
            PageNames.AccountManagement => _serviceProvider.GetService<AccountManagementViewModel>(),
            PageNames.Transactions => _serviceProvider.GetService<TransactionsViewModel>(),
            PageNames.TransactionHistory => _serviceProvider.GetService<TransactionHistoryViewModel>(),
            PageNames.Accounting => _serviceProvider.GetService<AccountingViewModel>(),
            PageNames.Operations => _serviceProvider.GetService<OperationsViewModel>(),
            PageNames.Audit => _serviceProvider.GetService<AuditViewModel>(),
            PageNames.Configurations => _serviceProvider.GetService<ConfigurationsViewModel>(),
            PageNames.CustomerList => _serviceProvider.GetService<CustomerListViewModel>(),
            _ => null
        };
    }
}