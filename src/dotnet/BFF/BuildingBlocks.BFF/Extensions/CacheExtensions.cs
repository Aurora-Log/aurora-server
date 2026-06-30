using Microsoft.Extensions.DependencyInjection;
using Shared.Cache;

namespace BuildingBlocks.BFF.Extensions;

public static class CacheExtensions
{
    /// <summary>
    /// Đăng ký Redis distributed cache + PermissionCacheService.
    /// </summary>
    public static IServiceCollection AddBffCache(
        this IServiceCollection services,
        IConfiguration config)
    {
        var redisConn = config["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString is required");

        services.AddStackExchangeRedisCache(opts => opts.Configuration = redisConn);
        services.AddScoped<IPermissionCacheService, PermissionCacheService>();

        return services;
    }

    /// <summary>
    /// Lấy Redis connection string đã được validate.
    /// Dùng cho Health Checks registration cần biết connection string.
    /// </summary>
    public static string GetRedisConnectionString(IConfiguration config) =>
        config["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString is required");
}
