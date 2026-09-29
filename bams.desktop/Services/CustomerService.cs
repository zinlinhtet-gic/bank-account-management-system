using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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
    // The server's [FromForm] DateOnly/DateTime binder accepts this round-trip date format.
    private const string FormDateFormat = "yyyy-MM-dd";

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

    public Task<CustomerResponse> GetCustomerByIdAsync(long id, CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<CustomerResponse>($"{ApiConstants.CustomersEndpoint}/{id}", cancellationToken);
    }

    public Task DownloadCustomerDocumentAsync(long customerId, long documentId, string destinationPath, CancellationToken cancellationToken)
    {
        return _apiClient.DownloadFileAsync(
            $"{ApiConstants.CustomersEndpoint}/{customerId}/documents/{documentId}/file",
            destinationPath,
            cancellationToken);
    }

    public Task<byte[]> GetCustomerDocumentBytesAsync(long customerId, long documentId, CancellationToken cancellationToken)
    {
        return _apiClient.GetBytesAsync(
            $"{ApiConstants.CustomersEndpoint}/{customerId}/documents/{documentId}/file",
            cancellationToken);
    }

    public async Task<CustomerResponse> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        // Must actually await here: a non-async method returning the task would let "using" dispose
        // formContent (and the file streams inside it) as soon as this method returns, before the
        // upload finishes, cutting the request off mid-transfer.
        using var formContent = BuildCreateForm(request);
        return await _apiClient.PostFormAsync<CustomerResponse>(ApiConstants.CustomersEndpoint, formContent, cancellationToken);
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
            parameters.Add($"startDate={request.StartDate.Value.ToString(FormDateFormat, CultureInfo.InvariantCulture)}");
        }

        if (request.EndDate is not null)
        {
            parameters.Add($"endDate={request.EndDate.Value.ToString(FormDateFormat, CultureInfo.InvariantCulture)}");
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
            { new StringContent(request.DateOfBirth.ToString(FormDateFormat, CultureInfo.InvariantCulture)), nameof(CreateCustomerRequest.DateOfBirth) }
        };

        AddFormField(form, nameof(CreateCustomerRequest.Nationality), request.Nationality);
        AddFormField(form, nameof(CreateCustomerRequest.NrcNumber), request.NrcNumber);
        AddFormField(form, nameof(CreateCustomerRequest.PassportNumber), request.PassportNumber);
        AddFormField(form, nameof(CreateCustomerRequest.Phone), request.Phone);
        AddFormField(form, nameof(CreateCustomerRequest.Occupation), request.Occupation);
        AddFormField(form, nameof(CreateCustomerRequest.AddressLine1), request.AddressLine1);
        AddFormField(form, nameof(CreateCustomerRequest.AddressLine2), request.AddressLine2);
        AddFormField(form, nameof(CreateCustomerRequest.City), request.City);
        AddFormField(form, nameof(CreateCustomerRequest.State), request.State);
        AddFormField(form, nameof(CreateCustomerRequest.PostalCode), request.PostalCode);
        AddFormField(form, nameof(CreateCustomerRequest.Country), request.Country);
        AddFormField(form, nameof(CreateCustomerRequest.Email), request.Email);

        AddDocuments(form, request.Documents);

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

    // ASP.NET Core's List<T>[N] form binding only works with contiguous zero-based indices, so a document
    // without a file is left out entirely rather than sent with a gap in the index sequence.
    private static void AddDocuments(MultipartFormDataContent form, List<CreateCustomerDocumentRequest>? documents)
    {
        if (documents is null)
        {
            return;
        }

        var index = 0;
        foreach (var document in documents)
        {
            if (string.IsNullOrWhiteSpace(document.FilePath))
            {
                continue;
            }

            var prefix = $"{nameof(CreateCustomerRequest.Documents)}[{index}]";

            form.Add(new StringContent(document.DocumentType.ToString()), $"{prefix}.{nameof(CreateCustomerDocumentRequest.DocumentType)}");
            AddFormField(form, $"{prefix}.{nameof(CreateCustomerDocumentRequest.DocumentNumber)}", document.DocumentNumber);

            if (document.IssuedDate is not null)
            {
                form.Add(
                    new StringContent(document.IssuedDate.Value.ToString(FormDateFormat, CultureInfo.InvariantCulture)),
                    $"{prefix}.{nameof(CreateCustomerDocumentRequest.IssuedDate)}");
            }

            if (document.ExpiryDate is not null)
            {
                form.Add(
                    new StringContent(document.ExpiryDate.Value.ToString(FormDateFormat, CultureInfo.InvariantCulture)),
                    $"{prefix}.{nameof(CreateCustomerDocumentRequest.ExpiryDate)}");
            }

            // Disposed together with the MultipartFormDataContent once the request completes.
            var fileContent = new StreamContent(File.OpenRead(document.FilePath));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, $"{prefix}.File", Path.GetFileName(document.FilePath));

            index++;
        }
    }
}
