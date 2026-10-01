using bams.desktop.Constants;
using bams.desktop.ViewModels;
using bams.desktop.ViewModels.Pages;
using bams.desktop.ViewModels.Pages.Configuration;
using Microsoft.Extensions.DependencyInjection;
using bams.desktop.ViewModels.Pages.Accounting;
using System.Printing;

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
            PageNames.CustomerManagement => _serviceProvider.GetService<CustomerListViewModel>(),
            PageNames.CustomerList => _serviceProvider.GetService<CustomerListViewModel>(),
            PageNames.CustomerKyc => _serviceProvider.GetService<CustomerKYCViewModel>(),
            PageNames.AccountManagement => _serviceProvider.GetService<AccountManagementViewModel>(),
            PageNames.Transactions => _serviceProvider.GetService<TransactionsViewModel>(),
            PageNames.TransactionHistory => _serviceProvider.GetService<TransactionHistoryViewModel>(),
            PageNames.GeneralLedger => _serviceProvider.GetService<GeneralLedgerViewModel>(),
            PageNames.AccountingEntries => _serviceProvider.GetService<AccountingEntriesViewModel>(),
            // Placeholder until reconciliation is built (shown by the generic page view).
            PageNames.Reconciliation => _serviceProvider.GetService<ReconciliationViewModel>(),
            PageNames.Operations => _serviceProvider.GetService<OperationsViewModel>(),
            PageNames.Audit => _serviceProvider.GetService<AuditViewModel>(),
            // The Audit nav group's only page; shows the audit placeholder until it has its own view.
            PageNames.TransactionAudit => _serviceProvider.GetService<AuditViewModel>(),
            PageNames.Configurations => _serviceProvider.GetService<ConfigurationsViewModel>(),
            PageNames.InterestRate => _serviceProvider.GetService<InterestRateViewModel>(),
            PageNames.FeeRate => _serviceProvider.GetService<FeeRateViewModel>(),
            PageNames.BankPolicies => _serviceProvider.GetService<BankPoliciesViewModel>(),
            PageNames.OtherBanks => _serviceProvider.GetService<OtherBanksViewModel>(),
            _ => null
        };
    }
}
