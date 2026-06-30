using System.Security.Claims;
using Shared.Security;

namespace BuildingBlocks.BFF.Middleware;

/// <summary>
/// Populate ICurrentUserService từ ClaimsPrincipal đã được validate bởi JwtBearer middleware.
/// KHÔNG đọc lại raw token từ Cookie — chỉ dùng HttpContext.User (đã validate chữ ký).
/// Phải chạy SAU UseAuthentication().
/// </summary>
public class CurrentUserContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUserContext currentUser)
    {
        // Chỉ populate khi user đã được authenticate (JWT đã pass validation)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId      = GetClaimGuid(context.User, JwtClaims.UserId);
            var tenantId    = GetClaimGuid(context.User, JwtClaims.TenantId);
            var permVersion = GetClaimInt(context.User, JwtClaims.PermissionVersion);
            var traceId     = context.TraceIdentifier;

            var roleIds = context.User.Claims
                .Where(c => c.Type == JwtClaims.RoleIds)
                .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .ToList();

            // Permissions sẽ được load từ Redis bởi PermissionVersionMiddleware (bước tiếp theo)
            currentUser.Populate(userId, tenantId, traceId, permVersion, roleIds, []);
        }

        await next(context);
    }

    private static Guid? GetClaimGuid(System.Security.Claims.ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return Guid.TryParse(value, out var result) ? result : null;
    }

    private static int? GetClaimInt(System.Security.Claims.ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return int.TryParse(value, out var result) ? result : null;
    }
}
