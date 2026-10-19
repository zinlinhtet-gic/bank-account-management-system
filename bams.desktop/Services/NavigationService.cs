using bams.desktop.Constants;
using bams.desktop.ViewModels;
using bams.desktop.ViewModels.Pages;
using bams.desktop.ViewModels.Pages.Configuration;
using Microsoft.Extensions.DependencyInjection;
using bams.desktop.ViewModels.Pages.Accounting;
using bams.desktop.ViewModels.Pages.Operations;
using System.Printing;
using bams.desktop.ViewModels.Pages.Audit;
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
            PageNames.AccountManagement => _serviceProvider.GetService<AccountManagementViewModel>(),
            PageNames.Transactions => _serviceProvider.GetService<TransactionsViewModel>(),
            PageNames.TransactionHistory => _serviceProvider.GetService<TransactionHistoryViewModel>(),
            PageNames.GeneralLedger => _serviceProvider.GetService<GeneralLedgerViewModel>(),
            PageNames.GLAccountDetail => _serviceProvider.GetService<GLAccountDetailViewModel>(),
            PageNames.AccountingEntries => _serviceProvider.GetService<AccountingEntriesViewModel>(),
            // Placeholder until reconciliation is built (shown by the generic page view).
            PageNames.Reconciliation => _serviceProvider.GetService<UnifiedReconciliationViewModel>(),
            PageNames.ReconciliationMaster => _serviceProvider.GetService<UnifiedReconciliationViewModel>(),
            PageNames.AssignedReconciliationTasks => _serviceProvider.GetService<AssignedReconciliationTasksViewModel>(),
            PageNames.ReconcileProposals => _serviceProvider.GetService<ReconcileProposalsViewModel>(),
            PageNames.CashProposalEditor => _serviceProvider.GetService<CashProposalEditorViewModel>(),
            PageNames.BankExpenses => _serviceProvider.GetService<BankExpensesViewModel>(),
            PageNames.ReconciliationChecks => _serviceProvider.GetService<ReconciliationChecksViewModel>(),
            PageNames.ReconciliationExceptions => _serviceProvider.GetService<ReconciliationExceptionsViewModel>(),
            PageNames.ReconciliationInvestigations => _serviceProvider.GetService<ReconciliationInvestigationsViewModel>(),
            PageNames.ReconciliationProposals => _serviceProvider.GetService<ReconciliationProposalsViewModel>(),
            PageNames.ReconciliationApprovals => _serviceProvider.GetService<ReconciliationApprovalsViewModel>(),
            PageNames.ReconciliationVerification => _serviceProvider.GetService<ReconciliationVerificationViewModel>(),
            PageNames.ReconciliationHistory => _serviceProvider.GetService<ReconciliationHistoryViewModel>(),
            PageNames.ReconciliationProposalEditor => _serviceProvider.GetService<ReconciliationProposalEditorViewModel>(),

            PageNames.CashReconciliation => _serviceProvider.GetService<SessionManagementViewModel>(),
            PageNames.SessionCreation => _serviceProvider.GetService<SessionCreationViewModel>(),
            PageNames.SessionDetail => _serviceProvider.GetService<SessionDetailViewModel>(),
            PageNames.SessionHandoffs => _serviceProvider.GetService<CashHandoffCenterViewModel>(),
            PageNames.CashRequests => _serviceProvider.GetService<CashRequestCenterViewModel>(),
            PageNames.BusinessDateOpening => _serviceProvider.GetService<BusinessDateOpeningViewModel>(),
            PageNames.EndOfDay => _serviceProvider.GetService<EndOfDayViewModel>(),
            PageNames.Operations => _serviceProvider.GetService<OperationsViewModel>(),
            PageNames.OperationInterest => _serviceProvider.GetService<OperationInterestViewModel>(),
            PageNames.OperationFees => _serviceProvider.GetService<OperationFeesViewModel>(),
            PageNames.FixedDepositMaturity => _serviceProvider.GetService<FixedDepositMaturityViewModel>(),
            PageNames.Audit => _serviceProvider.GetService<AuditViewModel>(),
            PageNames.TransactionAudit => _serviceProvider.GetService<TransactionAuditViewModel>(),
            PageNames.Configurations => _serviceProvider.GetService<ConfigurationsViewModel>(),
            PageNames.InterestRate => _serviceProvider.GetService<InterestRateViewModel>(),
            PageNames.FeeRate => _serviceProvider.GetService<FeeRateViewModel>(),
            PageNames.BankPolicies => _serviceProvider.GetService<BankPoliciesViewModel>(),
            PageNames.OtherBanks => _serviceProvider.GetService<OtherBanksViewModel>(),
            _ => null
        };
    }
}
