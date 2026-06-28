using System.IdentityModel.Tokens.Jwt;
using Shared.Security;

namespace BFF.Middleware;

/// <summary>
/// Đọc access_token từ HttpOnly Cookie, giải mã JWT claims,
/// và populate ICurrentUserService cho các middleware/handlers downstream.
/// Phải chạy SAU Cookie Authentication middleware.
/// </summary>
public class CurrentUserContextMiddleware(RequestDelegate next)
{
    private const string AccessTokenCookieName = "access_token";

    public async Task InvokeAsync(HttpContext context, ICurrentUserContext currentUser)
    {
        // Đọc access_token từ HttpOnly Cookie
        var token = context.Request.Cookies[AccessTokenCookieName];

        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);

                    var userId = GetClaimGuid(jwt, JwtClaims.UserId);
                    var tenantId = GetClaimGuid(jwt, JwtClaims.TenantId);
                    var traceId = context.TraceIdentifier;
                    var permVersion = GetClaimInt(jwt, JwtClaims.PermissionVersion);

                    var roleIds = jwt.Claims
                        .Where(c => c.Type == JwtClaims.RoleIds)
                        .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        .ToList();

                    currentUser.Populate(userId, tenantId, traceId, permVersion, roleIds, []);
                }
            }
            catch
            {
                // Token không hợp lệ — để AuthorizationMiddleware xử lý reject
            }
        }

        await next(context);
    }

    private static Guid? GetClaimGuid(JwtSecurityToken jwt, string claimType)
    {
        var value = jwt.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
        return Guid.TryParse(value, out var result) ? result : null;
    }

    private static int? GetClaimInt(JwtSecurityToken jwt, string claimType)
    {
        var value = jwt.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
        return int.TryParse(value, out var result) ? result : null;
    }
}
