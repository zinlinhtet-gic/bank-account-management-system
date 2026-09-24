using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using bams.desktop.Constants;
using bams.desktop.DTOs.Common;
using bams.desktop.Exceptions;
using bams.desktop.Utils;

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

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
    /// Sends a JSON POST request and returns the <c>Data</c> payload of the server response.
    /// </summary>
    /// <exception cref="ApiException">The server rejected the request or returned an unreadable body.</exception>
    /// <exception cref="NetworkException">The server could not be reached or timed out.</exception>
    public Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<TResponse>(
            () => _httpClient.PostAsJsonAsync(endpoint, request, SerializerOptions, cancellationToken),
            cancellationToken);
    }

    // Runs the request and converts transport failures into NetworkException.
    private static async Task<TResponse> SendAsync<TResponse>(
        Func<Task<HttpResponseMessage>> sendRequestAsync,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await sendRequestAsync();
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

        using (response)
        {
            return await ReadResponseAsync<TResponse>(response, cancellationToken);
        }
    }

    // Returns the success payload, or throws an ApiException built from the server's error body.
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
