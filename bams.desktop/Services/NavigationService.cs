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
            "User Management" => _serviceProvider.GetService<UserManagementViewModel>(),
            "Customer Management" => _serviceProvider.GetService<CustomerManagementViewModel>(),
            "Customer KYC" => _serviceProvider.GetService<CustomerKYCViewModel>(),
            "Account Management" => _serviceProvider.GetService<AccountManagementViewModel>(),
            "Transactions" => _serviceProvider.GetService<TransactionsViewModel>(),
            "Transaction History" => _serviceProvider.GetService<TransactionHistoryViewModel>(),
            "Accounting" => _serviceProvider.GetService<AccountingViewModel>(),
            "Operations" => _serviceProvider.GetService<OperationsViewModel>(),
            "Audit" => _serviceProvider.GetService<AuditViewModel>(),
            "Configurations" => _serviceProvider.GetService<ConfigurationsViewModel>(),
            "Customer List" => _serviceProvider.GetService<CustomerListViewModel>(),
            _ => null
        };
    }
}