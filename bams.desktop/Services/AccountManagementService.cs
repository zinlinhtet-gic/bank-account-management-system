using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounts;
using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Customers;

namespace bams.desktop.Services;

public sealed class AccountManagementService : IAccountManagementService
{
    private readonly ApiClient _apiClient;

    public AccountManagementService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<IReadOnlyList<AccountTypeResponse>> GetAccountTypesAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetRawAsync<IReadOnlyList<AccountTypeResponse>>(
            ApiConstants.AccountTypesEndpoint,
            cancellationToken);
    }

    public Task<IReadOnlyList<InterestRateRuleResponse>> GetInterestRateRulesAsync(
        long accountTypeId,
        CancellationToken cancellationToken)
    {
        return _apiClient.GetRawAsync<IReadOnlyList<InterestRateRuleResponse>>(
            $"{ApiConstants.InterestRateRulesEndpoint}?accountTypeId={accountTypeId.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken);
    }

    public Task<AccountPageResponse> GetAccountsAsync(
        AccountListCriteria criteria,
        CancellationToken cancellationToken)
    {
        var query = new List<string>
        {
            $"pageSize={criteria.PageSize.ToString(CultureInfo.InvariantCulture)}"
        };

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            query.Add($"search={Uri.EscapeDataString(criteria.Search.Trim())}");
        }

        if (criteria.AccountTypeId.HasValue)
        {
            query.Add($"accountTypeId={criteria.AccountTypeId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(criteria.Status))
        {
            query.Add($"status={Uri.EscapeDataString(criteria.Status)}");
        }

        if (!string.IsNullOrWhiteSpace(criteria.Cursor))
        {
            query.Add($"cursor={Uri.EscapeDataString(criteria.Cursor)}");
        }

        return _apiClient.GetRawAsync<AccountPageResponse>(
            $"{ApiConstants.AccountsEndpoint}?{string.Join("&", query)}",
            cancellationToken);
    }

    public Task<AccountResponse> GetAccountAsync(long id, CancellationToken cancellationToken)
    {
        return _apiClient.GetRawAsync<AccountResponse>(
            $"{ApiConstants.AccountsEndpoint}/{id.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken);
    }

    public Task<CustomerLookupResponse> GetCustomerByNrcAsync(string nrc, CancellationToken cancellationToken)
    {
        return _apiClient.GetRawAsync<CustomerLookupResponse>(
            $"{ApiConstants.AccountCustomerLookupEndpoint}?nrc={Uri.EscapeDataString(nrc.Trim())}", cancellationToken);
    }

    /// <summary>Creates a persisted customer and returns the server's saved profile.</summary>
    public Task<CustomerLookupResponse> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<CreateCustomerRequest, CustomerLookupResponse>(
            ApiConstants.CustomersEndpoint,
            request,
            cancellationToken);
    }

    public Task<AccountOpeningOptionsResponse> GetAccountOpeningOptionsAsync(string holderNrc, string? secondHolderNrc, CancellationToken cancellationToken)
    {
        var query = $"holderNrc={Uri.EscapeDataString(holderNrc.Trim())}";
        if (!string.IsNullOrWhiteSpace(secondHolderNrc)) query += $"&secondHolderNrc={Uri.EscapeDataString(secondHolderNrc.Trim())}";
        return _apiClient.GetRawAsync<AccountOpeningOptionsResponse>($"{ApiConstants.AccountOpeningOptionsEndpoint}?{query}", cancellationToken);
    }

    public Task<IReadOnlyList<AccountTransactionDetailResponse>> GetAccountTransactionsAsync(long id, CancellationToken cancellationToken) =>
        _apiClient.GetRawAsync<IReadOnlyList<AccountTransactionDetailResponse>>($"{ApiConstants.AccountsEndpoint}/{id}/transactions", cancellationToken);

    public Task<IReadOnlyList<AccountStatusHistoryResponse>> GetAccountStatusHistoryAsync(long id, CancellationToken cancellationToken) =>
        _apiClient.GetRawAsync<IReadOnlyList<AccountStatusHistoryResponse>>($"{ApiConstants.AccountsEndpoint}/{id}/status-history", cancellationToken);

    public Task<IReadOnlyList<InterestAccrualResponse>> GetAccountInterestAccrualsAsync(long id, CancellationToken cancellationToken) =>
        _apiClient.GetRawAsync<IReadOnlyList<InterestAccrualResponse>>($"{ApiConstants.AccountsEndpoint}/{id}/interest-accruals", cancellationToken);

    public async Task<AccountResponse> UpdateAccountStatusAsync(long id, string status, string? reason, long version, CancellationToken cancellationToken)
    {
        var action = status switch
        {
            "Active" => "reactivate",
            "Suspended" => "suspend",
            "Frozen" => "freeze",
            _ => throw new InvalidOperationException("This account status cannot be set through the available actions.")
        };
        return await _apiClient.PatchAsync<UpdateAccountStatusCommand, AccountResponse>(
            $"{ApiConstants.AccountsEndpoint}/{id}/{action}", new UpdateAccountStatusCommand(reason, version), cancellationToken);
    }

    public async Task<AccountResponse> CreateAccountAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        AddField(content, "AccountTypeId", command.AccountTypeId);
        AddField(content, "OpeningBalance", command.OpeningBalance);
        AddField(content, "IsSharedAccount", command.IsSharedAccount);
        AddOptionalField(content, "HolderNRC1", command.HolderNrc1);
        AddOptionalField(content, "HolderNRC2", command.HolderNrc2);
        AddOptionalField(content, "OwnershipPercentage1", command.OwnershipPercentage1);
        AddOptionalField(content, "OwnershipPercentage2", command.OwnershipPercentage2);
        AddOptionalField(content, "SigningRule", command.SigningRule);
        AddOptionalField(content, "PayoutAccountId", command.PayoutAccountId);
        AddOptionalField(content, "InterestRateRuleId", command.InterestRateRuleId);
        AddOptionalField(content, "RenewalInstruction", command.RenewalInstruction);
        AddOptionalField(content, "CalculateFromCurrent", command.CalculateFromCurrent);
        for (var index = 0; index < command.RefererNrcs.Count; index++)
        {
            AddField(content, $"RefererNrcs[{index}]", command.RefererNrcs[index]);
        }

        var openedFiles = new List<FileStream>();
        try
        {
            for (var index = 0; index < command.Documents.Count; index++)
            {
                var document = command.Documents[index];
                AddField(content, $"Documents[{index}].DocumentType", document.DocumentType);
                AddOptionalField(content, $"Documents[{index}].DocumentNumber", document.DocumentNumber);

                var stream = File.OpenRead(document.FilePath);
                openedFiles.Add(stream);
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetDocumentContentType(document.FilePath));
                content.Add(fileContent, $"Documents[{index}].File", Path.GetFileName(document.FilePath));
            }

            return await _apiClient.PostMultipartAsync<AccountResponse>(
                ApiConstants.AccountsEndpoint,
                content,
                cancellationToken);
        }
        finally
        {
            foreach (var stream in openedFiles)
            {
                await stream.DisposeAsync();
            }
        }
    }

    private static void AddField<T>(MultipartFormDataContent content, string name, T value)
    {
        content.Add(new StringContent(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, Encoding.UTF8), name);
    }

    private static void AddOptionalField<T>(MultipartFormDataContent content, string name, T? value)
    {
        if (value is not null)
        {
            AddField(content, name, value);
        }
    }

    // Matches the MIME types accepted by the server's account-document validator.
    private static string GetDocumentContentType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => "application/octet-stream"
    };
}
