using System.Net;
using System.Text.Json;

namespace BFF.Middleware;

/// <summary>
/// Global exception handler — catch tất cả unhandled exceptions,
/// trả về JSON theo chuẩn RFC 7807 Problem Details.
/// Phải là middleware đầu tiên trong pipeline để bắt được mọi lỗi.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}. CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (status, title) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            Shared.Exceptions.NotFoundException => (HttpStatusCode.NotFound, "Not Found"),
            Shared.Exceptions.ForbiddenException => (HttpStatusCode.Forbidden, "Forbidden"),
            Shared.Exceptions.ConflictException => (HttpStatusCode.Conflict, "Conflict"),
            Shared.Exceptions.DomainException => (HttpStatusCode.BadRequest, "Bad Request"),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        var problemDetails = new
        {
            Type = $"https://httpstatuses.io/{(int)status}",
            Title = title,
            Status = (int)status,
            Detail = exception.Message,
            Instance = context.Request.Path.Value,
            TraceId = context.TraceIdentifier
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)status;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, _jsonOptions));
    }
}
