using Serilog.Context;
using System.Diagnostics;

namespace BuildingBlocks.BFF.Middleware;

/// <summary>
/// Sinh hoặc propagate X-Correlation-ID cho mỗi request.
/// Đẩy CorrelationId và TraceId vào Serilog LogContext để tất cả log entries
/// trong request scope tự động được enrich — giúp trace trên Loki.
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

        // Enrich tất cả logs trong request scope với CorrelationId và TraceId
        // Activity.Current?.TraceId được set bởi OpenTelemetry ASP.NET Core instrumentation
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString() ?? correlationId.ToString()))
        {
            await next(context);
        }
    }
}
