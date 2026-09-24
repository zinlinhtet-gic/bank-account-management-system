using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounts;
using bams.desktop.DTOs.Common;

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
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, $"Documents[{index}].File", Path.GetFileName(document.FilePath));
            }

            var response = await _apiClient.PostMultipartAsync<ApiMessageResponse<AccountResponse>>(
                ApiConstants.AccountsEndpoint,
                content,
                cancellationToken);

            return response.Data ?? throw new InvalidOperationException("The account API returned an empty account.");
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
}
