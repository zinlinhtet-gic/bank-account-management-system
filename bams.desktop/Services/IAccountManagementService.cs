using bams.desktop.DTOs.Accounts;

namespace bams.desktop.Services;

public interface IAccountManagementService
{
    Task<IReadOnlyList<AccountTypeResponse>> GetAccountTypesAsync(CancellationToken cancellationToken);
    Task<AccountPageResponse> GetAccountsAsync(AccountListCriteria criteria, CancellationToken cancellationToken);
    Task<AccountResponse> GetAccountAsync(long id, CancellationToken cancellationToken);
    Task<AccountResponse> CreateAccountAsync(CreateAccountCommand command, CancellationToken cancellationToken);
}
