using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using bams.desktop.Constants;
using bams.desktop.DTOs.Common;
using bams.desktop.Exceptions;
using bams.desktop.Utils;
using bams.desktop.Services;

namespace bams.desktop.Api;

/// <summary>
/// Shared HTTP client for all feature services. Handles the bearer token, JSON serialization,
/// unwrapping the server's success envelope, and translating failures into typed exceptions.
/// </summary>
public sealed class ApiClient
{
    // Web defaults give camelCase, case-insensitive names; enums travel as strings like the server.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _httpClient;
    private readonly AuthContext _authContext;
    private readonly OfficerCashSessionContext _cashSessionContext;

    public ApiClient(HttpClient httpClient, AuthContext authContext, OfficerCashSessionContext cashSessionContext)
    {
        _httpClient = httpClient;
        _authContext = authContext;
        _cashSessionContext = cashSessionContext;
    }

    /// <summary>
    /// Attaches the JWT to every subsequent request.
    /// </summary>
    public void SetBearerToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(ApiConstants.BearerScheme, token);
    }

    /// <summary>
    /// Stops sending the JWT, e.g. after logout.
    /// </summary>
    public void ClearBearerToken()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Sends a GET request and returns the <c>Data</c> payload of the server response.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request or returned an unreadable body.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public Task<TResponse> GetAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken)
    {
        return SendAsync<TResponse>(
            () => _httpClient.GetAsync(endpoint, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Posts multipart form data and returns the standard success envelope's data payload.
    /// </summary>
    public async Task<TResponse> PostMultipartAsync<TResponse>(
        string endpoint,
        MultipartFormDataContent content,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(endpoint);
        using var response = await SendRequestAsync(
            () => _httpClient.PostAsync(endpoint, content, cancellationToken),
            cancellationToken);
        return await ReadResponseAsync<TResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Sends a JSON POST request and returns the <c>Data</c> payload of the server response.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request or returned an unreadable body.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(endpoint);
        return SendAsync<TResponse>(
            () => _httpClient.PostAsJsonAsync(endpoint, request, SerializerOptions, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Sends a JSON POST request with extra headers for this request only (e.g. <c>Idempotency-Key</c>) and returns
    /// the <c>Data</c> payload of the server response.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request or returned an unreadable body.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(endpoint);
        return SendAsync<TResponse>(
            () =>
            {
                // A request message can be sent only once, so it is built inside the send delegate.
                var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = JsonContent.Create(request, options: SerializerOptions)
                };
                foreach (var (name, value) in headers)
                {
                    message.Headers.Add(name, value);
                }

                return _httpClient.SendAsync(message, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>
    /// Sends a POST request without a body (state-change actions such as reset-password) and returns the
    /// <c>Data</c> payload of the server response.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request or returned an unreadable body.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public Task<TResponse> PostAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(endpoint);
        return SendAsync<TResponse>(
            () => _httpClient.PostAsync(endpoint, content: null, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Sends a multipart form POST request (for endpoints that accept <c>[FromForm]</c>, e.g. so a file
    /// can be attached) and returns the <c>Data</c> payload of the server response. The caller builds the
    /// form content, since only the feature service knows how its request maps to form fields.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request or returned an unreadable body.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public Task<TResponse> PostFormAsync<TResponse>(
        string endpoint,
        MultipartFormDataContent formContent,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(endpoint);
        return SendAsync<TResponse>(
            () => _httpClient.PostAsync(endpoint, formContent, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Sends a multipart form PATCH request (partial update with an optional file) and returns the
    /// <c>Data</c> payload of the server response.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request or returned an unreadable body.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public Task<TResponse> PatchFormAsync<TResponse>(
        string endpoint,
        MultipartFormDataContent formContent,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(endpoint);
        return SendAsync<TResponse>(
            () => _httpClient.PatchAsync(endpoint, formContent, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Sends a JSON PUT request (full update) and returns the <c>Data</c> payload of the server response.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request or returned an unreadable body.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public Task<TResponse> PutAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(endpoint);
        return SendAsync<TResponse>(
            () => _httpClient.PutAsJsonAsync(endpoint, request, SerializerOptions, cancellationToken),
            cancellationToken);
    }

    /// <summary>Sends a JSON PATCH request and returns the success envelope payload.</summary>
    public Task<TResponse> PatchAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(endpoint);
        return SendAsync<TResponse>(
            () => _httpClient.PatchAsJsonAsync(endpoint, request, SerializerOptions, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Sends a DELETE request and validates the standard success envelope.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public async Task DeleteAsync(
        string endpoint,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(endpoint);
        _ = await SendAsync<bool>(
            () => _httpClient.DeleteAsync(endpoint, cancellationToken),
            cancellationToken);
    }

    // Require an open teller session only for teller business writes, not account/customer maintenance or authentication.
    private void EnsureWriteAllowed(string endpoint)
    {
        if (!string.Equals(_authContext.Role, "officer", StringComparison.OrdinalIgnoreCase) ||
            _cashSessionContext.HasOpenSession || !RequiresTellerSession(endpoint) || IsOpeningCashSessionEndpoint(endpoint))
            return;

        throw new ApiException(MessageCode.CashSessionNotOpen,
            "If no opened session, no write can be done.", null);
    }

    // Identifies financial transaction and cash-custody operations that require the officer's open teller session.
    private static bool RequiresTellerSession(string endpoint) =>
        IsEndpointOrChild(endpoint, ApiConstants.TransactionsEndpoint) ||
        IsEndpointOrChild(endpoint, ApiConstants.CashOperationsEndpoint) ||
        IsEndpointOrChild(endpoint, ApiConstants.CashHandoffsEndpoint);

    private static bool IsEndpointOrChild(string endpoint, string endpointRoot)
    {
        var path = endpoint.TrimStart('/').Split('?', 2)[0];
        return path.Equals(endpointRoot, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(endpointRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    // Allows session creation so an officer can satisfy the session requirement after signing in.
    private static bool IsOpeningCashSessionEndpoint(string endpoint) =>
        endpoint.TrimStart('/').Split('?', 2)[0].Equals(ApiConstants.CashOperationsEndpoint + "/sessions", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Downloads a file response (not the JSON envelope) to a local path.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request or returned an unreadable body.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public async Task DownloadFileAsync(
        string endpoint,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        using var response = await SendRequestAsync(
            () => _httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateApiException(content);
        }

        await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destinationStream = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 64 * 1024, useAsync: true);

        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }

    /// <summary>
    /// Downloads a file response (not the JSON envelope) into memory, e.g. to decode as an image.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request or returned an unreadable body.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public async Task<byte[]> GetBytesAsync(
        string endpoint,
        CancellationToken cancellationToken)
    {
        using var response = await SendRequestAsync(
            () => _httpClient.GetAsync(endpoint, cancellationToken),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateApiException(content);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    // Sends the request and reads the success payload.
    private static async Task<TResponse> SendAsync<TResponse>(
        Func<Task<HttpResponseMessage>> sendRequestAsync,
        CancellationToken cancellationToken)
    {
        using var response = await SendRequestAsync(sendRequestAsync, cancellationToken);

        return await ReadResponseAsync<TResponse>(response, cancellationToken);
    }

    // Runs the request and converts transport failures into NetworkException.
    private static async Task<HttpResponseMessage> SendRequestAsync(
        Func<Task<HttpResponseMessage>> sendRequestAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            return await sendRequestAsync();
        }
        catch (HttpRequestException exception)
        {
            throw new NetworkException(MessageCode.NetworkUnavailable, exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation the caller did not request.
            throw new NetworkException(MessageCode.RequestTimeout, exception);
        }
    }

    private static async Task<TResponse> ReadResponseAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(content);
        }

        var envelope = TryDeserialize<ApiMessageResponse<TResponse>>(content);

        if (envelope is null || envelope.Data is null)
        {
            throw new ApiException(MessageCode.InvalidServerResponse);
        }

        return envelope.Data;
    }

    // Keeps the server's code, message and trace id; falls back when the body is not an ApiErrorResponse.
    private static ApiException CreateApiException(string content)
    {
        var error = TryDeserialize<ApiErrorResponse>(content);

        if (error is null || string.IsNullOrWhiteSpace(error.Message))
        {
            return new ApiException(MessageCode.InvalidServerResponse);
        }

        return new ApiException((MessageCode)error.Code, error.Message, error.TraceId);
    }

    // Deserializes JSON, returning null for empty or malformed bodies instead of throwing.
    private static T? TryDeserialize<T>(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(content, SerializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
