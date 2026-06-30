using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace BuildingBlocks.BFF.Extensions;

public static class RequestProtectionExtensions
{
    /// <summary>
    /// Cấu hình Request Timeouts và Kestrel limits để chống DoS / slow-loris.
    /// </summary>
    public static IServiceCollection AddBffRequestProtection(this IServiceCollection services)
    {
        services.AddRequestTimeouts(opts =>
        {
            // Default timeout cho tất cả routes
            opts.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(30),
                TimeoutStatusCode = 408
            };

            // Auth endpoints — tighter timeout (login/refresh là fast operations)
            opts.AddPolicy("auth", new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(10),
                TimeoutStatusCode = 408
            });
        });

        return services;
    }

    /// <summary>
    /// Cấu hình Kestrel limits: body size, header size, slow-loris protection.
    /// </summary>
    public static IWebHostBuilder AddBffKestrelLimits(this IWebHostBuilder webHost)
    {
        webHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Limits.MaxRequestBodySize = 1 * 1024 * 1024; // 1 MB
            kestrel.Limits.MaxRequestHeadersTotalSize = 32 * 1024;        // 32 KB
            kestrel.Limits.MinRequestBodyDataRate = new MinDataRate(
                bytesPerSecond: 100,
                gracePeriod: TimeSpan.FromSeconds(10));
            kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
            kestrel.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(120);
        });

        return webHost;
    }
}
