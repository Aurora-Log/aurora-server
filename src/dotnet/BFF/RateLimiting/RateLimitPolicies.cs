using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace BFF.RateLimiting;

public static class RateLimitPolicies
{
    public const string FixedByIp = "fixed-by-ip";
    public const string FixedByUser = "fixed-by-user";
    public const string AuthPolicy = "auth-strict";

    public static IServiceCollection AddBffRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue<int>("RateLimit:PermitLimit", 100);
        var windowSeconds = configuration.GetValue<int>("RateLimit:WindowSeconds", 60);
        var queueLimit = configuration.GetValue<int>("RateLimit:QueueLimit", 50);

        services.AddRateLimiter(opts =>
        {
            opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opts.OnRejected = async (ctx, token) =>
            {
                ctx.HttpContext.Response.Headers["Retry-After"] = windowSeconds.ToString();
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    Type = "https://httpstatuses.io/429",
                    Title = "Too Many Requests",
                    Status = 429,
                    Detail = "Rate limit exceeded. Please slow down.",
                    TraceId = ctx.HttpContext.TraceIdentifier
                }, token);
            };

            // Sliding Window per IP — cho mọi request public
            opts.AddSlidingWindowLimiter(FixedByIp, limiterOpts =>
            {
                limiterOpts.PermitLimit = permitLimit;
                limiterOpts.Window = TimeSpan.FromSeconds(windowSeconds);
                limiterOpts.SegmentsPerWindow = 4;
                limiterOpts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOpts.QueueLimit = queueLimit;
            });

            // Per authenticated User (stricter for mutation endpoints)
            opts.AddSlidingWindowLimiter(FixedByUser, limiterOpts =>
            {
                limiterOpts.PermitLimit = 50;
                limiterOpts.Window = TimeSpan.FromSeconds(60);
                limiterOpts.SegmentsPerWindow = 4;
                limiterOpts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOpts.QueueLimit = 10;
            });

            // Auth endpoints — Fixed Window rất chặt để chống brute-force
            opts.AddFixedWindowLimiter(AuthPolicy, limiterOpts =>
            {
                limiterOpts.PermitLimit = 10;
                limiterOpts.Window = TimeSpan.FromMinutes(1);
                limiterOpts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOpts.QueueLimit = 2;
            });

            // Default policy cho toàn bộ app
            opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        SegmentsPerWindow = 4,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = queueLimit
                    }));
        });

        return services;
    }
}
