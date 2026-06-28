using Shared.Security;

namespace BFF.Middleware;

/// <summary>
/// Resolve TenantId từ JWT claims (Backend là Source of Truth).
/// TenantId đã được set trong CurrentUserContextMiddleware từ Cookie/JWT.
/// Middleware này validate và có thể enrich thêm context nếu cần.
/// </summary>
public class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        // Với các route public (health check, login) — bỏ qua
        var path = context.Request.Path.Value ?? "";
        if (IsPublicPath(path))
        {
            await next(context);
            return;
        }

        // TenantId phải có trong JWT claims (đã được populate bởi CurrentUserContextMiddleware)
        if (context.User.Identity?.IsAuthenticated == true && !currentUser.TenantId.HasValue)
        {
            logger.LogWarning("Authenticated user {UserId} missing TenantId claim. Path: {Path}",
                currentUser.UserId, path);
            // Không reject — SystemAdmin có thể không có TenantId (TenantId = null = system scope)
        }

        // Gắn TenantId vào response header nội bộ để có thể debug
        if (currentUser.TenantId.HasValue)
        {
            context.Response.Headers["X-Resolved-Tenant"] = currentUser.TenantId.ToString();
        }

        await next(context);
    }

    private static bool IsPublicPath(string path) =>
        path.StartsWith("/auth/login", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);
}
