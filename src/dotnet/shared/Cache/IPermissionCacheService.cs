namespace Shared.Cache;

public interface IPermissionCacheService
{
    Task<UserPermissionCache?> GetAsync(Guid userId, CancellationToken ct = default);
    Task SetAsync(Guid userId, UserPermissionCache cache, CancellationToken ct = default);
    Task InvalidateAsync(Guid userId, CancellationToken ct = default);
}
