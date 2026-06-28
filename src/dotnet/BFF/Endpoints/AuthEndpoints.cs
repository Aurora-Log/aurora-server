using BFF.RateLimiting;
using Auth.Grpc;
using Grpc.Core;

namespace BFF.Endpoints;

/// <summary>
/// Auth Endpoints — /auth/login, /auth/refresh, /auth/logout
/// Không đi qua YARP — BFF xử lý trực tiếp, sau đó set/clear HttpOnly Cookie.
/// Refresh token gọi qua IamTenant gRPC AuthService (BFF không gọi Cognito trực tiếp).
/// </summary>
public static class AuthEndpoints
{
    private const string AccessTokenCookie = "access_token";
    private const string RefreshTokenCookie = "refresh_token";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth")
            .RequireRateLimiting(RateLimitPolicies.AuthPolicy);

        group.MapPost("/login", LoginAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapPost("/refresh", RefreshAsync);
    }

    /// <summary>
    /// POST /auth/login
    /// Body: { email, password }
    /// → Gọi IamTenant gRPC AuthService.Login
    /// → Set HttpOnly Cookie access_token + refresh_token
    /// </summary>
    private static async Task<IResult> LoginAsync(
        LoginRequest body,
        AuthService.AuthServiceClient authClient,
        IConfiguration config,
        HttpContext context)
    {
        try
        {
            var grpcRequest = new Auth.Grpc.LoginRequest
            {
                Email = body.Email,
                Password = body.Password
            };

            var result = await authClient.LoginAsync(grpcRequest);

            SetAuthCookies(context, result.AccessToken, result.RefreshToken, result.ExpiresIn, config);

            return Results.Ok(new
            {
                UserId = result.UserId,
                TenantId = result.TenantId,
                ExpiresIn = result.ExpiresIn
            });
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return Results.Unauthorized();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied)
        {
            return Results.Json(new { detail = ex.Status.Detail }, statusCode: 403);
        }
    }

    /// <summary>
    /// POST /auth/refresh
    /// → Đọc refresh_token từ HttpOnly Cookie
    /// → Gọi IamTenant gRPC AuthService.RefreshToken
    /// → Set lại access_token Cookie
    /// </summary>
    private static async Task<IResult> RefreshAsync(
        AuthService.AuthServiceClient authClient,
        IConfiguration config,
        HttpContext context)
    {
        var refreshToken = context.Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Results.Unauthorized();

        try
        {
            var result = await authClient.RefreshTokenAsync(new Auth.Grpc.RefreshTokenRequest
            {
                RefreshToken = refreshToken
            });

            SetAuthCookies(context, result.AccessToken, result.RefreshToken, result.ExpiresIn, config);

            return Results.Ok(new { ExpiresIn = result.ExpiresIn });
        }
        catch (RpcException)
        {
            ClearAuthCookies(context);
            return Results.Unauthorized();
        }
    }

    /// <summary>
    /// POST /auth/logout
    /// → Xóa HttpOnly Cookies
    /// </summary>
    private static IResult LogoutAsync(HttpContext context)
    {
        ClearAuthCookies(context);
        return Results.Ok(new { message = "Logged out successfully." });
    }

    private static void SetAuthCookies(HttpContext context, string accessToken, string refreshToken, int expiresIn, IConfiguration config)
    {
        var isSecure = config.GetValue("Auth:CookieSecure", true);
        var domain = config["Auth:CookieDomain"];

        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            Domain = domain,
            Path = "/"
        };

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict, // refresh token cần strict hơn
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Domain = domain,
            Path = "/auth/refresh" // Chỉ gửi refresh_token lên path này
        };

        context.Response.Cookies.Append(AccessTokenCookie, accessToken, accessCookieOptions);
        context.Response.Cookies.Append(RefreshTokenCookie, refreshToken, refreshCookieOptions);
    }

    private static void ClearAuthCookies(HttpContext context)
    {
        context.Response.Cookies.Delete(AccessTokenCookie);
        context.Response.Cookies.Delete(RefreshTokenCookie);
    }

    private record LoginRequest(string Email, string Password);
}
