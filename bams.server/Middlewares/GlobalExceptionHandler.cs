using System.Net;
using bams.server.DTO.Common;
using bams.server.Exceptions;
using bams.server.Messages;

namespace bams.server.Middlewares;

public sealed class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        RequestDelegate next,
        ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Converts application exceptions into stable API error responses.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await WriteExceptionResponseAsync(context, exception);
        }
    }

    // Maps known application exception types to HTTP status codes.
    private async Task WriteExceptionResponseAsync(
        HttpContext context,
        Exception exception)
    {
        var statusCode = GetStatusCode(exception);
        var messageCode = exception is AppException appException
            ? appException.Code
            : MessageCode.InternalServerError;

        if (exception is not AppException || exception is FileStorageException)
        {
            _logger.LogError(
                exception,
                "Unhandled exception returned message code {MessageCode}",
                messageCode);
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new ApiErrorResponse(
            (int)messageCode,
            messageCode.ToString(),
            exception is AppException ? exception.Message : MessageCatalog.GetMessage(messageCode),
            context.TraceIdentifier);

        await context.Response.WriteAsJsonAsync(response);
    }

    // Keeps protocol status mapping centralized and away from controllers.
    private static HttpStatusCode GetStatusCode(Exception exception)
    {
        return exception switch
        {
            AuthenticationRequiredException => HttpStatusCode.Unauthorized,
            NotFoundException => HttpStatusCode.NotFound,
            ValidationException => HttpStatusCode.BadRequest,
            ConflictException => HttpStatusCode.Conflict,
            ForbiddenException => HttpStatusCode.Forbidden,
            BusinessRuleException => HttpStatusCode.UnprocessableEntity,
            _ => HttpStatusCode.InternalServerError
        };
    }
}
