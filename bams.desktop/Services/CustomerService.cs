using System.Globalization;
using System.Net.Http;
using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Customers;

namespace bams.desktop.Services;

/// <summary>
/// Customer Management API calls. Transport, token and error translation are handled by <see cref="ApiClient"/>.
/// </summary>
public sealed class CustomerService : ICustomerService
{
    // Round-trip date format the server's [FromQuery] DateOnly binder accepts.
    private const string QueryDateFormat = "yyyy-MM-dd";

    private readonly ApiClient _apiClient;

    public CustomerService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<PagedResponse<CustomerSummaryResponse>> GetCustomersAsync(GetCustomersRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<PagedResponse<CustomerSummaryResponse>>(
            ApiConstants.CustomersEndpoint + BuildQueryString(request),
            cancellationToken);
    }

    public Task<CustomerResponse> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        using var formContent = BuildCreateForm(request);
        return _apiClient.PostFormAsync<CustomerResponse>(ApiConstants.CustomersEndpoint, formContent, cancellationToken);
    }

    // Builds "?pageNumber=..&customerNo=..&customerName=..&kycStatus=..&status=..&riskLevel=..&startDate=..&endDate=..",
    // skipping every filter that was not supplied.
    private static string BuildQueryString(GetCustomersRequest request)
    {
        var parameters = new List<string> { $"pageNumber={request.PageNumber}" };

        AddParameter(parameters, "customerNo", request.CustomerNo);
        AddParameter(parameters, "customerName", request.CustomerName);
        AddParameter(parameters, "status", request.Status);

        if (request.KycStatus is not null)
        {
            parameters.Add($"kycStatus={request.KycStatus}");
        }

        if (request.RiskLevel is not null)
        {
            parameters.Add($"riskLevel={request.RiskLevel}");
        }

        if (request.StartDate is not null)
        {
            parameters.Add($"startDate={request.StartDate.Value.ToString(QueryDateFormat, CultureInfo.InvariantCulture)}");
        }

        if (request.EndDate is not null)
        {
            parameters.Add($"endDate={request.EndDate.Value.ToString(QueryDateFormat, CultureInfo.InvariantCulture)}");
        }

        return "?" + string.Join("&", parameters);
    }

    // Adds one URL-encoded name=value pair when the value is not empty.
    private static void AddParameter(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    // Maps the create form fields into multipart/form-data, matching the server's [FromForm] contract.
    private static MultipartFormDataContent BuildCreateForm(CreateCustomerRequest request)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(request.CustomerType.ToString()), nameof(CreateCustomerRequest.CustomerType) },
            { new StringContent(request.FullName), nameof(CreateCustomerRequest.FullName) },
            { new StringContent(request.DateOfBirth.ToString(QueryDateFormat, CultureInfo.InvariantCulture)), nameof(CreateCustomerRequest.DateOfBirth) }
        };

        AddFormField(form, nameof(CreateCustomerRequest.NrcNumber), request.NrcNumber);
        AddFormField(form, nameof(CreateCustomerRequest.PassportNumber), request.PassportNumber);
        AddFormField(form, nameof(CreateCustomerRequest.Phone), request.Phone);
        AddFormField(form, nameof(CreateCustomerRequest.Email), request.Email);

        return form;
    }

    // Adds one form field when the value is supplied; the server treats every one of these as optional.
    private static void AddFormField(MultipartFormDataContent form, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            form.Add(new StringContent(value), name);
        }
    }
}
