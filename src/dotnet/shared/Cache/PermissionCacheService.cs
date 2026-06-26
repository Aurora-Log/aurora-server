using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Shared.Cache;

/// <summary>
/// Redis implementation sử dụng IDistributedCache (StackExchange.Redis).
/// Được đăng ký qua AddStackExchangeRedisCache.
/// </summary>
public class PermissionCacheService(IDistributedCache cache) : IPermissionCacheService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(30);

    public async Task<UserPermissionCache?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var key = CacheKeys.UserPermissions(userId);
        var json = await cache.GetStringAsync(key, ct);
        return json is null ? null : JsonSerializer.Deserialize<UserPermissionCache>(json);
    }

    public async Task SetAsync(Guid userId, UserPermissionCache permCache, CancellationToken ct = default)
    {
        var key = CacheKeys.UserPermissions(userId);
        var json = JsonSerializer.Serialize(permCache);
        await cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DefaultExpiry
        }, ct);
    }

    public async Task InvalidateAsync(Guid userId, CancellationToken ct = default)
    {
        var key = CacheKeys.UserPermissions(userId);
        await cache.RemoveAsync(key, ct);
    }
}
