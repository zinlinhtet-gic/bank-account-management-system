using bams.desktop.DTOs.Accounts;
using bams.desktop.DTOs.Customers;

namespace bams.desktop.Services;

public interface IAccountManagementService
{
    Task<IReadOnlyList<AccountTypeResponse>> GetAccountTypesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InterestRateRuleResponse>> GetInterestRateRulesAsync(long accountTypeId, CancellationToken cancellationToken);
    Task<AccountPageResponse> GetAccountsAsync(AccountListCriteria criteria, CancellationToken cancellationToken);
    Task<AccountResponse> GetAccountAsync(long id, CancellationToken cancellationToken);
    Task<CustomerLookupResponse> GetCustomerByNrcAsync(string nrc, CancellationToken cancellationToken);
    Task<CustomerLookupResponse> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken);
    Task<AccountOpeningOptionsResponse> GetAccountOpeningOptionsAsync(string holderNrc, string? secondHolderNrc, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccountTransactionDetailResponse>> GetAccountTransactionsAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccountStatusHistoryResponse>> GetAccountStatusHistoryAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<InterestAccrualResponse>> GetAccountInterestAccrualsAsync(long id, CancellationToken cancellationToken);
    Task<AccountResponse> UpdateAccountStatusAsync(long id, string status, string? reason, long version, CancellationToken cancellationToken);
    Task<AccountResponse> CreateAccountAsync(CreateAccountCommand command, CancellationToken cancellationToken);
}
