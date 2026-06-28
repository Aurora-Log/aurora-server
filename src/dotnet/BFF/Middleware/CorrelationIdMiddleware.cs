namespace BFF.Middleware;

/// <summary>
/// Sinh hoặc propagate X-Correlation-ID cho mỗi request.
/// Downstream services sẽ log theo cùng CorrelationId để dễ trace.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.TraceIdentifier = correlationId!;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}
