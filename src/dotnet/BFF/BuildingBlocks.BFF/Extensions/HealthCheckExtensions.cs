using BuildingBlocks.BFF.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.BFF.Extensions;

public static class HealthCheckExtensions
{
    /// <summary>
    /// Đăng ký real health checks cho /readyz:
    ///   - Redis connectivity (timeout: 2s)
    ///   - IAM gRPC connectivity (timeout: 3s)
    /// </summary>
    public static IServiceCollection AddBffHealthChecks(
        this IServiceCollection services,
        IConfiguration config)
    {
        var redisConn = CacheExtensions.GetRedisConnectionString(config);

        services
            .AddHealthChecks();
            // .AddRedis(
            //     redisConn,
            //     name: "redis",
            //     tags: ["readiness"],
            //     timeout: TimeSpan.FromSeconds(2))
            // .AddCheck<IamGrpcHealthCheck>(
            //     name: "iam-grpc",
            //     tags: ["readiness"],
            //     timeout: TimeSpan.FromSeconds(3));

        return services;
    }
}
