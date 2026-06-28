using Shared.Cache;
using Shared.Security;

namespace BFF.Middleware;

/// <summary>
/// Kiểm tra PermissionVersion trong JWT (cookie) có khớp với version đang lưu trong Redis không.
/// Nếu version lệch (admin vừa thay đổi quyền user), request sẽ bị reject với 401
/// để client phải đăng nhập lại và nhận token mới với quyền cập nhật.
/// </summary>
public class PermissionVersionMiddleware(
    RequestDelegate next,
    IPermissionCacheService permissionCache,
    ILogger<PermissionVersionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        // Chỉ kiểm tra với các request authenticated và có UserId + PermissionVersion
        if (context.User.Identity?.IsAuthenticated == true
            && currentUser.UserId.HasValue
            && currentUser.PermissionVersion.HasValue)
        {
            var cached = await permissionCache.GetAsync(currentUser.UserId.Value);

            if (cached is not null && cached.Version != currentUser.PermissionVersion.Value)
            {
                logger.LogWarning(
                    "PermissionVersion mismatch for User {UserId}. JWT={JwtVersion}, Cache={CacheVersion}. Rejecting.",
                    currentUser.UserId, currentUser.PermissionVersion, cached.Version);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    Type = "https://httpstatuses.io/401",
                    Title = "Token expired",
                    Detail = "Your permissions have been updated. Please log in again.",
                    Status = 401,
                    TraceId = context.TraceIdentifier
                });
                return;
            }
        }

        await next(context);
    }
}
